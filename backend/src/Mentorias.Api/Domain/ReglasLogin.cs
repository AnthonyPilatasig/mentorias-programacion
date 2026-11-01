namespace Mentorias.Api.Domain;

public sealed record IntentosLogin(int Fallos, DateTimeOffset? BloqueadoHasta);

/// <summary>Bloqueo temporal tras varios intentos fallidos (protección contra fuerza bruta).</summary>
public static class ReglasLogin
{
    public const int MaximoFallos = 5;
    public static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(15);

    public static TimeSpan? TiempoRestante(IntentosLogin? intentos, DateTimeOffset ahora) =>
        intentos?.BloqueadoHasta is { } hasta && hasta > ahora ? hasta - ahora : null;

    public static IntentosLogin RegistrarFallo(IntentosLogin? actual, DateTimeOffset ahora)
    {
        var fallos = (actual?.Fallos ?? 0) + 1;
        return new IntentosLogin(fallos, fallos >= MaximoFallos ? ahora + DuracionBloqueo : null);
    }
}
