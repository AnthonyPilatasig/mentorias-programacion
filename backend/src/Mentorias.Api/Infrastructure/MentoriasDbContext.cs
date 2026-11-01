using Mentorias.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mentorias.Api.Infrastructure;

public sealed class MentoriasDbContext(DbContextOptions<MentoriasDbContext> options) : DbContext(options)
{
    public DbSet<Mentoria> Mentorias => Set<Mentoria>();
    public DbSet<Reserva> Reservas => Set<Reserva>();
    public DbSet<BloqueAgenda> Bloques => Set<BloqueAgenda>();
    public DbSet<Adjunto> Adjuntos => Set<Adjunto>();
    public DbSet<Mentor> Mentores => Set<Mentor>();
    public DbSet<Sesion> Sesiones => Set<Sesion>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // MySQL no guarda la zona: todo se almacena en UTC y se marca como tal al leer.
        builder.Properties<DateTime>().HaveConversion<ConversorUtc>();
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Mentoria>(e =>
        {
            e.Property(m => m.Slug).HasMaxLength(60);
            e.Property(m => m.Titulo).HasMaxLength(100);
            e.Property(m => m.Descripcion).HasMaxLength(300);
            e.Property(m => m.Categoria).HasMaxLength(30);
            e.HasIndex(m => m.Slug).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("CK_Mentoria_Duracion", "DuracionMinutos IN (30, 60, 90, 120)"));
            e.HasData(DatosIniciales.Mentorias);
        });

        model.Entity<Reserva>(e =>
        {
            e.Property(r => r.Nombre).HasMaxLength(80);
            e.Property(r => r.Correo).HasMaxLength(120);
            e.Property(r => r.Mensaje).HasMaxLength(500);
            e.Property(r => r.Nivel).HasConversion<string>().HasMaxLength(20);
            e.Property(r => r.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(r => r.Version).IsConcurrencyToken();
            e.HasOne(r => r.Mentoria).WithMany().HasForeignKey(r => r.MentoriaId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(r => r.Bloques).WithOne().HasForeignKey(b => b.ReservaId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(r => r.Adjuntos).WithOne().HasForeignKey(a => a.ReservaId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => r.InicioUtc);
            e.HasIndex(r => new { r.Estado, r.InicioUtc });
            e.HasIndex(r => r.Correo);
        });

        model.Entity<BloqueAgenda>(e =>
        {
            e.ToTable("BloquesAgenda");
            e.HasKey(b => b.InicioUtc);
        });

        model.Entity<Adjunto>(e =>
        {
            e.Property(a => a.NombreOriginal).HasMaxLength(150);
            e.Property(a => a.TipoContenido).HasMaxLength(100);
            e.Property(a => a.Sha256).HasMaxLength(64).IsFixedLength();
        });

        model.Entity<Mentor>(e =>
        {
            e.ToTable("Mentores");
            e.Property(m => m.Correo).HasMaxLength(120);
            e.Property(m => m.Nombre).HasMaxLength(80);
            e.Property(m => m.PasswordHash).HasMaxLength(200);
            e.HasIndex(m => m.Correo).IsUnique();
        });

        model.Entity<Sesion>(e =>
        {
            e.ToTable("Sesiones");
            e.Property(s => s.Id).HasMaxLength(64);
            e.HasOne<Mentor>().WithMany().HasForeignKey(s => s.MentorId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => s.ExpiraUtc);
        });
    }

    private sealed class ConversorUtc() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
