namespace Mentorias.Api.Domain;

// Modelo de persistencia. Las decisiones de negocio no viven aquí: están en las
// funciones puras de Agenda, ReglasReserva y ReglasArchivo.

public enum EstadoReserva { Pendiente, Confirmada, Completada, Cancelada }

public enum NivelEstudiante { Inicial, Intermedio }

public class Mentoria
{
    public int Id { get; set; }
    public required string Slug { get; set; }
    public required string Titulo { get; set; }
    public required string Descripcion { get; set; }
    public required string Categoria { get; set; }
    public int DuracionMinutos { get; set; }
    public bool Activa { get; set; } = true;
}

public class Reserva
{
    public int Id { get; set; }
    public int MentoriaId { get; set; }
    public Mentoria? Mentoria { get; set; }
    public required string Nombre { get; set; }
    public required string Correo { get; set; }
    public NivelEstudiante Nivel { get; set; }
    public string? Mensaje { get; set; }
    public DateTime InicioUtc { get; set; }
    public DateTime FinUtc { get; set; }
    public EstadoReserva Estado { get; set; }

    /// <summary>Token de concurrencia optimista; se expone como ETag.</summary>
    public int Version { get; set; }

    public DateTime CreadaUtc { get; set; }
    public DateTime ActualizadaUtc { get; set; }
    public List<BloqueAgenda> Bloques { get; set; } = [];
    public List<Adjunto> Adjuntos { get; set; } = [];
}

/// <summary>
/// Bloque de 30 minutos ocupado por una reserva. Su clave primaria es el instante de
/// inicio, así la base de datos impide que dos reservas activas se solapen.
/// </summary>
public class BloqueAgenda
{
    public DateTime InicioUtc { get; set; }
    public int ReservaId { get; set; }
}

public class Adjunto
{
    public Guid Id { get; set; }
    public int ReservaId { get; set; }
    public required string NombreOriginal { get; set; }
    public required string TipoContenido { get; set; }
    public long TamanoBytes { get; set; }
    public required string Sha256 { get; set; }
    public DateTime CreadoUtc { get; set; }
}

public class Mentor
{
    public int Id { get; set; }
    public required string Correo { get; set; }
    public required string Nombre { get; set; }
    public string PasswordHash { get; set; } = "";
}

/// <summary>Sesión de servidor: la cookie solo guarda el Id; el ticket vive aquí.</summary>
public class Sesion
{
    public required string Id { get; set; }
    public int MentorId { get; set; }
    public required byte[] Ticket { get; set; }
    public DateTime CreadaUtc { get; set; }
    public DateTime ExpiraUtc { get; set; }
}
