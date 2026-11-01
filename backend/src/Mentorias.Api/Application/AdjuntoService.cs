using Mentorias.Api.Contracts;
using Mentorias.Api.Domain;
using Mentorias.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Mentorias.Api.Application;

public sealed record ArchivoDescarga(AdjuntoResponse Adjunto, Stream Contenido);

public sealed class AdjuntoService(
    MentoriasDbContext db, IAlmacenArchivos almacen, INotificadorReservas notificador, TimeProvider reloj)
{
    public async Task<Resultado<AdjuntoResponse>> SubirAsync(int reservaId, IFormFile? archivo, CancellationToken ct)
    {
        if (archivo is null) return Errores.ArchivoRequerido;
        if (!await db.Reservas.AnyAsync(r => r.Id == reservaId, ct)) return Errores.ReservaNoEncontrada(reservaId);

        var existentes = await db.Adjuntos.CountAsync(a => a.ReservaId == reservaId, ct);
        var evaluacion = ReglasArchivo.Evaluar(archivo.FileName, archivo.Length, await LeerCabeceraAsync(archivo, ct), existentes);
        if (evaluacion.Rechazo is { } motivo) return Errores.ArchivoRechazado(motivo, evaluacion.Mensaje!);

        var id = Guid.NewGuid();
        await using var contenido = archivo.OpenReadStream();
        var sha256 = await almacen.GuardarAsync(id, contenido, ct);
        var adjunto = Mapeos.NuevoAdjunto(id, reservaId, evaluacion, archivo.Length, sha256, reloj.GetUtcNow());

        db.Adjuntos.Add(adjunto);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            almacen.Eliminar(id); // no dejar archivos huérfanos si falla la base de datos
            throw;
        }

        var respuesta = Mapeos.AResponse(adjunto);
        await notificador.AdjuntoAgregadoAsync(respuesta);
        return respuesta;
    }

    public async Task<Resultado<IReadOnlyList<AdjuntoResponse>>> ListarAsync(int reservaId, CancellationToken ct)
    {
        if (!await db.Reservas.AnyAsync(r => r.Id == reservaId, ct)) return Errores.ReservaNoEncontrada(reservaId);
        var adjuntos = await db.Adjuntos.AsNoTracking()
            .Where(a => a.ReservaId == reservaId)
            .OrderBy(a => a.CreadoUtc)
            .ToListAsync(ct);
        return adjuntos.Select(Mapeos.AResponse).ToList();
    }

    public async Task<Resultado<ArchivoDescarga>> DescargarAsync(Guid id, CancellationToken ct)
    {
        var adjunto = await db.Adjuntos.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
        return adjunto is not null && almacen.Abrir(id) is { } contenido
            ? new ArchivoDescarga(Mapeos.AResponse(adjunto), contenido)
            : Errores.AdjuntoNoEncontrado(id);
    }

    public async Task<Resultado<bool>> EliminarAsync(Guid id, CancellationToken ct)
    {
        var adjunto = await db.Adjuntos.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (adjunto is null) return Errores.AdjuntoNoEncontrado(id);

        db.Adjuntos.Remove(adjunto);
        await db.SaveChangesAsync(ct);
        almacen.Eliminar(id);
        await notificador.AdjuntoEliminadoAsync(adjunto.ReservaId, id);
        return true;
    }

    private static async Task<byte[]> LeerCabeceraAsync(IFormFile archivo, CancellationToken ct)
    {
        await using var stream = archivo.OpenReadStream();
        var buffer = new byte[Math.Min(ReglasArchivo.BytesCabecera, archivo.Length)];
        await stream.ReadExactlyAsync(buffer, ct);
        return buffer;
    }
}
