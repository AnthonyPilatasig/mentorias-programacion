using System.Security.Claims;
using System.Security.Cryptography;
using Mentorias.Api.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace Mentorias.Api.Infrastructure;

/// <summary>
/// Sesiones del lado del servidor: la cookie solo transporta un identificador aleatorio y
/// el ticket se guarda en MySQL. Cerrar sesión borra la fila, así una cookie copiada deja
/// de servir de inmediato (con cookies "autocontenidas" seguiría siendo válida hasta expirar).
/// </summary>
public sealed class AlmacenSesiones(IServiceScopeFactory scopes, TimeProvider reloj) : ITicketStore
{
    private static readonly TimeSpan DuracionPorDefecto = TimeSpan.FromHours(2);

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var id = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MentoriasDbContext>();
        db.Sesiones.Add(new Sesion
        {
            Id = id,
            MentorId = int.Parse(ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier)!),
            Ticket = TicketSerializer.Default.Serialize(ticket),
            CreadaUtc = reloj.GetUtcNow().UtcDateTime,
            ExpiraUtc = Expiracion(ticket),
        });
        await db.SaveChangesAsync();
        return id;
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MentoriasDbContext>();
        await db.Sesiones.Where(s => s.Id == key).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Ticket, TicketSerializer.Default.Serialize(ticket))
            .SetProperty(x => x.ExpiraUtc, Expiracion(ticket)));
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MentoriasDbContext>();
        var ahora = reloj.GetUtcNow().UtcDateTime;
        var sesion = await db.Sesiones.AsNoTracking().FirstOrDefaultAsync(s => s.Id == key && s.ExpiraUtc > ahora);
        return sesion is null ? null : TicketSerializer.Default.Deserialize(sesion.Ticket);
    }

    public async Task RemoveAsync(string key)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MentoriasDbContext>();
        var ahora = reloj.GetUtcNow().UtcDateTime;
        // Además de la sesión cerrada, limpia las que ya expiraron.
        await db.Sesiones.Where(s => s.Id == key || s.ExpiraUtc <= ahora).ExecuteDeleteAsync();
    }

    private DateTime Expiracion(AuthenticationTicket ticket) =>
        ticket.Properties.ExpiresUtc?.UtcDateTime ?? reloj.GetUtcNow().UtcDateTime.Add(DuracionPorDefecto);
}
