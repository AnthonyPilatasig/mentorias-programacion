namespace Mentorias.Api.Domain;

public sealed record Horario(DateTimeOffset Inicio, DateTimeOffset Fin, bool Disponible);

/// <summary>Reglas de horario. Funciones puras: no leen el reloj ni la base de datos.</summary>
public static class Agenda
{
    public const string ZonaHoraria = "America/Guayaquil";

    /// <summary>Ecuador continental no usa horario de verano: UTC-5 todo el año.</summary>
    public static readonly TimeSpan OffsetLocal = TimeSpan.FromHours(-5);

    public static readonly TimeSpan Bloque = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan Apertura = TimeSpan.FromHours(8);
    public static readonly TimeSpan Cierre = TimeSpan.FromHours(20);
    public static readonly TimeSpan AnticipacionMinima = TimeSpan.FromHours(2);
    public const int DiasMaximos = 60;

    public static DateTimeOffset ALocal(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToOffset(OffsetLocal);

    public static DateOnly FechaLocal(DateTimeOffset instante) =>
        DateOnly.FromDateTime(instante.ToOffset(OffsetLocal).DateTime);

    public static (DateTime DesdeUtc, DateTime HastaUtc) RangoUtcDelDia(DateOnly fecha)
    {
        var inicio = new DateTimeOffset(fecha.ToDateTime(TimeOnly.MinValue), OffsetLocal);
        return (inicio.UtcDateTime, inicio.AddDays(1).UtcDateTime);
    }

    /// <summary>Devuelve el motivo por el que el inicio no es válido, o null si lo es.</summary>
    public static string? ValidarInicio(DateTimeOffset inicio, int duracionMinutos, DateTimeOffset ahora)
    {
        var local = inicio.ToOffset(OffsetLocal);
        var fin = local.AddMinutes(duracionMinutos);

        if (local.TimeOfDay.Ticks % Bloque.Ticks != 0)
            return "La sesión debe empezar en punto o a la media hora (por ejemplo 10:00 o 10:30).";
        if (local.DayOfWeek == DayOfWeek.Sunday)
            return "No hay sesiones los domingos.";
        if (local.TimeOfDay < Apertura || fin.Date != local.Date || fin.TimeOfDay > Cierre)
            return "La sesión debe empezar y terminar entre las 08:00 y las 20:00 (hora de Ecuador).";
        if (inicio < ahora + AnticipacionMinima)
            return "Reserva con al menos 2 horas de anticipación.";
        if (inicio > ahora.AddDays(DiasMaximos))
            return $"Solo se puede reservar hasta {DiasMaximos} días adelante.";
        return null;
    }

    public static DateTime[] BloquesDe(DateTimeOffset inicio, int duracionMinutos) =>
        Enumerable.Range(0, duracionMinutos / (int)Bloque.TotalMinutes)
            .Select(i => inicio.UtcDateTime.Add(Bloque * i))
            .ToArray();

    /// <summary>Qué bloques hay que liberar y cuáles ocupar al mover una reserva.</summary>
    public static (DateTime[] Quitar, DateTime[] Agregar) DiferenciaBloques(
        IEnumerable<DateTime> actuales, IEnumerable<DateTime> nuevos)
    {
        var a = actuales.ToHashSet();
        var n = nuevos.ToHashSet();
        return (a.Except(n).ToArray(), n.Except(a).ToArray());
    }

    /// <summary>Lista los inicios posibles de un día y marca si están libres.</summary>
    public static IReadOnlyList<Horario> Horarios(
        DateOnly fecha, int duracionMinutos, IReadOnlySet<DateTime> ocupadosUtc, DateTimeOffset ahora)
    {
        var duracion = TimeSpan.FromMinutes(duracionMinutos);
        var cantidad = (int)((Cierre - Apertura - duracion) / Bloque) + 1;
        var medianoche = new DateTimeOffset(fecha.ToDateTime(TimeOnly.MinValue), OffsetLocal);

        return Enumerable.Range(0, Math.Max(cantidad, 0))
            .Select(i => medianoche + Apertura + Bloque * i)
            .Where(inicio => inicio.DayOfWeek != DayOfWeek.Sunday)
            .Select(inicio => new Horario(
                inicio,
                inicio + duracion,
                ValidarInicio(inicio, duracionMinutos, ahora) is null
                    && !BloquesDe(inicio, duracionMinutos).Any(ocupadosUtc.Contains)))
            .ToList();
    }
}
