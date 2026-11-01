using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Mentorias.Api.Contracts;
using static Mentorias.Api.Tests.Integracion.ApiFixture;

namespace Mentorias.Api.Tests.Integracion;

[Collection(ApiCollection.Nombre)]
public class AdjuntosTests(ApiFixture api)
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n% prueba\n%%EOF");

    private static MultipartFormDataContent Formulario(string nombre, byte[] contenido)
    {
        var archivo = new ByteArrayContent(contenido);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return new MultipartFormDataContent { { archivo, "archivo", nombre } };
    }

    [Fact]
    public async Task CicloCompleto_subirListarDescargarEliminar()
    {
        var reserva = await api.CrearReservaAsync();
        var (mentor, _) = await api.ClienteMentorAsync();

        var subida = await mentor.PostAsync($"/api/reservas/{reserva.Id}/adjuntos", Formulario("../Mi Tarea.pdf", Pdf));
        Assert.Equal(HttpStatusCode.Created, subida.StatusCode);
        var adjunto = (await Leer<AdjuntoResponse>(subida))!;
        Assert.Equal(("Mi Tarea.pdf", "application/pdf", Pdf.Length), (adjunto.Nombre, adjunto.TipoContenido, (int)adjunto.TamanoBytes));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Pdf)), adjunto.Sha256);

        var lista = await mentor.GetFromJsonAsync<List<AdjuntoResponse>>($"/api/reservas/{reserva.Id}/adjuntos", Json);
        Assert.Equal(adjunto.Id, Assert.Single(lista!).Id);

        var descarga = await mentor.GetAsync($"/api/adjuntos/{adjunto.Id}");
        Assert.Equal(Pdf, await descarga.Content.ReadAsByteArrayAsync());
        Assert.Equal("attachment", descarga.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("nosniff", descarga.Headers.GetValues("X-Content-Type-Options").Single());

        Assert.Equal(HttpStatusCode.NoContent, (await mentor.DeleteAsync($"/api/adjuntos/{adjunto.Id}")).StatusCode);
        await Problema(await mentor.GetAsync($"/api/adjuntos/{adjunto.Id}"), HttpStatusCode.NotFound, "adjunto_no_encontrado");
    }

    [Fact]
    public async Task ArchivosNoPermitidos_seRechazanConElCodigoAdecuado()
    {
        var reserva = await api.CrearReservaAsync();
        var (mentor, _) = await api.ClienteMentorAsync();
        var url = $"/api/reservas/{reserva.Id}/adjuntos";
        var grande = new byte[2 * 1024 * 1024 + 1];
        Pdf.CopyTo(grande, 0);

        await Problema(await mentor.PostAsync(url, Formulario("programa.exe", [0x4D, 0x5A])), HttpStatusCode.UnsupportedMediaType, "tipo_no_permitido");
        await Problema(await mentor.PostAsync(url, Formulario("falso.pdf", [0x4D, 0x5A, 0x90])), HttpStatusCode.UnsupportedMediaType, "contenido_no_coincide");
        await Problema(await mentor.PostAsync(url, Formulario("enorme.pdf", grande)), HttpStatusCode.RequestEntityTooLarge, "archivo_muy_grande");
        await Problema(await mentor.PostAsync(url, new MultipartFormDataContent { { new StringContent("hola"), "nota" } }), HttpStatusCode.BadRequest, "validacion");
        await Problema(await mentor.PostAsync("/api/reservas/999999/adjuntos", Formulario("a.pdf", Pdf)), HttpStatusCode.NotFound, "reserva_no_encontrada");
        await Problema(await api.ClienteAnonimo().PostAsync(url, Formulario("a.pdf", Pdf)), HttpStatusCode.Unauthorized, "no_autenticado");
    }

    [Fact]
    public async Task MasDeCincoAdjuntos_devuelve409()
    {
        var reserva = await api.CrearReservaAsync();
        var (mentor, _) = await api.ClienteMentorAsync();
        var url = $"/api/reservas/{reserva.Id}/adjuntos";
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Created, (await mentor.PostAsync(url, Formulario($"Main{i}.java", "class A {}"u8.ToArray()))).StatusCode);

        await Problema(await mentor.PostAsync(url, Formulario("Main5.java", "class A {}"u8.ToArray())), HttpStatusCode.Conflict, "limite_adjuntos");
    }

    [Fact]
    public async Task EliminarLaReserva_borraTambienSusAdjuntos()
    {
        var reserva = await api.CrearReservaAsync();
        var (mentor, _) = await api.ClienteMentorAsync();
        var adjunto = (await Leer<AdjuntoResponse>(await mentor.PostAsync($"/api/reservas/{reserva.Id}/adjuntos", Formulario("a.pdf", Pdf))))!;

        await mentor.DeleteAsync($"/api/reservas/{reserva.Id}");

        await Problema(await mentor.GetAsync($"/api/adjuntos/{adjunto.Id}"), HttpStatusCode.NotFound, "adjunto_no_encontrado");
    }
}
