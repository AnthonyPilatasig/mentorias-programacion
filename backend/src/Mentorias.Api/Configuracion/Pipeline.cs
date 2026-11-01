using Mentorias.Api.Endpoints;
using Mentorias.Api.Infrastructure;
using Mentorias.Api.Realtime;
using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;

namespace Mentorias.Api.Configuracion;

public static class Pipeline
{
    public static WebApplication UseMentoriasApi(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages(); // 401, 404, 405... sin cuerpo -> ProblemDetails
        app.Use(EncabezadosSeguridad);
        UseFrontend(app);
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        app.MapOpenApi();
        app.MapScalarApiReference("/docs", o => o.WithTitle("API de Mentorías de código"));
        app.MapGet("/health", async (MentoriasDbContext db, CancellationToken ct) =>
                await db.Database.CanConnectAsync(ct)
                    ? Results.Ok(new { estado = "ok" })
                    : Results.Problem("La base de datos no responde.", statusCode: StatusCodes.Status503ServiceUnavailable))
            .WithTags("Salud").WithSummary("Comprueba la conexión con MySQL");

        app.MapCatalogoEndpoints();
        app.MapReservaEndpoints();
        app.MapAdjuntoEndpoints();
        app.MapAuthEndpoints();
        app.MapHub<ReservasHub>(ReservasHub.Ruta);
        return app;
    }

    /// <summary>
    /// Sirve el sitio de frontend/ desde la misma API (mismo origen: la cookie SameSite=Strict
    /// funciona y no hace falta CORS). En Docker se copia a wwwroot; en desarrollo se lee de ../../../frontend.
    /// </summary>
    private static void UseFrontend(WebApplication app)
    {
        var ruta = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, app.Configuration["Frontend:Ruta"] ?? "wwwroot"));
        if (!Directory.Exists(ruta)) return;
        var archivos = new PhysicalFileProvider(ruta);
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = archivos });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = archivos });
    }

    private static Task EncabezadosSeguridad(HttpContext ctx, Func<Task> siguiente)
    {
        var h = ctx.Response.Headers;
        h.XContentTypeOptions = "nosniff";
        h.XFrameOptions = "DENY";
        h["Referrer-Policy"] = "no-referrer";
        return siguiente();
    }
}
