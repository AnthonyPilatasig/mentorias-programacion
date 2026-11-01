using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace Mentorias.Api.Infrastructure;

public static class ErroresBaseDatos
{
    private const int ClaveDuplicada = 1062; // ER_DUP_ENTRY

    public static bool EsClaveDuplicada(DbUpdateException ex) =>
        ex.InnerException is MySqlException { Number: ClaveDuplicada };
}

/// <summary>Permite ejecutar "dotnet ef migrations" sin una base de datos encendida.</summary>
public sealed class FabricaDisenio : Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory<MentoriasDbContext>
{
    public MentoriasDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<MentoriasDbContext>()
            .UseMySQL("server=localhost;database=mentorias;user=diseno;password=diseno")
            .Options);
}
