using Mentorias.Api.Contracts;

namespace Mentorias.Api.Application;

/// <summary>Puerto de salida para eventos en tiempo real; la implementación usa SignalR.</summary>
public interface INotificadorReservas
{
    Task ReservaCreadaAsync(ReservaResponse reserva);
    Task ReservaActualizadaAsync(ReservaResponse reserva);
    Task ReservaEliminadaAsync(int id);
    Task AdjuntoAgregadoAsync(AdjuntoResponse adjunto);
    Task AdjuntoEliminadoAsync(int reservaId, Guid id);
    Task HorariosActualizadosAsync(IEnumerable<DateOnly> fechas);
}
