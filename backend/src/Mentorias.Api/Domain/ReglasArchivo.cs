using System.Text;

namespace Mentorias.Api.Domain;

public enum MotivoRechazo { Vacio, MuyGrande, ExtensionNoPermitida, ContenidoNoCoincide, LimiteAlcanzado }

public sealed record EvaluacionArchivo(string NombreSeguro, string? TipoContenido, MotivoRechazo? Rechazo, string? Mensaje)
{
    public bool Aceptado => Rechazo is null;
}

/// <summary>Reglas para adjuntos: lista blanca de extensiones y comprobación de la firma real del contenido.</summary>
public static class ReglasArchivo
{
    public const long TamanoMaximo = 2 * 1024 * 1024;
    public const int MaximoPorReserva = 5;
    public const int BytesCabecera = 512;

    private sealed record TipoPermitido(string Mime, Func<byte[], bool> FirmaValida);

    private static readonly TipoPermitido Texto = new("text/plain; charset=utf-8", EsTexto);

    private static readonly IReadOnlyDictionary<string, TipoPermitido> Permitidos =
        new Dictionary<string, TipoPermitido>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = new("application/pdf", c => EmpiezaCon(c, "%PDF-"u8)),
            [".png"] = new("image/png", c => EmpiezaCon(c, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])),
            [".jpg"] = new("image/jpeg", c => EmpiezaCon(c, [0xFF, 0xD8, 0xFF])),
            [".jpeg"] = new("image/jpeg", c => EmpiezaCon(c, [0xFF, 0xD8, 0xFF])),
            [".zip"] = new("application/zip", c => EmpiezaCon(c, [0x50, 0x4B, 0x03, 0x04])),
            // Código fuente: se sirve siempre como texto plano y como descarga.
            [".txt"] = Texto, [".md"] = Texto, [".java"] = Texto, [".cs"] = Texto,
            [".js"] = Texto, [".py"] = Texto, [".sql"] = Texto, [".html"] = Texto, [".css"] = Texto,
        };

    public static IEnumerable<string> Extensiones => Permitidos.Keys.Order();

    public static EvaluacionArchivo Evaluar(string nombre, long tamano, byte[] cabecera, int adjuntosExistentes)
    {
        var seguro = NombreSeguro(nombre);
        var extension = Path.GetExtension(seguro);

        if (adjuntosExistentes >= MaximoPorReserva)
            return Rechazar(seguro, MotivoRechazo.LimiteAlcanzado, $"La reserva ya tiene {MaximoPorReserva} adjuntos; elimina uno antes de subir otro.");
        if (tamano == 0)
            return Rechazar(seguro, MotivoRechazo.Vacio, "El archivo está vacío.");
        if (tamano > TamanoMaximo)
            return Rechazar(seguro, MotivoRechazo.MuyGrande, $"El archivo pesa {tamano / 1024} KB; el máximo es {TamanoMaximo / 1024 / 1024} MB.");
        if (!Permitidos.TryGetValue(extension, out var tipo))
            return Rechazar(seguro, MotivoRechazo.ExtensionNoPermitida, $"Extensión no permitida. Usa: {string.Join(", ", Extensiones)}.");
        if (!tipo.FirmaValida(cabecera))
            return Rechazar(seguro, MotivoRechazo.ContenidoNoCoincide, $"El contenido no corresponde a un archivo {extension}.");

        return new EvaluacionArchivo(seguro, tipo.Mime, null, null);
    }

    /// <summary>Quita rutas y caracteres peligrosos del nombre que envía el cliente.</summary>
    public static string NombreSeguro(string? nombre)
    {
        var soloNombre = Path.GetFileName((nombre ?? "").Replace('\\', '/').Split('/')[^1]).Trim();
        var limpio = new string(soloNombre
            .Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or ' ' ? c : '_')
            .ToArray()).Trim('.', ' ');
        var recortado = limpio.Length > 100
            ? limpio[..(100 - Path.GetExtension(limpio).Length)] + Path.GetExtension(limpio)
            : limpio;
        return string.IsNullOrEmpty(recortado) ? "archivo" : recortado;
    }

    private static EvaluacionArchivo Rechazar(string nombre, MotivoRechazo motivo, string mensaje) =>
        new(nombre, null, motivo, mensaje);

    private static bool EmpiezaCon(byte[] contenido, ReadOnlySpan<byte> firma) =>
        contenido.AsSpan().StartsWith(firma);

    private static bool EsTexto(byte[] contenido)
    {
        if (contenido.Contains((byte)0)) return false;
        try
        {
            // La cabecera puede cortar un carácter multibyte al final; se ignoran los últimos 3 bytes.
            var muestra = contenido.Length > 3 ? contenido[..^3] : contenido;
            new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(muestra);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
