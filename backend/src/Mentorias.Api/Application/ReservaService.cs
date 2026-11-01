using Mentorias.Api.Contracts;
using Mentorias.Api.Domain;
using Mentorias.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Mentorias.Api.Application;

/// <summary>
/// Casos de uso de reservas. Orquesta E/S (base de datos, notificaciones); las decisiones
/// se delegan en funciones puras (Validadores, Agenda, ReglasReserva, Mapeos).
/// </summary>
public sealed class ReservaService(
    MentoriasDbContext db, INotificadorReservas notificador, IAlmacenArchivos almacen, TimeProvider reloj)
{
    public async Task<Resultado<ReservaResponse>> CrearAsync(ReservaRequest request, CancellationToken ct)
    {
        var validacion = Validadores.Reserva(request);
        if (!validacion.EsValida) return Errores.Validacion(validacion.Errores);
        var datos = validacion.Valor!;

        var mentoria = await MentoriaActivaAsync(datos.MentoriaId, ct);
        if (mentoria is null) return Errores.MentoriaInexistente;

        var ahora = reloj.GetUtcNow();
        if (Agenda.ValidarInicio(datos.Inicio, mentoria.DuracionMinutos, ahora) is { } motivo)
            return Errores.Validacion("inicio", motivo);

        var bloques = Agenda.BloquesDe(datos.Inicio, mentoria.DuracionMinutos);
        // Comprobación previa para dar un mensaje claro; la clave primaria de BloquesAgenda
        // es la garantía real cuando dos peticiones llegan a la vez.
        if (await HayBloquesOcupadosAsync(bloques, ct)) return Errores.HorarioOcupado;

        var reserva = Mapeos.NuevaReserva(datos, mentoria, bloques, ahora);
        db.Reservas.Add(reserva);
        if (await GuardarAsync(ct) is { } error) return error;

        var respuesta = Mapeos.AResponse(reserva, mentoria);
        await notificador.ReservaCreadaAsync(respuesta);
        await notificador.HorariosActualizadosAsync(Mapeos.FechasAfectadas(reserva.InicioUtc));
        return respuesta;
    }

    public async Task<Resultado<Paginado<ReservaResponse>>> ListarAsync(FiltroReservasQuery query, CancellationToken ct)
    {
        var validacion = Validadores.Filtro(query);
        if (!validacion.EsValida) return Errores.Validacion(validacion.Errores);
        var f = validacion.Valor!;

        var consulta = db.Reservas.AsNoTracking().Include(r => r.Mentoria).AsQueryable();
        if (f.Estado is { } estado) consulta = consulta.Where(r => r.Estado == estado);
        if (f.Correo is { } correo) consulta = consulta.Where(r => r.Correo == correo);
        if (f.Desde is { } d)
        {
            var desde = Agenda.RangoUtcDelDia(d).DesdeUtc;
            consulta = consulta.Where(r => r.InicioUtc >= desde);
        }
        if (f.Hasta is { } h)
        {
            var hasta = Agenda.RangoUtcDelDia(h).HastaUtc;
            consulta = consulta.Where(r => r.InicioUtc < hasta);
        }

        var total = await consulta.CountAsync(ct);
        var pagina = await consulta
            .OrderBy(r => r.InicioUtc).ThenBy(r => r.Id)
            .Skip((f.Pagina - 1) * f.Tamano).Take(f.Tamano)
            .ToListAsync(ct);

        return new Paginado<ReservaResponse>(
            pagina.Select(r => Mapeos.AResponse(r, r.Mentoria!)).ToList(),
            f.Pagina, f.Tamano, total, (int)Math.Ceiling(total / (double)f.Tamano));
    }

    public async Task<Resultado<ReservaResponse>> ObtenerAsync(int id, CancellationToken ct) =>
        await db.Reservas.AsNoTracking().Include(r => r.Mentoria).FirstOrDefaultAsync(r => r.Id == id, ct) is { } r
            ? Mapeos.AResponse(r, r.Mentoria!)
            : Errores.ReservaNoEncontrada(id);

    public async Task<Resultado<ReservaResponse>> ActualizarAsync(
        int id, ReservaRequest request, string? ifMatch, CancellationToken ct)
    {
        var validacion = Validadores.Reserva(request);
        if (!validacion.EsValida) return Errores.Validacion(validacion.Errores);
        var datos = validacion.Valor!;

        var reserva = await db.Reservas.Include(r => r.Bloques).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (reserva is null) return Errores.ReservaNoEncontrada(id);
        if (VerificarVersion(reserva.Version, ifMatch, requerida: true) is { } errorVersion) return errorVersion;
        if (!ReglasReserva.EsEditable(reserva.Estado)) return Errores.NoEditable(reserva.Estado);

        var mentoria = await MentoriaActivaAsync(datos.MentoriaId, ct);
        if (mentoria is null) return Errores.MentoriaInexistente;

        var ahora = reloj.GetUtcNow();
        var nuevosBloques = Agenda.BloquesDe(datos.Inicio, mentoria.DuracionMinutos);
        var (quitar, agregar) = Agenda.DiferenciaBloques(reserva.Bloques.Select(b => b.InicioUtc), nuevosBloques);
        var cambiaHorario = quitar.Length > 0 || agregar.Length > 0;

        // Si solo cambian los datos de contacto no se vuelve a exigir la anticipación mínima.
        if (cambiaHorario && Agenda.ValidarInicio(datos.Inicio, mentoria.DuracionMinutos, ahora) is { } motivo)
            return Errores.Validacion("inicio", motivo);
        if (await HayBloquesOcupadosAsync(agregar, ct)) return Errores.HorarioOcupado;

        var inicioAnterior = reserva.InicioUtc;
        db.Entry(reserva).CurrentValues.SetValues(Mapeos.Cambios(datos, mentoria, reserva.Version, ahora));
        reserva.Bloques.RemoveAll(b => quitar.Contains(b.InicioUtc));
        reserva.Bloques.AddRange(agregar.Select(b => new BloqueAgenda { InicioUtc = b }));
        if (await GuardarAsync(ct) is { } error) return error;

        var respuesta = Mapeos.AResponse(reserva, mentoria);
        await notificador.ReservaActualizadaAsync(respuesta);
        if (cambiaHorario)
            await notificador.HorariosActualizadosAsync(Mapeos.FechasAfectadas(inicioAnterior, reserva.InicioUtc));
        return respuesta;
    }

    public async Task<Resultado<ReservaResponse>> CambiarEstadoAsync(
        int id, CambioEstadoRequest request, string? ifMatch, CancellationToken ct)
    {
        var validacion = Validadores.CambioEstado(request);
        if (!validacion.EsValida) return Errores.Validacion(validacion.Errores);
        var nuevo = validacion.Valor;

        var reserva = await db.Reservas.Include(r => r.Bloques).Include(r => r.Mentoria)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (reserva is null) return Errores.ReservaNoEncontrada(id);
        if (VerificarVersion(reserva.Version, ifMatch, requerida: true) is { } errorVersion) return errorVersion;
        if (!ReglasReserva.PuedeTransicionar(reserva.Estado, nuevo)) return Errores.TransicionInvalida(reserva.Estado, nuevo);

        db.Entry(reserva).CurrentValues.SetValues(Mapeos.NuevoEstado(nuevo, reserva.Version, reloj.GetUtcNow()));
        var liberaAgenda = !ReglasReserva.OcupaAgenda(nuevo);
        if (liberaAgenda) reserva.Bloques.Clear();
        if (await GuardarAsync(ct) is { } error) return error;

        var respuesta = Mapeos.AResponse(reserva, reserva.Mentoria!);
        await notificador.ReservaActualizadaAsync(respuesta);
        if (liberaAgenda) await notificador.HorariosActualizadosAsync(Mapeos.FechasAfectadas(reserva.InicioUtc));
        return respuesta;
    }

    public async Task<Resultado<bool>> EliminarAsync(int id, string? ifMatch, CancellationToken ct)
    {
        var reserva = await db.Reservas.Include(r => r.Adjuntos).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (reserva is null) return Errores.ReservaNoEncontrada(id);
        if (VerificarVersion(reserva.Version, ifMatch, requerida: false) is { } errorVersion) return errorVersion;

        var adjuntos = reserva.Adjuntos.Select(a => a.Id).ToArray();
        db.Reservas.Remove(reserva); // bloques y adjuntos se borran en cascada
        if (await GuardarAsync(ct) is { } error) return error;

        foreach (var adjunto in adjuntos) almacen.Eliminar(adjunto);
        await notificador.ReservaEliminadaAsync(id);
        await notificador.HorariosActualizadosAsync(Mapeos.FechasAfectadas(reserva.InicioUtc));
        return true;
    }

    private static Error? VerificarVersion(int actual, string? ifMatch, bool requerida) => ifMatch switch
    {
        null or "" when requerida => Errores.VersionRequerida,
        null or "" => null,
        _ when !Etag.Coincide(ifMatch, actual) => Errores.VersionDesactualizada,
        _ => null,
    };

    private Task<Mentoria?> MentoriaActivaAsync(int id, CancellationToken ct) =>
        db.Mentorias.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id && m.Activa, ct);

    private async Task<bool> HayBloquesOcupadosAsync(DateTime[] bloques, CancellationToken ct) =>
        bloques.Length > 0 && await db.Bloques.AnyAsync(b => bloques.Contains(b.InicioUtc), ct);

    private async Task<Error?> GuardarAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return Errores.VersionDesactualizada;
        }
        catch (DbUpdateException ex) when (ErroresBaseDatos.EsClaveDuplicada(ex))
        {
            return Errores.HorarioOcupado;
        }
    }
}
