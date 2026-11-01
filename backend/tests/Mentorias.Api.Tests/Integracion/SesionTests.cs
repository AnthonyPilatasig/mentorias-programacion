using System.Net;
using System.Net.Http.Json;
using Mentorias.Api.Contracts;
using static Mentorias.Api.Tests.Integracion.ApiFixture;

namespace Mentorias.Api.Tests.Integracion;

[Collection(ApiCollection.Nombre)]
public class SesionTests(ApiFixture api)
{
    [Fact]
    public async Task Login_correcto_emiteCookieSeguraYAbreSesion()
    {
        var cliente = api.ClienteAnonimo();
        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(" MENTOR@pruebas.dev ", PasswordMentor));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cookie = respuesta.Headers.GetValues("Set-Cookie").Single().ToLowerInvariant();
        Assert.Contains("httponly", cookie);
        Assert.Contains("samesite=strict", cookie);

        var (mentor, _) = await api.ClienteMentorAsync();
        var sesion = await mentor.GetFromJsonAsync<SesionResponse>("/api/auth/me", Json);
        Assert.Equal(CorreoMentor, sesion!.Correo);
    }

    [Fact]
    public async Task Login_incorrecto_o_incompleto_noRevelaDetalles()
    {
        var cliente = api.ClienteAnonimo();

        await Problema(await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(CorreoMentor, "equivocada")), HttpStatusCode.Unauthorized, "credenciales_invalidas");
        await Problema(await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest("nadie@pruebas.dev", "x")), HttpStatusCode.Unauthorized, "credenciales_invalidas");
        await Problema(await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(null, null)), HttpStatusCode.BadRequest, "validacion");
    }

    [Fact]
    public async Task Logout_invalidaLaSesionEnElServidor()
    {
        var (mentor, cookie) = await api.ClienteMentorAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await mentor.PostAsync("/api/auth/logout", null)).StatusCode);

        // Un atacante que hubiera copiado la cookie ya no puede usarla.
        var copia = api.ClienteAnonimo();
        copia.DefaultRequestHeaders.Add("Cookie", cookie);
        await Problema(await copia.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized, "no_autenticado");
    }

    [Fact]
    public async Task CincoFallosSeguidos_bloqueanElCorreoCon429()
    {
        var cliente = api.ClienteAnonimo();
        var peticion = new LoginRequest("fuerza-bruta@pruebas.dev", "x");
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.PostAsJsonAsync("/api/auth/login", peticion)).StatusCode);

        var bloqueada = await cliente.PostAsJsonAsync("/api/auth/login", peticion);

        await Problema(bloqueada, HttpStatusCode.TooManyRequests, "cuenta_bloqueada");
        Assert.Equal(TimeSpan.FromMinutes(15), bloqueada.Headers.RetryAfter?.Delta);
    }
}
