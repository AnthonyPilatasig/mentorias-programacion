using Mentorias.Api.Application;
using Mentorias.Api.Configuracion;
using Mentorias.Api.Contracts;
using Mentorias.Api.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Mentorias.Api.Endpoints;

public static class AdjuntoEndpoints
{
    public static IEndpointRouteBuilder MapAdjuntoEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api").WithTags("Adjuntos")
            .RequireAuthorization(Politicas.Mentor)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        grupo.MapPost("/reservas/{id:int}/adjuntos", Subir)
            // La cookie de sesión es SameSite=Strict: otro sitio no puede enviar este formulario
            // con la sesión del mentor, por eso no se exige token antiforgery.
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(ReglasArchivo.TamanoMaximo + 64 * 1024))
            .WithSummary("Sube un adjunto (multipart/form-data, campo 'archivo', máx. 2 MB)")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<AdjuntoResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        grupo.MapGet("/reservas/{id:int}/adjuntos", async (int id, AdjuntoService servicio, HttpContext ctx, CancellationToken ct) =>
                (await servicio.ListarAsync(id, ct)).Responder(ctx, TypedResults.Ok))
            .WithSummary("Lista los adjuntos de una reserva")
            .Produces<IReadOnlyList<AdjuntoResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapGet("/adjuntos/{id:guid}", Descargar)
            .WithSummary("Descarga un adjunto")
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapDelete("/adjuntos/{id:guid}", async (Guid id, AdjuntoService servicio, HttpContext ctx, CancellationToken ct) =>
                (await servicio.EliminarAsync(id, ct)).Responder(ctx, _ => TypedResults.NoContent()))
            .WithSummary("Elimina un adjunto")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> Subir(int id, IFormFile? archivo, AdjuntoService servicio, HttpContext ctx, CancellationToken ct) =>
        (await servicio.SubirAsync(id, archivo, ct)).Responder(ctx, a =>
            TypedResults.Created($"/api/adjuntos/{a.Id}", a));

    private static async Task<IResult> Descargar(Guid id, AdjuntoService servicio, HttpContext ctx, CancellationToken ct) =>
        (await servicio.DescargarAsync(id, ct)).Responder(ctx, d =>
        {
            ctx.Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
            // Siempre como descarga, nunca se muestra dentro de la página.
            return TypedResults.File(d.Contenido, d.Adjunto.TipoContenido, d.Adjunto.Nombre);
        });
}
