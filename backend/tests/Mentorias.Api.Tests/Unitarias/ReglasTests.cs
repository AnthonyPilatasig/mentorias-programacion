using System.Text;
using Mentorias.Api.Application;
using Mentorias.Api.Domain;

namespace Mentorias.Api.Tests.Unitarias;

public class ReglasReservaTests
{
    [Theory]
    [InlineData(EstadoReserva.Pendiente, EstadoReserva.Confirmada, true)]
    [InlineData(EstadoReserva.Pendiente, EstadoReserva.Cancelada, true)]
    [InlineData(EstadoReserva.Pendiente, EstadoReserva.Completada, false)]
    [InlineData(EstadoReserva.Confirmada, EstadoReserva.Completada, true)]
    [InlineData(EstadoReserva.Confirmada, EstadoReserva.Cancelada, true)]
    [InlineData(EstadoReserva.Cancelada, EstadoReserva.Confirmada, false)]
    [InlineData(EstadoReserva.Completada, EstadoReserva.Cancelada, false)]
    public void PuedeTransicionar_sigueElFlujoDeEstados(EstadoReserva actual, EstadoReserva nuevo, bool esperado) =>
        Assert.Equal(esperado, ReglasReserva.PuedeTransicionar(actual, nuevo));

    [Fact]
    public void SoloLaCancelacionLiberaLaAgenda()
    {
        Assert.False(ReglasReserva.OcupaAgenda(EstadoReserva.Cancelada));
        Assert.True(ReglasReserva.OcupaAgenda(EstadoReserva.Completada));
    }

    [Theory]
    [InlineData("\"3\"", 3, true)]
    [InlineData("W/\"3\"", 3, true)]
    [InlineData("\"1\", \"3\"", 3, true)]
    [InlineData("*", 9, true)]
    [InlineData("\"2\"", 3, false)]
    [InlineData("basura", 3, false)]
    public void Etag_coincideSegunRfc9110(string ifMatch, int version, bool esperado) =>
        Assert.Equal(esperado, Etag.Coincide(ifMatch, version));
}

public class ReglasArchivoTests
{
    private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);

    [Theory]
    [InlineData("tarea.pdf", "%PDF-1.7", "application/pdf")]
    [InlineData("Main.java", "public class Main {}", "text/plain; charset=utf-8")]
    [InlineData("notas.TXT", "año y acción", "text/plain; charset=utf-8")]
    public void Evaluar_aceptaTiposPermitidosConFirmaCorrecta(string nombre, string contenido, string mime)
    {
        var e = Evaluar(nombre, Bytes(contenido));

        Assert.True(e.Aceptado);
        Assert.Equal(mime, e.TipoContenido);
    }

    [Fact]
    public void Evaluar_aceptaPngPorSusBytesMagicos() =>
        Assert.True(Evaluar("captura.png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0]).Aceptado);

    [Fact]
    public void Evaluar_rechazaUnEjecutableRenombradoComoPdf() =>
        Assert.Equal(MotivoRechazo.ContenidoNoCoincide, Evaluar("tarea.pdf", Bytes("MZ\u0090\0")).Rechazo);

    [Fact]
    public void Evaluar_rechazaTextoConBytesBinarios() =>
        Assert.Equal(MotivoRechazo.ContenidoNoCoincide, Evaluar("a.txt", [0x41, 0x00, 0x42]).Rechazo);

    [Fact]
    public void Evaluar_rechazaExtensionesFueraDeLaListaBlanca() =>
        Assert.Equal(MotivoRechazo.ExtensionNoPermitida, Evaluar("virus.exe", Bytes("MZ")).Rechazo);

    [Fact]
    public void Evaluar_rechazaArchivosVaciosGrandesOSobreElLimite()
    {
        Assert.Equal(MotivoRechazo.Vacio, ReglasArchivo.Evaluar("a.txt", 0, [], 0).Rechazo);
        Assert.Equal(MotivoRechazo.MuyGrande, ReglasArchivo.Evaluar("a.txt", ReglasArchivo.TamanoMaximo + 1, Bytes("a"), 0).Rechazo);
        Assert.Equal(MotivoRechazo.LimiteAlcanzado, ReglasArchivo.Evaluar("a.txt", 1, Bytes("a"), ReglasArchivo.MaximoPorReserva).Rechazo);
    }

    [Theory]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("C:\\Windows\\system.ini", "system.ini")]
    [InlineData("mi tarea (final).pdf", "mi tarea _final_.pdf")]
    [InlineData("...", "archivo")]
    public void NombreSeguro_eliminaRutasYCaracteresPeligrosos(string entrada, string esperado) =>
        Assert.Equal(esperado, ReglasArchivo.NombreSeguro(entrada));

    private static EvaluacionArchivo Evaluar(string nombre, byte[] contenido) =>
        ReglasArchivo.Evaluar(nombre, contenido.Length, contenido, 0);
}

public class ReglasLoginTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 2, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BloqueaAlQuintoFalloYSeLiberaAlVencer()
    {
        var intentos = Enumerable.Range(0, ReglasLogin.MaximoFallos)
            .Aggregate((IntentosLogin?)null, (actual, _) => ReglasLogin.RegistrarFallo(actual, Ahora));

        Assert.Equal(ReglasLogin.DuracionBloqueo, ReglasLogin.TiempoRestante(intentos, Ahora));
        Assert.Null(ReglasLogin.TiempoRestante(intentos, Ahora + ReglasLogin.DuracionBloqueo));
    }

    [Fact]
    public void CuatroFallosNoBloquean()
    {
        var intentos = Enumerable.Range(0, ReglasLogin.MaximoFallos - 1)
            .Aggregate((IntentosLogin?)null, (actual, _) => ReglasLogin.RegistrarFallo(actual, Ahora));

        Assert.Null(ReglasLogin.TiempoRestante(intentos, Ahora));
    }
}
