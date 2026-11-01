using Mentorias.Api.Contracts;
using Mentorias.Api.Domain;
using Mentorias.Api.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Mentorias.Api.Application;

public sealed class AuthService(
    MentoriasDbContext db, IPasswordHasher<Mentor> hasher, IMemoryCache intentos, TimeProvider reloj)
{
    // Si el correo no existe igual se verifica un hash, para que el tiempo de respuesta
    // no revele qué correos están registrados.
    private static readonly Mentor Ficticio = new() { Correo = "", Nombre = "" };
    private static readonly string HashFicticio = new PasswordHasher<Mentor>().HashPassword(Ficticio, Guid.NewGuid().ToString());

    public async Task<Resultado<Mentor>> ValidarCredencialesAsync(LoginRequest request, CancellationToken ct)
    {
        var validacion = Validadores.Login(request);
        if (!validacion.EsValida) return Errores.Validacion(validacion.Errores);
        var (correo, password) = validacion.Valor;

        var clave = $"login:{correo}";
        var ahora = reloj.GetUtcNow();
        if (ReglasLogin.TiempoRestante(intentos.Get<IntentosLogin>(clave), ahora) is { } restante)
            return Errores.CuentaBloqueada(restante);

        var mentor = await db.Mentores.AsNoTracking().FirstOrDefaultAsync(m => m.Correo == correo, ct);
        var verificacion = hasher.VerifyHashedPassword(mentor ?? Ficticio, mentor?.PasswordHash ?? HashFicticio, password);

        if (mentor is null || verificacion == PasswordVerificationResult.Failed)
        {
            intentos.Set(clave, ReglasLogin.RegistrarFallo(intentos.Get<IntentosLogin>(clave), ahora), ReglasLogin.DuracionBloqueo);
            return Errores.CredencialesInvalidas;
        }

        intentos.Remove(clave);
        return mentor;
    }
}
