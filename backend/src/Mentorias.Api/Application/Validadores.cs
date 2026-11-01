using System.Net.Mail;
using System.Text.RegularExpressions;
using Mentorias.Api.Contracts;
using Mentorias.Api.Domain;

namespace Mentorias.Api.Application;

public sealed record FiltroReservas(
    EstadoReserva? Estado, DateOnly? Desde, DateOnly? Hasta, string? Correo, int Pagina, int Tamano);

/// <summary>Validación y normalización de entradas. Funciones puras: entrada -> Validacion.</summary>
public static partial class Validadores
{
    public const int NombreMinimo = 3, NombreMaximo = 80, CorreoMaximo = 120, MensajeMaximo = 500;
    public const int TamanoPaginaMaximo = 100;

    public static Validacion<DatosReserva> Reserva(ReservaRequest r)
    {
        var nombre = NormalizarTexto(r.Nombre);
        var correo = NormalizarCorreo(r.Correo);
        var mensaje = NormalizarTexto(r.Mensaje);
        var nivel = ParseEnum<NivelEstudiante>(r.Nivel);

        var errores = Reunir(
            ("mentoriaId", r.MentoriaId is null or <= 0 ? "Indica el id de la mentoría." : null),
            ("nombre", ValidarNombre(nombre)),
            ("correo", ValidarCorreo(correo)),
            ("nivel", nivel is null ? "El nivel debe ser 'inicial' o 'intermedio'." : null),
            ("mensaje", mensaje?.Length > MensajeMaximo ? $"El mensaje admite como máximo {MensajeMaximo} caracteres." : null),
            ("inicio", r.Inicio is null ? "Indica el inicio en formato ISO 8601, por ejemplo 2026-11-10T10:00:00-05:00." : null));

        return errores.Count > 0
            ? Validacion<DatosReserva>.Falla(errores)
            : Validacion<DatosReserva>.Ok(new DatosReserva(r.MentoriaId!.Value, nombre!, correo!, nivel!.Value, mensaje, r.Inicio!.Value));
    }

    public static Validacion<EstadoReserva> CambioEstado(CambioEstadoRequest r) =>
        ParseEnum<EstadoReserva>(r.Estado) is { } estado and not EstadoReserva.Pendiente
            ? Validacion<EstadoReserva>.Ok(estado)
            : Validacion<EstadoReserva>.Falla(Reunir(("estado", "El estado debe ser 'confirmada', 'completada' o 'cancelada'.")));

    public static Validacion<FiltroReservas> Filtro(FiltroReservasQuery q)
    {
        var estado = ParseEnum<EstadoReserva>(q.Estado);
        var errores = Reunir(
            ("estado", q.Estado is not null && estado is null ? "Estado desconocido. Usa pendiente, confirmada, completada o cancelada." : null),
            ("hasta", q.Desde > q.Hasta ? "'hasta' debe ser igual o posterior a 'desde'." : null),
            ("pagina", q.Pagina is < 1 ? "La página empieza en 1." : null),
            ("tamano", q.Tamano is < 1 or > TamanoPaginaMaximo ? $"El tamaño de página va de 1 a {TamanoPaginaMaximo}." : null));

        return errores.Count > 0
            ? Validacion<FiltroReservas>.Falla(errores)
            : Validacion<FiltroReservas>.Ok(new FiltroReservas(
                estado, q.Desde, q.Hasta, NormalizarCorreo(q.Correo), q.Pagina ?? 1, q.Tamano ?? 20));
    }

    public static Validacion<(DateOnly Fecha, int MentoriaId)> ConsultaHorarios(DateOnly? fecha, int? mentoriaId, DateOnly hoy)
    {
        var errores = Reunir(
            ("fecha", fecha is null ? "Indica la fecha (AAAA-MM-DD)."
                : fecha < hoy || fecha > hoy.AddDays(Agenda.DiasMaximos) ? $"La fecha debe estar entre hoy y {Agenda.DiasMaximos} días adelante." : null),
            ("mentoriaId", mentoriaId is null or <= 0 ? "Indica el id de la mentoría." : null));

        return errores.Count > 0
            ? Validacion<(DateOnly, int)>.Falla(errores)
            : Validacion<(DateOnly, int)>.Ok((fecha!.Value, mentoriaId!.Value));
    }

    public static Validacion<(string Correo, string Password)> Login(LoginRequest r)
    {
        var correo = NormalizarCorreo(r.Correo);
        var errores = Reunir(
            ("correo", string.IsNullOrEmpty(correo) ? "Escribe tu correo." : null),
            ("password", string.IsNullOrEmpty(r.Password) ? "Escribe tu contraseña." : null));

        return errores.Count > 0
            ? Validacion<(string, string)>.Falla(errores)
            : Validacion<(string, string)>.Ok((correo!, r.Password!));
    }

    public static string? NormalizarTexto(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : EspaciosRepetidos().Replace(texto.Trim(), " ");

    public static string? NormalizarCorreo(string? correo) =>
        string.IsNullOrWhiteSpace(correo) ? null : correo.Trim().ToLowerInvariant();

    /// <summary>Acepta el nombre del valor sin distinguir mayúsculas; rechaza números ("1").</summary>
    public static T? ParseEnum<T>(string? texto) where T : struct, Enum =>
        Enum.GetValues<T>().Cast<T?>()
            .FirstOrDefault(v => string.Equals(v.ToString(), texto?.Trim(), StringComparison.OrdinalIgnoreCase));

    private static string? ValidarNombre(string? nombre) => nombre switch
    {
        null => "Escribe tu nombre completo.",
        { Length: < NombreMinimo or > NombreMaximo } => $"El nombre debe tener entre {NombreMinimo} y {NombreMaximo} caracteres.",
        _ when !NombreValido().IsMatch(nombre) => "El nombre solo puede contener letras, espacios, apóstrofos, puntos y guiones.",
        _ => null,
    };

    private static string? ValidarCorreo(string? correo) => correo switch
    {
        null => "Escribe tu correo electrónico.",
        { Length: > CorreoMaximo } => $"El correo admite como máximo {CorreoMaximo} caracteres.",
        _ when !CorreoValido().IsMatch(correo) || !MailAddress.TryCreate(correo, out _) => "Escribe un correo válido, por ejemplo nombre@dominio.com.",
        _ => null,
    };

    private static IReadOnlyDictionary<string, string[]> Reunir(params (string Campo, string? Mensaje)[] reglas) =>
        reglas.Where(r => r.Mensaje is not null).ToDictionary(r => r.Campo, r => new[] { r.Mensaje! });

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspaciosRepetidos();

    [GeneratedRegex(@"^\p{L}[\p{L} '.\-]*$")]
    private static partial Regex NombreValido();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$")]
    private static partial Regex CorreoValido();
}
