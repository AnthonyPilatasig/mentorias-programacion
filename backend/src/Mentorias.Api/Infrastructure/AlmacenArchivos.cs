using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Mentorias.Api.Infrastructure;

public sealed class OpcionesAlmacen
{
    /// <summary>Carpeta fuera de wwwroot: los adjuntos nunca se sirven como archivos estáticos.</summary>
    public string Ruta { get; set; } = "App_Data/adjuntos";
}

public interface IAlmacenArchivos
{
    /// <summary>Guarda el contenido y devuelve su hash SHA-256 en hexadecimal.</summary>
    Task<string> GuardarAsync(Guid id, Stream contenido, CancellationToken ct);
    Stream? Abrir(Guid id);
    void Eliminar(Guid id);
}

public sealed class AlmacenArchivosLocal : IAlmacenArchivos
{
    private readonly string _raiz;

    public AlmacenArchivosLocal(IOptions<OpcionesAlmacen> opciones, IHostEnvironment env)
    {
        _raiz = Path.GetFullPath(Path.Combine(env.ContentRootPath, opciones.Value.Ruta));
        Directory.CreateDirectory(_raiz);
    }

    // El nombre en disco es el Guid: el nombre que envía el cliente nunca forma parte de la ruta.
    private string RutaDe(Guid id) => Path.Combine(_raiz, id.ToString("N"));

    public async Task<string> GuardarAsync(Guid id, Stream contenido, CancellationToken ct)
    {
        var temporal = RutaDe(id) + ".tmp";
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        try
        {
            await using (var destino = new FileStream(temporal, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                int leidos;
                while ((leidos = await contenido.ReadAsync(buffer, ct)) > 0)
                {
                    hash.AppendData(buffer, 0, leidos);
                    await destino.WriteAsync(buffer.AsMemory(0, leidos), ct);
                }
            }
            File.Move(temporal, RutaDe(id));
            return Convert.ToHexStringLower(hash.GetHashAndReset());
        }
        finally
        {
            File.Delete(temporal);
        }
    }

    public Stream? Abrir(Guid id) =>
        File.Exists(RutaDe(id))
            ? new FileStream(RutaDe(id), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;

    public void Eliminar(Guid id) => File.Delete(RutaDe(id));
}
