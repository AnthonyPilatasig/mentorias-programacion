using Mentorias.Api.Application;
using Mentorias.Api.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace Mentorias.Api.Realtime;

/// <summary>
/// Hub de notificaciones. Cualquiera puede conectarse para saber qué horarios cambiaron
/// (sin datos personales); solo el mentor autenticado entra al grupo que recibe las reservas.
/// </summary>
public sealed class ReservasHub : Hub
{
    public const string Ruta = "/hubs/reservas";
    public const string GrupoMentores = "mentores";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole(Mapeos.RolMentor) == true)
            await Groups.AddToGroupAsync(Context.ConnectionId, GrupoMentores);
        await base.OnConnectedAsync();
    }
}

/// <summary>Nombres de los eventos que reciben los clientes.</summary>
public static class Eventos
{
    public const string ReservaCreada = "reservaCreada";
    public const string ReservaActualizada = "reservaActualizada";
    public const string ReservaEliminada = "reservaEliminada";
    public const string AdjuntoAgregado = "adjuntoAgregado";
    public const string AdjuntoEliminado = "adjuntoEliminado";
    public const string HorariosActualizados = "horariosActualizados";
}

public sealed class NotificadorSignalR(IHubContext<ReservasHub> hub, ILogger<NotificadorSignalR> logger) : INotificadorReservas
{
    private IClientProxy Mentores => hub.Clients.Group(ReservasHub.GrupoMentores);

    public Task ReservaCreadaAsync(ReservaResponse r) => Enviar(Mentores, Eventos.ReservaCreada, r);
    public Task ReservaActualizadaAsync(ReservaResponse r) => Enviar(Mentores, Eventos.ReservaActualizada, r);
    public Task ReservaEliminadaAsync(int id) => Enviar(Mentores, Eventos.ReservaEliminada, new { id });
    public Task AdjuntoAgregadoAsync(AdjuntoResponse a) => Enviar(Mentores, Eventos.AdjuntoAgregado, a);
    public Task AdjuntoEliminadoAsync(int reservaId, Guid id) => Enviar(Mentores, Eventos.AdjuntoEliminado, new { reservaId, id });

    public Task HorariosActualizadosAsync(IEnumerable<DateOnly> fechas) =>
        Enviar(hub.Clients.All, Eventos.HorariosActualizados, new { fechas = fechas.Select(f => f.ToString("yyyy-MM-dd")) });

    // La operación ya se guardó: un fallo al notificar se registra pero no revierte la petición.
    private async Task Enviar(IClientProxy destino, string evento, object datos)
    {
        try
        {
            await destino.SendAsync(evento, datos);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo enviar el evento {Evento}", evento);
        }
    }
}
