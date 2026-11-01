using Mentorias.Api.Application;
using Mentorias.Api.Configuracion;
using Mentorias.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Mentorias.Api.Endpoints;

public static class ReservaEndpoints
{
    public static IEndpointRouteBuilder MapReservaEndpoints(this IEndpointRouteBuilder app)
    {
        var reservas = app.MapGroup("/api/reservas").WithTags("Reservas");

        reservas.MapPost("/", Crear)
            .RequireRateLimiting(Politicas.LimiteReservas)
            .WithSummary("Crea una reserva (público)")
            .WithDescription("Devuelve 201 con Location y ETag. 409 si el horario se cruza con otra reserva.")
            .Produces<ReservaResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        var mentor = reservas.MapGroup("").RequireAuthorization(Politicas.Mentor)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        mentor.MapGet("/", Listar)
            .WithSummary("Lista reservas con filtros y paginación (mentor)")
            .Produces<Paginado<ReservaResponse>>()
            .ProducesValidationProblem();

        mentor.MapGet("/{id:int}", Obtener)
            .WithSummary("Obtiene una reserva (mentor)")
            .Produces<ReservaResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        mentor.MapPut("/{id:int}", Actualizar)
            .WithSummary("Reemplaza los datos de una reserva (mentor, requiere If-Match)")
            .Produces<ReservaResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        mentor.MapPatch("/{id:int}/estado", CambiarEstado)
            .WithSummary("Confirma, completa o cancela una reserva (mentor, requiere If-Match)")
            .Produces<ReservaResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        mentor.MapDelete("/{id:int}", Eliminar)
            .WithSummary("Elimina una reserva y sus adjuntos (mentor; If-Match opcional)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        return app;
    }

    private static async Task<IResult> Crear(ReservaRequest request, ReservaService servicio, HttpContext ctx, CancellationToken ct) =>
        (await servicio.CrearAsync(request, ct)).Responder(ctx, r =>
            ConEtag(ctx, r, TypedResults.Created($"/api/reservas/{r.Id}", r)));

    private static async Task<IResult> Listar([AsParameters] FiltroReservasQuery filtro, ReservaService servicio, HttpContext ctx, CancellationToken ct) =>
        (await servicio.ListarAsync(filtro, ct)).Responder(ctx, TypedResults.Ok);

    private static async Task<IResult> Obtener(int id, ReservaService servicio, HttpContext ctx, CancellationToken ct) =>
        (await servicio.ObtenerAsync(id, ct)).Responder(ctx, r => ConEtag(ctx, r, TypedResults.Ok(r)));

    private static async Task<IResult> Actualizar(
        int id, ReservaRequest request, [FromHeader(Name = "If-Match")] string? ifMatch,
        ReservaService servicio, HttpContext ctx, CancellationToken ct) =>
        (await servicio.ActualizarAsync(id, request, ifMatch, ct)).Responder(ctx, r => ConEtag(ctx, r, TypedResults.Ok(r)));

    private static async Task<IResult> CambiarEstado(
        int id, CambioEstadoRequest request, [FromHeader(Name = "If-Match")] string? ifMatch,
        ReservaService servicio, HttpContext ctx, CancellationToken ct) =>
        (await servicio.CambiarEstadoAsync(id, request, ifMatch, ct)).Responder(ctx, r => ConEtag(ctx, r, TypedResults.Ok(r)));

    private static async Task<IResult> Eliminar(
        int id, [FromHeader(Name = "If-Match")] string? ifMatch, ReservaService servicio, HttpContext ctx, CancellationToken ct) =>
        (await servicio.EliminarAsync(id, ifMatch, ct)).Responder(ctx, _ => TypedResults.NoContent());

    private static IResult ConEtag(HttpContext ctx, ReservaResponse reserva, IResult resultado)
    {
        ctx.Response.Headers.ETag = Etag.De(reserva.Version);
        return resultado;
    }
}
