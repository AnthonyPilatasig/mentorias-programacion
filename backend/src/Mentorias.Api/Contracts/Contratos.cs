using Mentorias.Api.Domain;

namespace Mentorias.Api.Contracts;

// Peticiones: todos los campos son anulables para que la validación pueda
// informar de cada campo faltante en lugar de fallar al deserializar.

/// <summary>Datos para crear o reemplazar (PUT) una reserva.</summary>
/// <param name="MentoriaId">Id de la mentoría (GET /api/mentorias).</param>
/// <param name="Nombre">Nombre completo del estudiante (3 a 80 caracteres).</param>
/// <param name="Correo">Correo de contacto.</param>
/// <param name="Nivel">"inicial" o "intermedio".</param>
/// <param name="Mensaje">Qué quiere lograr el estudiante (opcional, máx. 500).</param>
/// <param name="Inicio">Fecha y hora ISO 8601 con zona, p. ej. 2026-11-10T10:00:00-05:00.</param>
public sealed record ReservaRequest(
    int? MentoriaId, string? Nombre, string? Correo, string? Nivel, string? Mensaje, DateTimeOffset? Inicio);

/// <param name="Estado">"confirmada", "completada" o "cancelada".</param>
public sealed record CambioEstadoRequest(string? Estado);

public sealed record LoginRequest(string? Correo, string? Password);

public sealed record FiltroReservasQuery(
    string? Estado, DateOnly? Desde, DateOnly? Hasta, string? Correo, int? Pagina, int? Tamano);

// Respuestas

public sealed record MentoriaResponse(
    int Id, string Slug, string Titulo, string Descripcion, string Categoria, int DuracionMinutos);

public sealed record MentoriaResumen(int Id, string Titulo, int DuracionMinutos);

public sealed record ReservaResponse(
    int Id,
    MentoriaResumen Mentoria,
    string Nombre,
    string Correo,
    NivelEstudiante Nivel,
    string? Mensaje,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    EstadoReserva Estado,
    int Version,
    DateTime CreadaUtc,
    DateTime ActualizadaUtc);

public sealed record AdjuntoResponse(
    Guid Id, int ReservaId, string Nombre, string TipoContenido, long TamanoBytes, string Sha256, DateTime CreadoUtc);

public sealed record HorarioResponse(DateTimeOffset Inicio, DateTimeOffset Fin, bool Disponible);

public sealed record HorariosResponse(
    DateOnly Fecha, int MentoriaId, int DuracionMinutos, string ZonaHoraria, IReadOnlyList<HorarioResponse> Horarios);

public sealed record Paginado<T>(IReadOnlyList<T> Items, int Pagina, int Tamano, int Total, int TotalPaginas);

public sealed record SesionResponse(int Id, string Nombre, string Correo);
