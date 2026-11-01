using Mentorias.Api.Configuracion;
using Mentorias.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMentoriasApi(builder.Configuration);

var app = builder.Build();
app.UseMentoriasApi();
await app.PrepararBaseDeDatosAsync();
app.Run();

// Expuesto para las pruebas de integración (WebApplicationFactory).
public partial class Program;
