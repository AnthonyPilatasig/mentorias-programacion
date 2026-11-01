using Mentorias.Api.Contracts;
using Mentorias.Api.Domain;
using Mentorias.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Mentorias.Api.Application;

public sealed class CatalogoService(MentoriasDbContext db, TimeProvider reloj)
{
    public async Task<IReadOnlyList<MentoriaResponse>> ListarAsync(string? categoria, CancellationToken ct)
    {
        var filtro = Validadores.NormalizarTexto(categoria)?.ToLowerInvariant();
        var mentorias = await db.Mentorias.AsNoTracking()
            .Where(m => m.Activa && (filtro == null || m.Categoria == filtro))
            .OrderBy(m => m.Id)
            .ToListAsync(ct);
        return mentorias.Select(Mapeos.AResponse).ToList();
    }

    public async Task<Resultado<MentoriaResponse>> ObtenerAsync(int id, CancellationToken ct) =>
        await db.Mentorias.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id && m.Activa, ct) is { } m
            ? Mapeos.AResponse(m)
            : Errores.MentoriaNoEncontrada(id);

    public async Task<Resultado<HorariosResponse>> HorariosAsync(DateOnly? fecha, int? mentoriaId, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var consulta = Validadores.ConsultaHorarios(fecha, mentoriaId, Agenda.FechaLocal(ahora));
        if (!consulta.EsValida) return Errores.Validacion(consulta.Errores);
        var (dia, idMentoria) = consulta.Valor;

        var mentoria = await db.Mentorias.AsNoTracking().FirstOrDefaultAsync(m => m.Id == idMentoria && m.Activa, ct);
        if (mentoria is null) return Errores.MentoriaInexistente;

        var (desde, hasta) = Agenda.RangoUtcDelDia(dia);
        var ocupados = await db.Bloques.AsNoTracking()
            .Where(b => b.InicioUtc >= desde && b.InicioUtc < hasta)
            .Select(b => b.InicioUtc)
            .ToListAsync(ct);

        return Mapeos.AResponse(dia, mentoria, Agenda.Horarios(dia, mentoria.DuracionMinutos, ocupados.ToHashSet(), ahora));
    }
}
