using System.Net.Http.Json;
using Mentorias.Api.Contracts;
using Mentorias.Api.Domain;
using Microsoft.AspNetCore.SignalR.Client;
using static Mentorias.Api.Tests.Integracion.ApiFixture;

namespace Mentorias.Api.Tests.Integracion;

[Collection(ApiCollection.Nombre)]
public class TiempoRealTests(ApiFixture api)
{
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(15);

    public sealed record FechasCambiadas(string[] Fechas);

    [Fact]
    public async Task MentorRecibeLaReservaYElPublicoSoloLosHorarios()
    {
        var (_, cookie) = await api.ClienteMentorAsync();
        await using var mentor = api.ConexionHub(cookie);
        await using var publico = api.ConexionHub();
        var reservaRecibida = new TaskCompletionSource<ReservaResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var horariosPublico = new TaskCompletionSource<FechasCambiadas>(TaskCreationOptions.RunContinuationsAsynchronously);
        var publicoVioDatosPersonales = false;
        mentor.On<ReservaResponse>("reservaCreada", r => reservaRecibida.TrySetResult(r));
        publico.On<ReservaResponse>("reservaCreada", _ => publicoVioDatosPersonales = true);
        publico.On<FechasCambiadas>("horariosActualizados", f => horariosPublico.TrySetResult(f));
        await mentor.StartAsync();
        await publico.StartAsync();

        var creada = await api.CrearReservaAsync(correo: "tiempo-real@correo.com");

        var notificada = await reservaRecibida.Task.WaitAsync(Espera);
        var fechas = await horariosPublico.Task.WaitAsync(Espera);
        Assert.Equal(creada.Id, notificada.Id);
        Assert.Equal("tiempo-real@correo.com", notificada.Correo);
        Assert.Contains(creada.Inicio.ToString("yyyy-MM-dd"), fechas.Fechas);
        Assert.False(publicoVioDatosPersonales);
    }

    [Fact]
    public async Task CambiarEstado_notificaReservaActualizada()
    {
        var reserva = await api.CrearReservaAsync();
        var (cliente, cookie) = await api.ClienteMentorAsync();
        await using var mentor = api.ConexionHub(cookie);
        var actualizada = new TaskCompletionSource<ReservaResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        mentor.On<ReservaResponse>("reservaActualizada", r => { if (r.Id == reserva.Id) actualizada.TrySetResult(r); });
        await mentor.StartAsync();

        await cliente.SendAsync(ConIfMatch(HttpMethod.Patch, $"/api/reservas/{reserva.Id}/estado", new CambioEstadoRequest("confirmada"), "\"1\""));

        var evento = await actualizada.Task.WaitAsync(Espera);
        Assert.Equal((EstadoReserva.Confirmada, 2), (evento.Estado, evento.Version));
    }

    [Fact]
    public async Task EliminarReserva_notificaAlMentor()
    {
        var reserva = await api.CrearReservaAsync();
        var (cliente, cookie) = await api.ClienteMentorAsync();
        await using var mentor = api.ConexionHub(cookie);
        var eliminada = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        mentor.On<IdEliminado>("reservaEliminada", e => eliminada.TrySetResult(e.Id));
        await mentor.StartAsync();

        await cliente.DeleteAsync($"/api/reservas/{reserva.Id}");

        Assert.Equal(reserva.Id, await eliminada.Task.WaitAsync(Espera));
    }

    public sealed record IdEliminado(int Id);
}
