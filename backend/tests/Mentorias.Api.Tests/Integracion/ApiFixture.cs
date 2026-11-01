using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mentorias.Api.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MySql;

namespace Mentorias.Api.Tests.Integracion;

public sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => ahora;
}

[CollectionDefinition(Nombre)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Nombre = "api";
}

/// <summary>
/// Levanta un MySQL 8.4 real en Docker y la API completa en memoria. El reloj está fijo en
/// el lunes 2/11/2026 08:00 (Ecuador) para que las reglas de horario sean deterministas.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public static readonly DateTimeOffset Ahora = new(2026, 11, 2, 8, 0, 0, TimeSpan.FromHours(-5));
    public const string CorreoMentor = "mentor@pruebas.dev";
    public const string PasswordMentor = "Clave#Pruebas2026";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly MySqlContainer _mysql = new MySqlBuilder("mysql:8.4").Build();
    private readonly string _carpetaAdjuntos = Path.Combine(Path.GetTempPath(), "mentorias-pruebas", Guid.NewGuid().ToString("N"));
    private int _siguienteHorario = -1;

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _mysql.StartAsync();
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.UseSetting("ConnectionStrings:Mentorias", _mysql.GetConnectionString());
            b.UseSetting("Mentor:Correo", CorreoMentor);
            b.UseSetting("Mentor:Password", PasswordMentor);
            b.UseSetting("Mentor:Nombre", "Mentor de pruebas");
            b.UseSetting("Almacen:Ruta", _carpetaAdjuntos);
            b.UseSetting("Limites:ReservasPorMinuto", "100000");
            b.UseSetting("Limites:LoginPorMinuto", "100000");
            b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(new RelojFijo(Ahora)));
        });
        _ = Factory.Server; // arranca la API: aplica migraciones y crea el mentor
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _mysql.DisposeAsync();
        if (Directory.Exists(_carpetaAdjuntos)) Directory.Delete(_carpetaAdjuntos, recursive: true);
    }

    /// <summary>Cada prueba recibe un horario distinto (lun-sáb, cada 2 h desde el martes 3/11).</summary>
    public DateTimeOffset NuevoHorario()
    {
        var n = Interlocked.Increment(ref _siguienteHorario);
        var dias = Enumerable.Range(1, 120)
            .Select(i => new DateTimeOffset(2026, 11, 2, 0, 0, 0, Ahora.Offset).AddDays(i))
            .Where(d => d.DayOfWeek != DayOfWeek.Sunday);
        return dias.ElementAt(n / 6).AddHours(8 + n % 6 * 2);
    }

    public HttpClient ClienteAnonimo() => Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    public async Task<(HttpClient Cliente, string Cookie)> ClienteMentorAsync()
    {
        var cliente = ClienteAnonimo();
        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(CorreoMentor, PasswordMentor));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cookie = respuesta.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("mentorias.sesion=")).Split(';')[0];
        cliente.DefaultRequestHeaders.Add("Cookie", cookie);
        return (cliente, cookie);
    }

    public static ReservaRequest Peticion(DateTimeOffset inicio, int mentoriaId = 1, string correo = "estudiante@correo.com") =>
        new(mentoriaId, "Estudiante de Prueba", correo, "inicial", "Quiero repasar", inicio);

    public async Task<ReservaResponse> CrearReservaAsync(DateTimeOffset? inicio = null, int mentoriaId = 1, string correo = "estudiante@correo.com")
    {
        var respuesta = await ClienteAnonimo().PostAsJsonAsync("/api/reservas", Peticion(inicio ?? NuevoHorario(), mentoriaId, correo));
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await Leer<ReservaResponse>(respuesta))!;
    }

    public HubConnection ConexionHub(string? cookie = null) => new HubConnectionBuilder()
        .WithUrl(new Uri(Factory.Server.BaseAddress, "hubs/reservas"), o =>
        {
            o.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
            o.Transports = HttpTransportType.LongPolling;
            if (cookie is not null) o.Headers["Cookie"] = cookie;
        })
        .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)))
        .Build();

    public static async Task<T?> Leer<T>(HttpResponseMessage respuesta) =>
        await respuesta.Content.ReadFromJsonAsync<T>(Json);

    /// <summary>Comprueba el formato uniforme de error y devuelve el cuerpo.</summary>
    public static async Task<ValidationProblemDetails> Problema(HttpResponseMessage respuesta, HttpStatusCode estado, string codigo)
    {
        Assert.Equal(estado, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = (await Leer<ValidationProblemDetails>(respuesta))!;
        Assert.Equal((int)estado, problema.Status);
        Assert.False(string.IsNullOrEmpty(problema.Title));
        Assert.True(problema.Extensions.ContainsKey("traceId"));
        Assert.Equal(codigo, problema.Extensions["codigo"]?.ToString());
        return problema;
    }

    public static HttpRequestMessage ConIfMatch(HttpMethod metodo, string url, object? cuerpo, string? etag)
    {
        var peticion = new HttpRequestMessage(metodo, url) { Content = cuerpo is null ? null : JsonContent.Create(cuerpo) };
        if (etag is not null) peticion.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(etag));
        return peticion;
    }
}
