using Mentorias.Api.Domain;

namespace Mentorias.Api.Tests.Unitarias;

public class AgendaTests
{
    private static readonly TimeSpan Ec = TimeSpan.FromHours(-5);
    // Lunes 2 de noviembre de 2026, 08:00 en Ecuador.
    private static readonly DateTimeOffset Ahora = new(2026, 11, 2, 8, 0, 0, Ec);

    private static DateTimeOffset Local(int dia, int hora, int minuto = 0) => new(2026, 11, dia, hora, minuto, 0, Ec);

    [Theory]
    [InlineData(10, 10, 0, 60)]  // martes 10:00
    [InlineData(10, 10, 30, 90)] // a la media hora
    [InlineData(10, 19, 0, 60)]  // termina justo a las 20:00
    [InlineData(14, 8, 0, 90)]   // sábado
    public void ValidarInicio_aceptaHorariosValidos(int dia, int hora, int minuto, int duracion) =>
        Assert.Null(Agenda.ValidarInicio(Local(dia, hora, minuto), duracion, Ahora));

    [Theory]
    [InlineData(10, 10, 15, 60, "media hora")]
    [InlineData(8, 10, 0, 60, "domingos")]
    [InlineData(10, 7, 30, 60, "08:00 y las 20:00")]
    [InlineData(10, 19, 0, 90, "08:00 y las 20:00")]   // terminaría 20:30
    [InlineData(2, 9, 0, 60, "anticipación")]          // dentro de las 2 horas
    public void ValidarInicio_rechazaConMotivo(int dia, int hora, int minuto, int duracion, string motivo) =>
        Assert.Contains(motivo, Agenda.ValidarInicio(Local(dia, hora, minuto), duracion, Ahora));

    [Fact]
    public void ValidarInicio_rechazaMasDeSesentaDias() =>
        Assert.Contains("60 días", Agenda.ValidarInicio(Ahora.AddDays(61).AddHours(2), 60, Ahora));

    [Fact]
    public void ValidarInicio_esIndependienteDeLaZonaDelCliente()
    {
        // 15:00Z es 10:00 en Ecuador: misma hora expresada en UTC.
        Assert.Null(Agenda.ValidarInicio(new DateTimeOffset(2026, 11, 10, 15, 0, 0, TimeSpan.Zero), 60, Ahora));
    }

    [Fact]
    public void BloquesDe_divideLaSesionEnTramosDeTreintaMinutosUtc()
    {
        var bloques = Agenda.BloquesDe(Local(10, 10), 90);

        Assert.Equal(
            [new DateTime(2026, 11, 10, 15, 0, 0), new DateTime(2026, 11, 10, 15, 30, 0), new DateTime(2026, 11, 10, 16, 0, 0)],
            bloques);
    }

    [Fact]
    public void DiferenciaBloques_soloLiberaYOcupaLoQueCambia()
    {
        var actuales = Agenda.BloquesDe(Local(10, 10), 60);   // 10:00, 10:30
        var nuevos = Agenda.BloquesDe(Local(10, 10, 30), 60); // 10:30, 11:00

        var (quitar, agregar) = Agenda.DiferenciaBloques(actuales, nuevos);

        Assert.Equal([actuales[0]], quitar);
        Assert.Equal([nuevos[1]], agregar);
    }

    [Fact]
    public void Horarios_marcaNoDisponiblesLosQueSeCruzanConBloquesOcupados()
    {
        var ocupados = Agenda.BloquesDe(Local(10, 10), 60).ToHashSet();

        var horarios = Agenda.Horarios(DateOnly.FromDateTime(new DateTime(2026, 11, 10)), 60, ocupados, Ahora);

        Assert.Equal(23, horarios.Count); // 08:00 a 19:00 cada 30 minutos
        Assert.Equal(Local(10, 8), horarios[0].Inicio);
        Assert.Equal(Local(10, 19), horarios[^1].Inicio);
        var noDisponibles = horarios.Where(h => !h.Disponible).Select(h => h.Inicio.TimeOfDay).ToArray();
        Assert.Equal([new TimeSpan(9, 30, 0), new TimeSpan(10, 0, 0), new TimeSpan(10, 30, 0)], noDisponibles);
    }

    [Fact]
    public void Horarios_domingoNoTieneHorarios() =>
        Assert.Empty(Agenda.Horarios(new DateOnly(2026, 11, 8), 60, new HashSet<DateTime>(), Ahora));

    [Fact]
    public void Horarios_hoyDescartaLosQueNoCumplenAnticipacion()
    {
        var horarios = Agenda.Horarios(new DateOnly(2026, 11, 2), 60, new HashSet<DateTime>(), Ahora);

        Assert.All(horarios.Where(h => h.Inicio < Ahora.AddHours(2)), h => Assert.False(h.Disponible));
        Assert.True(horarios.Single(h => h.Inicio == Ahora.AddHours(2)).Disponible);
    }

    [Fact]
    public void RangoUtcDelDia_cubreElDiaLocalCompleto()
    {
        var (desde, hasta) = Agenda.RangoUtcDelDia(new DateOnly(2026, 11, 10));

        Assert.Equal(new DateTime(2026, 11, 10, 5, 0, 0), desde);
        Assert.Equal(new DateTime(2026, 11, 11, 5, 0, 0), hasta);
    }
}
