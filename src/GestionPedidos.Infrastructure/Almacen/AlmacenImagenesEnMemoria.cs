using System.Collections.Concurrent;
using GestionPedidos.Application.Imagenes;

namespace GestionPedidos.Infrastructure.Almacen;

/// <summary>Almacén de imágenes en memoria: para pruebas y para trabajar sin Garage. Los datos se pierden al apagar la aplicación.</summary>
public sealed class AlmacenImagenesEnMemoria : IAlmacenImagenes
{
    private readonly ConcurrentDictionary<string, (byte[] Datos, string Tipo, DateTimeOffset Fecha)> _objetos = new();

    /// <summary>Cantidad de objetos guardados (para las pruebas).</summary>
    public int Cantidad => _objetos.Count;

    /// <summary>Claves guardadas (para las pruebas).</summary>
    public IReadOnlyCollection<string> Claves => _objetos.Keys.ToList();

    /// <inheritdoc/>
    public async Task GuardarAsync(string clave, Stream contenido, string tipoContenido, CancellationToken ct)
    {
        using var m = new MemoryStream();
        await contenido.CopyToAsync(m, ct);
        _objetos[clave] = (m.ToArray(), tipoContenido, DateTimeOffset.UtcNow);
    }

    /// <inheritdoc/>
    public Task<ImagenAlmacenada?> ObtenerAsync(string clave, CancellationToken ct) =>
        Task.FromResult(_objetos.TryGetValue(clave, out var o)
            ? new ImagenAlmacenada(new MemoryStream(o.Datos), o.Tipo, o.Datos.Length, Convert.ToHexString(System.Security.Cryptography.MD5.HashData(o.Datos)), o.Fecha)
            : null);

    /// <inheritdoc/>
    public Task EliminarAsync(string clave, CancellationToken ct) { _objetos.TryRemove(clave, out _); return Task.CompletedTask; }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> ListarAsync(string prefijo, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>(_objetos.Keys.Where(k => k.StartsWith(prefijo, StringComparison.Ordinal)).OrderBy(k => k).ToList());
}
