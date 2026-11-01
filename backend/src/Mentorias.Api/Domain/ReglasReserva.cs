namespace Mentorias.Api.Domain;

/// <summary>Datos de una reserva ya validados y normalizados.</summary>
public sealed record DatosReserva(
    int MentoriaId, string Nombre, string Correo, NivelEstudiante Nivel, string? Mensaje, DateTimeOffset Inicio);

public static class ReglasReserva
{
    // Pendiente -> Confirmada | Cancelada; Confirmada -> Completada | Cancelada.
    private static readonly IReadOnlyDictionary<EstadoReserva, EstadoReserva[]> Transiciones =
        new Dictionary<EstadoReserva, EstadoReserva[]>
        {
            [EstadoReserva.Pendiente] = [EstadoReserva.Confirmada, EstadoReserva.Cancelada],
            [EstadoReserva.Confirmada] = [EstadoReserva.Completada, EstadoReserva.Cancelada],
            [EstadoReserva.Completada] = [],
            [EstadoReserva.Cancelada] = [],
        };

    public static bool PuedeTransicionar(EstadoReserva actual, EstadoReserva nuevo) =>
        Transiciones[actual].Contains(nuevo);

    public static bool EsEditable(EstadoReserva estado) =>
        estado is EstadoReserva.Pendiente or EstadoReserva.Confirmada;

    /// <summary>Una reserva cancelada libera sus bloques; las demás los conservan.</summary>
    public static bool OcupaAgenda(EstadoReserva estado) => estado != EstadoReserva.Cancelada;
}
