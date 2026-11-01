using System.Net;
using System.Net.Http.Json;
using Mentorias.Api.Contracts;
using static Mentorias.Api.Tests.Integracion.ApiFixture;

namespace Mentorias.Api.Tests.Integracion;

[Collection(ApiCollection.Nombre)]
public class ConcurrenciaTests(ApiFixture api)
{
    [Fact]
    public async Task DiezReservasSimultaneasAlMismoHorario_soloUnaGana()
    {
        var inicio = api.NuevoHorario();

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 10).Select(i =>
            api.ClienteAnonimo().PostAsJsonAsync("/api/reservas", Peticion(inicio, correo: $"carrera{i}@correo.com"))));

        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(9, respuestas.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task ReservasSimultaneasQueSeSolapanParcialmente_soloUnaGana()
    {
        var inicio = api.NuevoHorario();

        // 90 min desde las X:00 y 60 min desde las X:30 comparten el bloque X:30.
        var respuestas = await Task.WhenAll(
            api.ClienteAnonimo().PostAsJsonAsync("/api/reservas", Peticion(inicio, mentoriaId: 3)),
            api.ClienteAnonimo().PostAsJsonAsync("/api/reservas", Peticion(inicio.AddMinutes(30), mentoriaId: 1)));

        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DosEdicionesConElMismoEtag_laSegundaRecibe412()
    {
        var reserva = await api.CrearReservaAsync();
        var (mentor, _) = await api.ClienteMentorAsync();
        var url = $"/api/reservas/{reserva.Id}";

        var respuestas = await Task.WhenAll(
            mentor.SendAsync(ConIfMatch(HttpMethod.Put, url, Peticion(reserva.Inicio) with { Nombre = "Primera Edicion" }, "\"1\"")),
            mentor.SendAsync(ConIfMatch(HttpMethod.Put, url, Peticion(reserva.Inicio) with { Nombre = "Segunda Edicion" }, "\"1\"")));

        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.PreconditionFailed);
        var final = await mentor.GetFromJsonAsync<ReservaResponse>(url, Json);
        Assert.Equal(2, final!.Version);
    }
}
