using System.Collections.Concurrent;

namespace GestionPedidos.Ui.Tienda;

/// <summary>
/// Entrega la dirección que el hospedaje puede mostrar para una foto de producto o categoría. En la web es la misma ruta relativa;
/// en la aplicación nativa (<see cref="UiOptions.ImagenesComoDatos"/>) descarga la foto del endpoint público de la API y la devuelve como
/// <c>data:</c>, con caché en memoria para no repetir la descarga.
/// </summary>
public sealed class ImagenesService
{
    private readonly IHttpClientFactory _fabrica;
    private readonly UiOptions _opciones;
    private readonly ConcurrentDictionary<string, Task<string?>> _cache = new();

    /// <summary>Crea el servicio.</summary>
    public ImagenesService(IHttpClientFactory fabrica, UiOptions opciones)
    {
        _fabrica = fabrica;
        _opciones = opciones;
    }

    /// <summary>Resuelve la dirección de una foto.</summary>
    /// <param name="ruta">Ruta relativa que entrega la API (<c>/imagenes/productos/CODIGO</c>), o <c>null</c> si no hay foto.</param>
    /// <returns>La dirección a mostrar, o <c>null</c> si no hay foto o no se pudo descargar.</returns>
    public async Task<string?> ResolverAsync(string? ruta)
    {
        if (string.IsNullOrEmpty(ruta) || !_opciones.ImagenesComoDatos) return ruta;
        var resultado = await _cache.GetOrAdd(ruta, DescargarAsync);
        if (resultado is null) _cache.TryRemove(ruta, out _);   // un fallo no se guarda: se reintenta la próxima vez
        return resultado;
    }

    private async Task<string?> DescargarAsync(string ruta)
    {
        try
        {
            using var respuesta = await _fabrica.CreateClient("api").GetAsync("api/v1" + ruta);
            if (!respuesta.IsSuccessStatusCode) return null;
            var bytes = await respuesta.Content.ReadAsByteArrayAsync();
            var tipo = respuesta.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            return $"data:{tipo};base64,{Convert.ToBase64String(bytes)}";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return null; }   // sin foto: queda el ícono de respaldo
    }
}
