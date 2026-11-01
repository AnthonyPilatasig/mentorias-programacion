using System.Net;
using System.Net.Http.Json;
using System.Text;
using Mentorias.Api.Contracts;
using Mentorias.Api.Domain;
using static Mentorias.Api.Tests.Integracion.ApiFixture;

namespace Mentorias.Api.Tests.Integracion;

[Collection(ApiCollection.Nombre)]
public class ReservasTests(ApiFixture api)
{
    [Fact]
    public async Task Mentorias_listaElCatalogoYFiltraPorCategoria()
    {
        var cliente = api.ClienteAnonimo();

        var todas = await cliente.GetFromJsonAsync<List<MentoriaResponse>>("/api/mentorias", Json);
        var java = await cliente.GetFromJsonAsync<List<MentoriaResponse>>("/api/mentorias?categoria=java", Json);

        Assert.Equal(6, todas!.Count);
        Assert.Equal(3, java!.Count);
        await Problema(await cliente.GetAsync("/api/mentorias/99"), HttpStatusCode.NotFound, "mentoria_no_encontrada");
    }

    [Fact]
    public async Task Crear_devuelve201ConLocationEtagYDatosNormalizados()
    {
        var inicio = api.NuevoHorario();
        var respuesta = await api.ClienteAnonimo().PostAsJsonAsync("/api/reservas",
            Peticion(inicio, mentoriaId: 3) with { Nombre = "  Ana   Lucía ", Correo = "ANA@Correo.com" });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var reserva = (await Leer<ReservaResponse>(respuesta))!;
        Assert.Equal($"/api/reservas/{reserva.Id}", respuesta.Headers.Location?.OriginalString);
        Assert.Equal("\"1\"", respuesta.Headers.ETag?.Tag);
        Assert.Equal(("Ana Lucía", "ana@correo.com", EstadoReserva.Pendiente), (reserva.Nombre, reserva.Correo, reserva.Estado));
        Assert.Equal(inicio.AddMinutes(90), reserva.Fin); // POO dura 90 minutos
    }

    [Fact]
    public async Task Crear_conDatosInvalidos_devuelve400ConErroresPorCampo()
    {
        var respuesta = await api.ClienteAnonimo().PostAsJsonAsync("/api/reservas",
            new ReservaRequest(0, "J", "no-es-correo", "experto", null, null));

        var problema = await Problema(respuesta, HttpStatusCode.BadRequest, "validacion");
        Assert.Equal(["correo", "inicio", "mentoriaId", "nivel", "nombre"], problema.Errors.Keys.Order());
    }

    [Fact]
    public async Task Crear_enDomingoOConMentoriaInexistente_devuelve400()
    {
        var cliente = api.ClienteAnonimo();
        var domingo = new DateTimeOffset(2026, 11, 8, 10, 0, 0, TimeSpan.FromHours(-5));

        var p1 = await Problema(await cliente.PostAsJsonAsync("/api/reservas", Peticion(domingo)), HttpStatusCode.BadRequest, "validacion");
        var p2 = await Problema(await cliente.PostAsJsonAsync("/api/reservas", Peticion(api.NuevoHorario(), mentoriaId: 42)), HttpStatusCode.BadRequest, "validacion");

        Assert.Contains("domingos", p1.Errors["inicio"][0]);
        Assert.True(p2.Errors.ContainsKey("mentoriaId"));
    }

    [Theory]
    [InlineData("{\"mentoriaId\": ")]
    [InlineData("{\"mentoriaId\": \"tres\"}")]
    public async Task Crear_conJsonMalFormado_devuelve400ConElMismoFormato(string cuerpo)
    {
        var respuesta = await api.ClienteAnonimo().PostAsync("/api/reservas", new StringContent(cuerpo, Encoding.UTF8, "application/json"));

        await Problema(respuesta, HttpStatusCode.BadRequest, "peticion_invalida");
    }

    [Fact]
    public async Task Crear_horarioQueSeCruza_devuelve409()
    {
        var inicio = api.NuevoHorario();
        await api.CrearReservaAsync(inicio, mentoriaId: 3); // 90 min

        var respuesta = await api.ClienteAnonimo().PostAsJsonAsync("/api/reservas", Peticion(inicio.AddMinutes(60)));

        await Problema(respuesta, HttpStatusCode.Conflict, "horario_ocupado");
    }

    [Fact]
    public async Task Horarios_reflejaLasReservasExistentes()
    {
        var inicio = api.NuevoHorario();
        await api.CrearReservaAsync(inicio);
        var fecha = DateOnly.FromDateTime(inicio.DateTime);

        var horarios = await api.ClienteAnonimo().GetFromJsonAsync<HorariosResponse>($"/api/horarios?fecha={fecha:yyyy-MM-dd}&mentoriaId=1", Json);

        Assert.False(horarios!.Horarios.Single(h => h.Inicio == inicio).Disponible);
        Assert.False(horarios.Horarios.Single(h => h.Inicio == inicio.AddMinutes(-30)).Disponible);
        Assert.True(horarios.Horarios.Single(h => h.Inicio == inicio.AddMinutes(60)).Disponible);
    }

