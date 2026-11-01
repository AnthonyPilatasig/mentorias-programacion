using System.Diagnostics;
using Mentorias.Api.Application;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Mentorias.Api.Endpoints;

/// <summary>
/// Formato único de error (RFC 9457, application/problem+json) para toda la API:
/// type, title, status, detail, instance, codigo, traceId y, en validaciones, errors por campo.
/// </summary>
public static class ProblemasHttp
{
    private static readonly IReadOnlyDictionary<int, (string Titulo, string Codigo)> PorEstado =
        new Dictionary<int, (string, string)>
        {
            [400] = ("Petición inválida", "peticion_invalida"),
            [401] = ("No autenticado", "no_autenticado"),
            [403] = ("Acceso denegado", "acceso_denegado"),
            [404] = ("Recurso no encontrado", "no_encontrado"),
            [405] = ("Método no permitido", "metodo_no_permitido"),
            [409] = ("Conflicto con el estado actual", "conflicto"),
            [412] = ("Precondición fallida", "precondicion_fallida"),
            [413] = ("Contenido demasiado grande", "contenido_muy_grande"),
            [415] = ("Tipo de contenido no soportado", "tipo_no_soportado"),
            [428] = ("Precondición requerida", "precondicion_requerida"),
            [429] = ("Demasiadas peticiones", "demasiadas_peticiones"),
            [500] = ("Error interno del servidor", "error_interno"),
        };

    private static readonly HashSet<string> TitulosPorDefecto =
        ["One or more validation errors occurred.", "An error occurred while processing your request."];

    /// <summary>Se aplica a todo ProblemDetails que genere ASP.NET Core o la propia API.</summary>
    public static void Personalizar(ProblemDetailsContext ctx)
    {
        var p = ctx.ProblemDetails;
        var status = p.Status ?? ctx.HttpContext.Response.StatusCode;
        p.Status = status;

        if (PorEstado.TryGetValue(status, out var info))
        {
            if (p.Title is null || TitulosPorDefecto.Contains(p.Title) || p.Title == ReasonPhrases.GetReasonPhrase(status))
                p.Title = info.Titulo;
            p.Extensions.TryAdd("codigo", info.Codigo);
        }
        // ASP.NET Core no asigna "type" a 428 y 429 (definidos en RFC 6585).
        p.Type ??= status is 428 or 429 ? $"https://www.rfc-editor.org/rfc/rfc6585#section-{(status == 428 ? 3 : 4)}" : null;
        p.Instance ??= ctx.HttpContext.Request.Path;
        p.Extensions["traceId"] = Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier;
    }

    public static int Estado(TipoError tipo) => tipo switch
    {
        TipoError.Validacion => StatusCodes.Status400BadRequest,
        TipoError.NoAutorizado => StatusCodes.Status401Unauthorized,
        TipoError.NoEncontrado => StatusCodes.Status404NotFound,
        TipoError.Conflicto => StatusCodes.Status409Conflict,
        TipoError.PrecondicionFallida => StatusCodes.Status412PreconditionFailed,
        TipoError.ArchivoMuyGrande => StatusCodes.Status413PayloadTooLarge,
        TipoError.TipoNoSoportado => StatusCodes.Status415UnsupportedMediaType,
        TipoError.PrecondicionRequerida => StatusCodes.Status428PreconditionRequired,
        TipoError.DemasiadosIntentos => StatusCodes.Status429TooManyRequests,
        _ => StatusCodes.Status500InternalServerError,
    };

    public static IResult Desde(Error e, HttpContext ctx)
    {
        if (e.ReintentarEn is { } espera)
            ctx.Response.Headers.RetryAfter = Math.Ceiling(espera.TotalSeconds).ToString();

        var extensiones = new Dictionary<string, object?> { ["codigo"] = e.Codigo };
        return e.Tipo == TipoError.Validacion
            ? TypedResults.ValidationProblem(e.Campos ?? new Dictionary<string, string[]>(), e.Mensaje, extensions: extensiones)
            : TypedResults.Problem(e.Mensaje, statusCode: Estado(e.Tipo), extensions: extensiones);
    }

    /// <summary>Traduce un Resultado: error -> ProblemDetails; éxito -> la respuesta indicada.</summary>
    public static IResult Responder<T>(this Resultado<T> resultado, HttpContext ctx, Func<T, IResult> exito) =>
        resultado.Error is { } error ? Desde(error, ctx) : exito(resultado.Valor!);

    public static async ValueTask EscribirAsync(HttpContext ctx, int status, string codigo, string detalle)
    {
        ctx.Response.StatusCode = status;
        await ctx.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = ctx,
            ProblemDetails = new ProblemDetails { Status = status, Detail = detalle, Extensions = { ["codigo"] = codigo } },
        });
    }
}

/// <summary>Convierte excepciones en ProblemDetails sin filtrar detalles internos al cliente.</summary>
public sealed class ManejadorExcepciones(ILogger<ManejadorExcepciones> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (status, codigo, detalle) = ex switch
        {
            BadHttpRequestException { StatusCode: 413 } =>
                (413, "contenido_muy_grande", "El cuerpo de la petición supera el tamaño permitido."),
            BadHttpRequestException { StatusCode: 415 } =>
                (415, "tipo_no_soportado", "Tipo de contenido no soportado. Usa application/json o multipart/form-data según el endpoint."),
            BadHttpRequestException b =>
                (b.StatusCode, "peticion_invalida", "No se pudo leer la petición: revisa que el JSON esté bien formado y que los tipos de datos sean correctos."),
            _ => (500, "error_interno", "Ocurrió un error inesperado. Comparte el traceId si necesitas reportarlo."),
        };

        if (status >= 500) logger.LogError(ex, "Error no controlado");
        else logger.LogInformation("Petición rechazada: {Mensaje}", ex.Message);

        await ProblemasHttp.EscribirAsync(ctx, status, codigo, detalle);
        return true;
    }
}
