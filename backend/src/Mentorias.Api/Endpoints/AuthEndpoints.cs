using Mentorias.Api.Application;
using Mentorias.Api.Configuracion;
using Mentorias.Api.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Mentorias.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Sesión");

        auth.MapPost("/login", Login)
            .RequireRateLimiting(Politicas.LimiteLogin)
            .WithSummary("Inicia sesión del mentor")
            .WithDescription("Crea una sesión de servidor y devuelve la cookie HttpOnly 'mentorias.sesion'. Tras 5 fallos el correo se bloquea 15 minutos.")
            .Produces<SesionResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        auth.MapPost("/logout", async (HttpContext ctx) =>
            {
                await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return TypedResults.NoContent();
            })
            .WithSummary("Cierra la sesión (borra la sesión del servidor)")
            .Produces(StatusCodes.Status204NoContent);

        auth.MapGet("/me", (HttpContext ctx) => TypedResults.Ok(Mapeos.Sesion(ctx.User)))
            .RequireAuthorization(Politicas.Mentor)
            .WithSummary("Datos de la sesión actual")
            .Produces<SesionResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> Login(LoginRequest request, AuthService auth, HttpContext ctx, CancellationToken ct)
    {
        var resultado = await auth.ValidarCredencialesAsync(request, ct);
        if (resultado.Error is { } error) return ProblemasHttp.Desde(error, ctx);

        var mentor = resultado.Valor!;
        await ctx.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            Mapeos.Principal(mentor, CookieAuthenticationDefaults.AuthenticationScheme),
            new AuthenticationProperties { IsPersistent = false });
        return TypedResults.Ok(Mapeos.Sesion(mentor));
    }
}
