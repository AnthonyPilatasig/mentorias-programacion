using Mentorias.Api.Application;
using Mentorias.Api.Contracts;

namespace Mentorias.Api.Endpoints;

public static class CatalogoEndpoints
{
    public static IEndpointRouteBuilder MapCatalogoEndpoints(this IEndpointRouteBuilder app)
    {
        var mentorias = app.MapGroup("/api/mentorias").WithTags("Mentorías");

        mentorias.MapGet("/", async (string? categoria, CatalogoService servicio, CancellationToken ct) =>
                TypedResults.Ok(await servicio.ListarAsync(categoria, ct)))
            .WithSummary("Lista las mentorías activas")
            .WithDescription("Filtro opcional: ?categoria=fundamentos|java|web|datos");

        mentorias.MapGet("/{id:int}", async (int id, CatalogoService servicio, HttpContext ctx, CancellationToken ct) =>
                (await servicio.ObtenerAsync(id, ct)).Responder(ctx, TypedResults.Ok))
            .WithSummary("Obtiene una mentoría")
            .Produces<MentoriaResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/horarios", async (DateOnly? fecha, int? mentoriaId, CatalogoService servicio, HttpContext ctx, CancellationToken ct) =>
                (await servicio.HorariosAsync(fecha, mentoriaId, ct)).Responder(ctx, TypedResults.Ok))
            .WithTags("Horarios")
            .WithSummary("Horarios de un día para una mentoría")
            .WithDescription("Ejemplo: /api/horarios?fecha=2026-11-10&mentoriaId=3. Hora de Ecuador (UTC-5).")
            .Produces<HorariosResponse>()
            .ProducesValidationProblem();

        return app;
    }
}
