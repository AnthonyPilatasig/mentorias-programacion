using Mentorias.Api.Application;
using Mentorias.Api.Contracts;
using Mentorias.Api.Domain;

namespace Mentorias.Api.Tests.Unitarias;

public class ValidadoresTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 11, 10, 10, 0, 0, TimeSpan.FromHours(-5));

    private static ReservaRequest Valida() => new(3, "María José", "mj@correo.com", "intermedio", "Repasar POO", Inicio);

    [Fact]
    public void Reserva_normalizaEspaciosYCorreo()
    {
        var v = Validadores.Reserva(Valida() with { Nombre = "  María   José ", Correo = " MJ@Correo.COM ", Nivel = "INTERMEDIO", Mensaje = "   " });

        Assert.True(v.EsValida);
        Assert.Equal(new DatosReserva(3, "María José", "mj@correo.com", NivelEstudiante.Intermedio, null, Inicio), v.Valor);
    }

    [Fact]
    public void Reserva_vacia_informaCadaCampoObligatorio()
    {
        var v = Validadores.Reserva(new ReservaRequest(null, null, null, null, null, null));

        Assert.False(v.EsValida);
        Assert.Equal(["correo", "inicio", "mentoriaId", "nivel", "nombre"], v.Errores.Keys.Order());
    }

    [Theory]
    [InlineData("Jo")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("Ana123")]
    public void Reserva_rechazaNombresInvalidos(string nombre) =>
        Assert.True(Validadores.Reserva(Valida() with { Nombre = nombre }).Errores.ContainsKey("nombre"));

    [Theory]
    [InlineData("sin-arroba")]
    [InlineData("a@b")]
    [InlineData("a b@c.com")]
    public void Reserva_rechazaCorreosInvalidos(string correo) =>
        Assert.True(Validadores.Reserva(Valida() with { Correo = correo }).Errores.ContainsKey("correo"));

    [Theory]
    [InlineData("experto")]
    [InlineData("1")] // Enum.TryParse aceptaría números; aquí no.
    public void Reserva_rechazaNivelesDesconocidos(string nivel) =>
        Assert.True(Validadores.Reserva(Valida() with { Nivel = nivel }).Errores.ContainsKey("nivel"));

    [Fact]
    public void Reserva_limitaLongitudDelMensaje() =>
        Assert.True(Validadores.Reserva(Valida() with { Mensaje = new string('a', 501) }).Errores.ContainsKey("mensaje"));

    [Theory]
    [InlineData("pendiente")] // no se puede volver a pendiente
    [InlineData("borrada")]
    [InlineData(null)]
    public void CambioEstado_rechazaValoresNoPermitidos(string? estado) =>
        Assert.False(Validadores.CambioEstado(new CambioEstadoRequest(estado)).EsValida);

    [Fact]
    public void Filtro_aplicaValoresPorDefecto()
    {
        var v = Validadores.Filtro(new FiltroReservasQuery(null, null, null, " A@B.com ", null, null));

        Assert.Equal(new FiltroReservas(null, null, null, "a@b.com", 1, 20), v.Valor);
    }

    [Fact]
    public void Filtro_rechazaRangosYPaginasInvalidos()
    {
        var v = Validadores.Filtro(new FiltroReservasQuery("x", new DateOnly(2026, 12, 1), new DateOnly(2026, 11, 1), null, 0, 500));

        Assert.Equal(["estado", "hasta", "pagina", "tamano"], v.Errores.Keys.Order());
    }

    [Fact]
    public void ConsultaHorarios_limitaLaVentanaDeFechas()
    {
        var hoy = new DateOnly(2026, 11, 2);

        Assert.True(Validadores.ConsultaHorarios(hoy, 1, hoy).EsValida);
        Assert.False(Validadores.ConsultaHorarios(hoy.AddDays(-1), 1, hoy).EsValida);
        Assert.False(Validadores.ConsultaHorarios(hoy.AddDays(61), 1, hoy).EsValida);
    }
}