    [Fact]
    public async Task Listar_requiereSesionYFiltraConPaginacion()
    {
        var correo = $"filtro-{Guid.NewGuid():N}@correo.com";
        for (var i = 0; i < 3; i++) await api.CrearReservaAsync(correo: correo);
        var (mentor, _) = await api.ClienteMentorAsync();

        await Problema(await api.ClienteAnonimo().GetAsync("/api/reservas"), HttpStatusCode.Unauthorized, "no_autenticado");
        var pagina = await mentor.GetFromJsonAsync<Paginado<ReservaResponse>>($"/api/reservas?correo={correo}&estado=pendiente&tamano=2&pagina=2", Json);

        Assert.Equal((3, 2, 2, 1), (pagina!.Total, pagina.TotalPaginas, pagina.Pagina, pagina.Items.Count));
        await Problema(await mentor.GetAsync("/api/reservas?tamano=0"), HttpStatusCode.BadRequest, "validacion");
    }

    [Fact]
    public async Task Actualizar_exigeIfMatchYDetectaVersionesViejas()
    {
        var reserva = await api.CrearReservaAsync();
        var (mentor, _) = await api.ClienteMentorAsync();
        var url = $"/api/reservas/{reserva.Id}";
        var cambios = Peticion(reserva.Inicio) with { Nombre = "Nombre Corregido" };

        await Problema(await mentor.SendAsync(ConIfMatch(HttpMethod.Put, url, cambios, null)), HttpStatusCode.PreconditionRequired, "version_requerida");
        await Problema(await mentor.SendAsync(ConIfMatch(HttpMethod.Put, url, cambios, "\"99\"")), HttpStatusCode.PreconditionFailed, "version_desactualizada");

        var ok = await mentor.SendAsync(ConIfMatch(HttpMethod.Put, url, cambios, "\"1\""));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("\"2\"", ok.Headers.ETag?.Tag);
        Assert.Equal("Nombre Corregido", (await Leer<ReservaResponse>(ok))!.Nombre);
    }

    [Fact]
    public async Task Actualizar_moverLaReservaLiberaElHorarioAnterior()
    {
        var reserva = await api.CrearReservaAsync();
        var (mentor, _) = await api.ClienteMentorAsync();
        var nuevoInicio = api.NuevoHorario();

        var movida = await mentor.SendAsync(ConIfMatch(HttpMethod.Put, $"/api/reservas/{reserva.Id}", Peticion(nuevoInicio), "\"1\""));

        Assert.Equal(nuevoInicio, (await Leer<ReservaResponse>(movida))!.Inicio);
        await api.CrearReservaAsync(reserva.Inicio); // el horario viejo quedó libre
        await Problema(await api.ClienteAnonimo().PostAsJsonAsync("/api/reservas", Peticion(nuevoInicio)), HttpStatusCode.Conflict, "horario_ocupado");
    }

    [Fact]
    public async Task CambiarEstado_sigueLasTransicionesYCancelarLiberaElHorario()
    {
        var reserva = await api.CrearReservaAsync();
        var (mentor, _) = await api.ClienteMentorAsync();
        var url = $"/api/reservas/{reserva.Id}/estado";

        var confirmada = await mentor.SendAsync(ConIfMatch(HttpMethod.Patch, url, new CambioEstadoRequest("confirmada"), "\"1\""));
        Assert.Equal(EstadoReserva.Confirmada, (await Leer<ReservaResponse>(confirmada))!.Estado);
        await Problema(await mentor.SendAsync(ConIfMatch(HttpMethod.Patch, url, new CambioEstadoRequest("pendiente"), "\"2\"")), HttpStatusCode.BadRequest, "validacion");

        var cancelada = await mentor.SendAsync(ConIfMatch(HttpMethod.Patch, url, new CambioEstadoRequest("cancelada"), "\"2\""));
        Assert.Equal(HttpStatusCode.OK, cancelada.StatusCode);
        await Problema(await mentor.SendAsync(ConIfMatch(HttpMethod.Patch, url, new CambioEstadoRequest("completada"), "\"3\"")), HttpStatusCode.Conflict, "transicion_invalida");
        await Problema(await mentor.SendAsync(ConIfMatch(HttpMethod.Put, $"/api/reservas/{reserva.Id}", Peticion(reserva.Inicio), "\"3\"")), HttpStatusCode.Conflict, "reserva_no_editable");

        await api.CrearReservaAsync(reserva.Inicio); // cancelar liberó el horario
    }

    [Fact]
    public async Task Eliminar_devuelve204YLuego404()
    {
        var reserva = await api.CrearReservaAsync();
        var (mentor, _) = await api.ClienteMentorAsync();
        var url = $"/api/reservas/{reserva.Id}";

        await Problema(await mentor.SendAsync(ConIfMatch(HttpMethod.Delete, url, null, "\"5\"")), HttpStatusCode.PreconditionFailed, "version_desactualizada");
        Assert.Equal(HttpStatusCode.NoContent, (await mentor.DeleteAsync(url)).StatusCode);
        await Problema(await mentor.GetAsync(url), HttpStatusCode.NotFound, "reserva_no_encontrada");
        await Problema(await mentor.DeleteAsync(url), HttpStatusCode.NotFound, "reserva_no_encontrada");
        await api.CrearReservaAsync(reserva.Inicio);
    }

    [Fact]
    public async Task RutasYMetodosDesconocidos_respondenConProblemDetails()
    {
        var cliente = api.ClienteAnonimo();

        await Problema(await cliente.GetAsync("/api/no-existe"), HttpStatusCode.NotFound, "no_encontrado");
        await Problema(await cliente.DeleteAsync("/api/mentorias/1"), HttpStatusCode.MethodNotAllowed, "metodo_no_permitido");
    }
}
