using Mentorias.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Mentorias.Api.Infrastructure;

public static class DatosIniciales
{
    /// <summary>Las mismas seis mentorías que muestra el sitio de la Unidad 1.</summary>
    public static readonly Mentoria[] Mentorias =
    [
        new() { Id = 1, Slug = "logica-algoritmos", Categoria = "fundamentos", DuracionMinutos = 60, Titulo = "Lógica y algoritmos",
            Descripcion = "Del enunciado al pseudocódigo y a la prueba de escritorio, con ejercicios de condicionales y ciclos." },
        new() { Id = 2, Slug = "java-desde-cero", Categoria = "java", DuracionMinutos = 60, Titulo = "Java desde cero",
            Descripcion = "Variables, métodos, arreglos y manejo de entradas para escribir programas que compilan a la primera." },
        new() { Id = 3, Slug = "poo", Categoria = "java", DuracionMinutos = 90, Titulo = "Programación orientada a objetos",
            Descripcion = "Clases, herencia, polimorfismo e interfaces explicados con ejemplos que puedes ejecutar." },
        new() { Id = 4, Slug = "html-css-js", Categoria = "web", DuracionMinutos = 60, Titulo = "HTML, CSS y JavaScript",
            Descripcion = "Páginas semánticas, diseño responsivo y accesible, e interacciones sin dependencias innecesarias." },
        new() { Id = 5, Slug = "sql-modelado", Categoria = "datos", DuracionMinutos = 90, Titulo = "SQL y modelado de datos",
            Descripcion = "Tablas, relaciones y consultas para diseñar una base de datos que no se rompa al crecer." },
        new() { Id = 6, Slug = "proyecto-java", Categoria = "java", DuracionMinutos = 90, Titulo = "Proyecto final en Java",
            Descripcion = "Estructura, organización en paquetes y revisión de código de tu proyecto de fin de ciclo." },
    ];

    /// <summary>Aplica migraciones (con reintentos mientras MySQL arranca) y crea el mentor configurado.</summary>
    public static async Task PrepararBaseDeDatosAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue("BaseDatos:MigrarAlIniciar", true)) return;

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MentoriasDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<MentoriasDbContext>>();

        for (var intento = 1; ; intento++)
        {
            try
            {
                await db.Database.MigrateAsync();
                break;
            }
            catch (Exception ex) when (intento < 15)
            {
                logger.LogWarning("MySQL aún no responde (intento {Intento}): {Mensaje}", intento, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }

        await CrearMentorAsync(db, app.Configuration.GetSection("Mentor"), scope.ServiceProvider, logger);
    }

    private static async Task CrearMentorAsync(
        MentoriasDbContext db, IConfigurationSection config, IServiceProvider services, ILogger logger)
    {
        var correo = config["Correo"]?.Trim().ToLowerInvariant();
        var password = config["Password"];
        if (string.IsNullOrEmpty(correo) || string.IsNullOrEmpty(password))
        {
            logger.LogWarning("No se configuró Mentor:Correo / Mentor:Password; no se creará la cuenta del mentor.");
            return;
        }
        if (await db.Mentores.AnyAsync(m => m.Correo == correo)) return;

        var mentor = new Mentor { Correo = correo, Nombre = config["Nombre"] ?? "Mentor" };
        mentor.PasswordHash = services.GetRequiredService<IPasswordHasher<Mentor>>().HashPassword(mentor, password);
        db.Mentores.Add(mentor);
        await db.SaveChangesAsync();
        logger.LogInformation("Cuenta de mentor creada para {Correo}.", correo);
    }
}
