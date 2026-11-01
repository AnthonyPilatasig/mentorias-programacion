using System.Security.Claims;
using Mentorias.Api.Contracts;
using Mentorias.Api.Domain;

namespace Mentorias.Api.Application;

/// <summary>Cambios que se aplican a una reserva existente (EF los copia con SetValues).</summary>
public sealed record CambiosReserva(
    int MentoriaId, string Nombre, string Correo, NivelEstudiante Nivel, string? Mensaje,
    DateTime InicioUtc, DateTime FinUtc, int Version, DateTime ActualizadaUtc);

public sealed record CambioEstado(EstadoReserva Estado, int Version, DateTime ActualizadaUtc);

/// <summary>Transformaciones puras entre entidades, datos validados y contratos.</summary>
public static class Mapeos
{
    public const string RolMentor = "mentor";

    /// <summary>Recorta a milisegundos: MySQL guarda microsegundos y así el valor no cambia al releerlo.</summary>
    public static DateTime Instante(DateTimeOffset ahora) =>
        new(ahora.UtcTicks - ahora.UtcTicks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);

    public static Reserva NuevaReserva(DatosReserva d, Mentoria m, DateTime[] bloques, DateTimeOffset ahora) => new()
    {
        MentoriaId = m.Id,
        Nombre = d.Nombre,
        Correo = d.Correo,
        Nivel = d.Nivel,
        Mensaje = d.Mensaje,
        InicioUtc = d.Inicio.UtcDateTime,
        FinUtc = d.Inicio.UtcDateTime.AddMinutes(m.DuracionMinutos),
        Estado = EstadoReserva.Pendiente,
        Version = 1,
        CreadaUtc = Instante(ahora),
        ActualizadaUtc = Instante(ahora),
        Bloques = bloques.Select(b => new BloqueAgenda { InicioUtc = b }).ToList(),
    };

    public static CambiosReserva Cambios(DatosReserva d, Mentoria m, int versionActual, DateTimeOffset ahora) => new(
        m.Id, d.Nombre, d.Correo, d.Nivel, d.Mensaje,
        d.Inicio.UtcDateTime, d.Inicio.UtcDateTime.AddMinutes(m.DuracionMinutos),
        versionActual + 1, Instante(ahora));

    public static CambioEstado NuevoEstado(EstadoReserva estado, int versionActual, DateTimeOffset ahora) =>
        new(estado, versionActual + 1, Instante(ahora));

    public static ReservaResponse AResponse(Reserva r, Mentoria m) => new(
        r.Id,
        new MentoriaResumen(m.Id, m.Titulo, m.DuracionMinutos),
        r.Nombre, r.Correo, r.Nivel, r.Mensaje,
        Agenda.ALocal(r.InicioUtc), Agenda.ALocal(r.FinUtc),
        r.Estado, r.Version, r.CreadaUtc, r.ActualizadaUtc);

    public static MentoriaResponse AResponse(Mentoria m) =>
        new(m.Id, m.Slug, m.Titulo, m.Descripcion, m.Categoria, m.DuracionMinutos);

    public static AdjuntoResponse AResponse(Adjunto a) =>
        new(a.Id, a.ReservaId, a.NombreOriginal, a.TipoContenido, a.TamanoBytes, a.Sha256, a.CreadoUtc);

    public static Adjunto NuevoAdjunto(Guid id, int reservaId, EvaluacionArchivo e, long tamano, string sha256, DateTimeOffset ahora) => new()
    {
        Id = id,
        ReservaId = reservaId,
        NombreOriginal = e.NombreSeguro,
        TipoContenido = e.TipoContenido!,
        TamanoBytes = tamano,
        Sha256 = sha256,
        CreadoUtc = Instante(ahora),
    };

    public static HorariosResponse AResponse(DateOnly fecha, Mentoria m, IEnumerable<Horario> horarios) => new(
        fecha, m.Id, m.DuracionMinutos, Agenda.ZonaHoraria,
        horarios.Select(h => new HorarioResponse(h.Inicio, h.Fin, h.Disponible)).ToList());

    public static ClaimsPrincipal Principal(Mentor m, string esquema) => new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, m.Id.ToString()),
            new Claim(ClaimTypes.Name, m.Nombre),
            new Claim(ClaimTypes.Email, m.Correo),
            new Claim(ClaimTypes.Role, RolMentor),
        ],
        esquema));

    public static SesionResponse Sesion(Mentor m) => new(m.Id, m.Nombre, m.Correo);

    public static SesionResponse Sesion(ClaimsPrincipal u) => new(
        int.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!),
        u.FindFirstValue(ClaimTypes.Name)!,
        u.FindFirstValue(ClaimTypes.Email)!);

    /// <summary>Fechas locales cuyo listado de horarios cambia al mover/cancelar una reserva.</summary>
    public static DateOnly[] FechasAfectadas(params DateTime[] iniciosUtc) =>
        iniciosUtc.Select(i => Agenda.FechaLocal(Agenda.ALocal(i))).Distinct().ToArray();
}

/// <summary>Conversión entre la versión de una reserva y su ETag HTTP.</summary>
public static class Etag
{
    public static string De(int version) => $"\"{version}\"";

    /// <summary>true si el encabezado If-Match coincide con la versión ("*" coincide siempre).</summary>
    public static bool Coincide(string ifMatch, int version) =>
        ifMatch.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(v => v == "*" || v.Replace("W/", "").Trim('"') == version.ToString());
}
