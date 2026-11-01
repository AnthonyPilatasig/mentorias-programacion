using Mentorias.Api.Domain;

namespace Mentorias.Api.Application;

public enum TipoError
{
    Validacion, NoAutorizado, NoEncontrado, Conflicto, PrecondicionFallida,
    ArchivoMuyGrande, TipoNoSoportado, PrecondicionRequerida, DemasiadosIntentos,
}

/// <summary>Error de aplicación; la capa HTTP lo traduce a ProblemDetails.</summary>
public sealed record Error(
    TipoError Tipo,
    string Codigo,
    string Mensaje,
    IReadOnlyDictionary<string, string[]>? Campos = null,
    TimeSpan? ReintentarEn = null);

/// <summary>Valor o error: los servicios no lanzan excepciones para casos esperados.</summary>
public sealed record Resultado<T>(T? Valor, Error? Error)
{
    public static implicit operator Resultado<T>(T valor) => new(valor, null);
    public static implicit operator Resultado<T>(Error error) => new(default, error);
}

public sealed record Validacion<T>(T? Valor, IReadOnlyDictionary<string, string[]> Errores)
{
    public bool EsValida => Errores.Count == 0;
    public static Validacion<T> Ok(T valor) => new(valor, new Dictionary<string, string[]>());
    public static Validacion<T> Falla(IReadOnlyDictionary<string, string[]> errores) => new(default, errores);
}

/// <summary>Catálogo de errores con mensajes y códigos estables para el cliente.</summary>
public static class Errores
{
    public static Error Validacion(IReadOnlyDictionary<string, string[]> campos) =>
        new(TipoError.Validacion, "validacion", "Uno o más campos no son válidos.", campos);

    public static Error Validacion(string campo, string mensaje) =>
        Validacion(new Dictionary<string, string[]> { [campo] = [mensaje] });

    public static readonly Error MentoriaInexistente =
        Validacion("mentoriaId", "No existe una mentoría activa con ese id.");

    public static Error MentoriaNoEncontrada(int id) =>
        new(TipoError.NoEncontrado, "mentoria_no_encontrada", $"No existe la mentoría {id}.");

    public static Error ReservaNoEncontrada(int id) =>
        new(TipoError.NoEncontrado, "reserva_no_encontrada", $"No existe la reserva {id}.");

    public static Error AdjuntoNoEncontrado(Guid id) =>
        new(TipoError.NoEncontrado, "adjunto_no_encontrado", $"No existe el adjunto {id}.");

    public static readonly Error HorarioOcupado = new(TipoError.Conflicto, "horario_ocupado",
        "El horario elegido se cruza con otra reserva. Consulta GET /api/horarios para ver los disponibles.");

    public static Error NoEditable(EstadoReserva estado) => new(TipoError.Conflicto, "reserva_no_editable",
        $"Una reserva en estado '{estado.ToString().ToLowerInvariant()}' ya no se puede modificar.");

    public static Error TransicionInvalida(EstadoReserva actual, EstadoReserva nuevo) =>
        new(TipoError.Conflicto, "transicion_invalida",
            $"No se puede pasar de '{actual.ToString().ToLowerInvariant()}' a '{nuevo.ToString().ToLowerInvariant()}'.");

    public static readonly Error VersionRequerida = new(TipoError.PrecondicionRequerida, "version_requerida",
        "Envía el encabezado If-Match con el ETag de la reserva (lo devuelve GET /api/reservas/{id}).");

    public static readonly Error VersionDesactualizada = new(TipoError.PrecondicionFallida, "version_desactualizada",
        "La reserva cambió desde que la leíste. Vuelve a consultarla y repite la operación.");

    public static readonly Error CredencialesInvalidas =
        new(TipoError.NoAutorizado, "credenciales_invalidas", "Correo o contraseña incorrectos.");

    public static Error CuentaBloqueada(TimeSpan restante) => new(TipoError.DemasiadosIntentos, "cuenta_bloqueada",
        $"Demasiados intentos fallidos. Intenta de nuevo en {Math.Ceiling(restante.TotalMinutes)} minutos.",
        ReintentarEn: restante);

    public static readonly Error ArchivoRequerido = Validacion("archivo",
        "Adjunta un archivo en el campo 'archivo' de un formulario multipart/form-data.");

    public static Error ArchivoRechazado(MotivoRechazo motivo, string mensaje) => motivo switch
    {
        MotivoRechazo.MuyGrande => new(TipoError.ArchivoMuyGrande, "archivo_muy_grande", mensaje),
        MotivoRechazo.ExtensionNoPermitida => new(TipoError.TipoNoSoportado, "tipo_no_permitido", mensaje),
        MotivoRechazo.ContenidoNoCoincide => new(TipoError.TipoNoSoportado, "contenido_no_coincide", mensaje),
        MotivoRechazo.LimiteAlcanzado => new(TipoError.Conflicto, "limite_adjuntos", mensaje),
        _ => Validacion("archivo", mensaje),
    };
}
