using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Mentorias.Api.Application;
using Mentorias.Api.Domain;
using Mentorias.Api.Endpoints;
using Mentorias.Api.Infrastructure;
using Mentorias.Api.Realtime;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using MySql.EntityFrameworkCore.Extensions;

namespace Mentorias.Api.Configuracion;

public static class Politicas
{
    public const string Mentor = "Mentor";
    public const string LimiteReservas = "limite-reservas";
    public const string LimiteLogin = "limite-login";
}

public static class Servicios
{
    public static IServiceCollection AddMentoriasApi(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddMemoryCache();
        // Las claves que cifran la cookie se guardan en disco para sobrevivir a reinicios del contenedor.
        services.AddDataProtection()
            .SetApplicationName("mentorias-api")
            .PersistKeysToFileSystem(new DirectoryInfo(config["DataProtection:Ruta"] ?? "App_Data/claves"));

        services.AddDbContext<MentoriasDbContext>(o => o.UseMySQL(
            config.GetConnectionString("Mentorias")
            ?? throw new InvalidOperationException("Falta la cadena de conexión ConnectionStrings:Mentorias.")));

        services.ConfigureHttpJsonOptions(o => ConfigurarJson(o.SerializerOptions));
        // Los errores de lectura (JSON mal formado, tipos incorrectos) pasan por el manejador
        // de excepciones para responder siempre con el mismo formato.
        services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);
        services.AddProblemDetails(o => o.CustomizeProblemDetails = ProblemasHttp.Personalizar);
        services.AddExceptionHandler<ManejadorExcepciones>();

        AddSesiones(services);
        AddLimites(services, config);

        services.AddCors(o => o.AddDefaultPolicy(p => p
            .WithOrigins(config.GetSection("Cors:Origenes").Get<string[]>() ?? [])
            .WithMethods("GET", "POST")
            .WithHeaders("Content-Type")));

        services.AddSignalR().AddJsonProtocol(o => ConfigurarJson(o.PayloadSerializerOptions));
        services.AddOpenApi(o => o.AddDocumentTransformer((doc, _, _) =>
        {
            doc.Info = new OpenApiInfo
            {
                Title = "API de Mentorías de código",
                Version = "v1",
                Description = "Reservas de mentorías con persistencia en MySQL, sesiones del mentor, adjuntos y " +
                              "notificaciones en tiempo real (SignalR en /hubs/reservas). Errores en formato RFC 9457.",
            };
            return Task.CompletedTask;
        }));

        services.Configure<OpcionesAlmacen>(config.GetSection("Almacen"));
        services.AddSingleton<IAlmacenArchivos, AlmacenArchivosLocal>();
        services.AddSingleton<IPasswordHasher<Mentor>, PasswordHasher<Mentor>>();
        services.AddScoped<INotificadorReservas, NotificadorSignalR>();
        services.AddScoped<CatalogoService>();
        services.AddScoped<ReservaService>();
        services.AddScoped<AdjuntoService>();
        services.AddScoped<AuthService>();
        return services;
    }

    public static void ConfigurarJson(JsonSerializerOptions o)
    {
        o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        o.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    }

    private static void AddSesiones(IServiceCollection services)
    {
        services.AddSingleton<AlmacenSesiones>();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
        {
            o.Cookie.Name = "mentorias.sesion";
            o.Cookie.HttpOnly = true;                       // inaccesible desde JavaScript
            o.Cookie.SameSite = SameSiteMode.Strict;        // no viaja en peticiones de otros sitios (CSRF)
            o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            o.ExpireTimeSpan = TimeSpan.FromHours(2);
            o.SlidingExpiration = true;
            // En una API no se redirige a una página de login: se responde 401/403.
            o.Events.OnRedirectToLogin = ctx => ResponderSinRedireccion(ctx.Response, StatusCodes.Status401Unauthorized);
            o.Events.OnRedirectToAccessDenied = ctx => ResponderSinRedireccion(ctx.Response, StatusCodes.Status403Forbidden);
        });
        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<AlmacenSesiones>((o, almacen) => o.SessionStore = almacen);

        services.AddAuthorizationBuilder()
            .AddPolicy(Politicas.Mentor, p => p.RequireAuthenticatedUser().RequireRole(Mapeos.RolMentor));
    }

    private static Task ResponderSinRedireccion(HttpResponse response, int status)
    {
        response.StatusCode = status;
        return Task.CompletedTask;
    }

    private static void AddLimites(IServiceCollection services, IConfiguration config)
    {
        var reservasPorMinuto = config.GetValue("Limites:ReservasPorMinuto", 10);
        var loginPorMinuto = config.GetValue("Limites:LoginPorMinuto", 10);

        services.AddRateLimiter(o =>
        {
            o.OnRejected = async (ctx, _) =>
            {
                if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
                    ctx.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(espera.TotalSeconds).ToString();
                await ProblemasHttp.EscribirAsync(ctx.HttpContext, StatusCodes.Status429TooManyRequests,
                    "demasiadas_peticiones", "Hiciste demasiadas peticiones seguidas. Espera un momento y vuelve a intentarlo.");
            };
            o.AddPolicy(Politicas.LimiteReservas, ctx => PorIp(ctx, reservasPorMinuto));
            o.AddPolicy(Politicas.LimiteLogin, ctx => PorIp(ctx, loginPorMinuto));
        });
    }

    private static RateLimitPartition<string> PorIp(HttpContext ctx, int porMinuto) =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "local",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = porMinuto, Window = TimeSpan.FromMinutes(1) });
}
