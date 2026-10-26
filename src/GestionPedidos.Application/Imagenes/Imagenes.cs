using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Contracts;

namespace GestionPedidos.Application.Imagenes;

/// <summary>A qué pertenece una imagen.</summary>
public enum TipoImagen
{
    /// <summary>Producto (<c>productos/CODIGO.jpg</c>).</summary>
    Producto,
    /// <summary>Categoría (<c>categorias/CODIGO.jpg</c>).</summary>
    Categoria,
}

/// <summary>Reglas de nombres de las imágenes: la clave se arma con el <b>código</b> de lo que ilustra, nunca con el id ni con un nombre al azar.</summary>
public static class ClavesDeImagen
{
    /// <summary>Carpeta (prefijo) del tipo en el almacén.</summary>
    public static string Prefijo(TipoImagen tipo) => tipo == TipoImagen.Producto ? "productos" : "categorias";

    /// <summary>Tipo a partir de la carpeta de la dirección (<c>productos</c> o <c>categorias</c>), o <c>null</c>.</summary>
    public static TipoImagen? Tipo(string? carpeta) => carpeta switch { "productos" => TipoImagen.Producto, "categorias" => TipoImagen.Categoria, _ => null };

    /// <summary>Clave en el almacén: <c>productos/ROP-CAM-001.jpg</c>.</summary>
    public static string Clave(TipoImagen tipo, string codigo, string extension) => $"{Prefijo(tipo)}/{codigo}.{extension}";

    /// <summary>Indica si el código tiene un formato seguro (letras, números, guion y guion bajo): evita rutas raras en el almacén.</summary>
    public static bool CodigoValido(string? codigo) =>
        !string.IsNullOrEmpty(codigo) && codigo.Length <= 30 && codigo.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    /// <summary>Código contenido en una clave (<c>productos/ROP-CAM-001.jpg</c> → <c>ROP-CAM-001</c>), o <c>null</c> si no tiene la forma esperada.</summary>
    public static string? CodigoDe(string clave)
    {
        var archivo = clave[(clave.LastIndexOf('/') + 1)..];
        var punto = archivo.LastIndexOf('.');
        var codigo = punto > 0 ? archivo[..punto] : archivo;
        return CodigoValido(codigo) ? codigo : null;
    }
}

/// <summary>Una imagen leída del almacén.</summary>
/// <param name="Contenido">Bytes de la imagen (quien la reciba debe liberarlo).</param>
/// <param name="TipoContenido">Tipo MIME.</param>
/// <param name="Longitud">Tamaño en bytes.</param>
/// <param name="ETag">Huella para el caché del navegador.</param>
/// <param name="Modificada">Última modificación.</param>
public sealed record ImagenAlmacenada(Stream Contenido, string TipoContenido, long Longitud, string? ETag, DateTimeOffset? Modificada);

/// <summary>Puerto del almacén de objetos (S3 / Garage) donde viven las imágenes.</summary>
public interface IAlmacenImagenes
{
    /// <summary>Guarda (o reemplaza) un objeto.</summary>
    Task GuardarAsync(string clave, Stream contenido, string tipoContenido, CancellationToken ct);
    /// <summary>Lee un objeto, o <c>null</c> si no existe.</summary>
    Task<ImagenAlmacenada?> ObtenerAsync(string clave, CancellationToken ct);
    /// <summary>Borra un objeto (no falla si no existe).</summary>
    Task EliminarAsync(string clave, CancellationToken ct);
    /// <summary>Claves de los objetos que empiezan con el prefijo.</summary>
    Task<IReadOnlyList<string>> ListarAsync(string prefijo, CancellationToken ct);
}

/// <summary>Datos de lo que tiene (o va a tener) una imagen.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="Codigo">Código estable.</param>
/// <param name="Nombre">Nombre visible.</param>
/// <param name="ImagenClave">Clave actual de la imagen, o <c>null</c>.</param>
public sealed record DestinoDeImagen(int Id, string Codigo, string Nombre, string? ImagenClave);

/// <summary>Puerto de la base de datos para las imágenes: qué clave tiene cada producto o categoría.</summary>
public interface IImagenesRepository
{
    /// <summary>Destino por id (con seguimiento), o <c>null</c>.</summary>
    Task<DestinoDeImagen?> ObtenerDestinoAsync(TipoImagen tipo, int id, CancellationToken ct);
    /// <summary>Clave de la imagen según el código, o <c>null</c> si no existe o no tiene imagen.</summary>
    Task<string?> ObtenerClavePorCodigoAsync(TipoImagen tipo, string codigo, CancellationToken ct);
    /// <summary>Fija (o borra, con <c>null</c>) la clave de la imagen del destino.</summary>
    Task EstablecerClaveAsync(TipoImagen tipo, int id, string? clave, CancellationToken ct);
    /// <summary>Todos los destinos por código (para reenlazar imágenes).</summary>
    Task<IReadOnlyDictionary<string, DestinoDeImagen>> PorCodigoAsync(TipoImagen tipo, CancellationToken ct);
}

/// <summary>Caso de uso: la imagen de un producto o categoría para mostrarla en la tienda (pública).</summary>
public sealed class ObtenerImagen
{
    private readonly IImagenesRepository _repo;
    private readonly IAlmacenImagenes _almacen;

    /// <summary>Crea el caso de uso.</summary>
    public ObtenerImagen(IImagenesRepository repo, IAlmacenImagenes almacen) { _repo = repo; _almacen = almacen; }

    /// <summary>Busca la clave en la base (nunca se usa una ruta que mande quien llama) y lee la imagen.</summary>
    public async Task<Resultado<ImagenAlmacenada>> EjecutarAsync(string? carpeta, string? codigo, CancellationToken ct)
    {
        static Resultado<ImagenAlmacenada> NoHay() => Resultado<ImagenAlmacenada>.Falla(TipoError.NoEncontrado, CodigosError.NoEncontrado, "La imagen no existe.");
        if (ClavesDeImagen.Tipo(carpeta) is not { } tipo || !ClavesDeImagen.CodigoValido(codigo)) return NoHay();
        var clave = await _repo.ObtenerClavePorCodigoAsync(tipo, codigo!, ct);
        if (clave is null) return NoHay();
        var imagen = await _almacen.ObtenerAsync(clave, ct);
        return imagen is null ? NoHay() : Resultado<ImagenAlmacenada>.Ok(imagen);
    }
}

/// <summary>Casos de uso del administrador: subir, quitar y reenlazar imágenes.</summary>
public sealed class GestionarImagenes
{
    /// <summary>Tamaño máximo de una imagen subida (2 MB).</summary>
    public const long TamanoMaximo = 2 * 1024 * 1024;

    private readonly IImagenesRepository _repo;
    private readonly IAlmacenImagenes _almacen;
    private readonly IUnitOfWork _uow;
    private readonly EventosAplicacion _eventos;

    /// <summary>Crea el caso de uso.</summary>
    public GestionarImagenes(IImagenesRepository repo, IAlmacenImagenes almacen, IUnitOfWork uow, EventosAplicacion eventos)
    {
        _repo = repo; _almacen = almacen; _uow = uow; _eventos = eventos;
    }

    private static string NombreEntidad(TipoImagen t) => t == TipoImagen.Producto ? "Producto" : "Categoria";

    /// <summary>Detecta el formato por los primeros bytes (no por lo que diga el nombre ni el tipo declarado, que se pueden falsear).</summary>
    public static (string Extension, string TipoContenido)? Detectar(ReadOnlySpan<byte> b) =>
        b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF ? ("jpg", "image/jpeg")
        : b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 ? ("png", "image/png")
        : b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P' ? ("webp", "image/webp")
        : null;

    /// <summary>Guarda la imagen con el nombre <c>{carpeta}/{CODIGO}.{ext}</c> y la liga al producto o categoría.</summary>
    public async Task<Resultado<string>> SubirAsync(TipoImagen tipo, int id, Stream contenido, int actorId, CancellationToken ct)
    {
        var destino = await _repo.ObtenerDestinoAsync(tipo, id, ct);
        if (destino is null)
            return Resultado<string>.Falla(TipoError.NoEncontrado, CodigosError.NoEncontrado, tipo == TipoImagen.Producto ? "El producto no existe." : "La categoría no existe.");

        // Se lee con tope (+1 byte para saber si se pasó) antes de tocar el almacén
        using var memoria = new MemoryStream();
        var buffer = new byte[81920];
        int leidos;
        while ((leidos = await contenido.ReadAsync(buffer, ct)) > 0)
        {
            memoria.Write(buffer, 0, leidos);
            if (memoria.Length > TamanoMaximo)
                return Resultado<string>.Falla(TipoError.Validacion, CodigosError.Validacion, "La imagen no puede pesar más de 2 MB.");
        }
        if (memoria.Length == 0) return Resultado<string>.Falla(TipoError.Validacion, CodigosError.Validacion, "El archivo está vacío.");
        if (Detectar(memoria.GetBuffer().AsSpan(0, (int)memoria.Length)) is not { } formato)
            return Resultado<string>.Falla(TipoError.Validacion, CodigosError.Validacion, "El archivo no es una imagen JPEG, PNG o WebP.");

        var clave = ClavesDeImagen.Clave(tipo, destino.Codigo, formato.Extension);
        memoria.Position = 0;
        await _almacen.GuardarAsync(clave, memoria, formato.TipoContenido, ct);
        if (destino.ImagenClave is { } anterior && anterior != clave) await _almacen.EliminarAsync(anterior, ct);   // no quedan huérfanas al cambiar de formato
        await _repo.EstablecerClaveAsync(tipo, id, clave, ct);
        await _uow.GuardarAsync(ct);

        _eventos.OnEntidadCambiada(new EntidadCambiadaServidorEventArgs
        {
            Entidad = NombreEntidad(tipo), EntidadId = id, Tipo = TipoCambioServidor.Actualizada, UsuarioId = actorId,
            Detalle = $"Imagen de {destino.Nombre} ({destino.Codigo}) actualizada: {clave}",
        });
        return Resultado<string>.Ok(clave);
    }

    /// <summary>Quita la imagen del producto o categoría (borra el objeto y limpia la clave).</summary>
    public async Task<Resultado> QuitarAsync(TipoImagen tipo, int id, int actorId, CancellationToken ct)
    {
        var destino = await _repo.ObtenerDestinoAsync(tipo, id, ct);
        if (destino is null)
            return Resultado.Falla(TipoError.NoEncontrado, CodigosError.NoEncontrado, tipo == TipoImagen.Producto ? "El producto no existe." : "La categoría no existe.");
        if (destino.ImagenClave is null) return Resultado.Ok();

        await _almacen.EliminarAsync(destino.ImagenClave, ct);
        await _repo.EstablecerClaveAsync(tipo, id, null, ct);
        await _uow.GuardarAsync(ct);
        _eventos.OnEntidadCambiada(new EntidadCambiadaServidorEventArgs
        {
            Entidad = NombreEntidad(tipo), EntidadId = id, Tipo = TipoCambioServidor.Actualizada, UsuarioId = actorId,
            Detalle = $"Imagen de {destino.Nombre} ({destino.Codigo}) eliminada",
        });
        return Resultado.Ok();
    }

    /// <summary>
    /// Recuperación: recorre el almacén y vuelve a ligar cada imagen con el producto o categoría cuyo <b>código</b> aparece en su
    /// nombre. Sirve cuando se restaura la base (o se pierde el campo de la imagen) pero las imágenes siguen en el almacén.
    /// </summary>
    public async Task<ReenlaceImagenesDto> ReenlazarAsync(int? actorId, CancellationToken ct)
    {
        int enlazadas = 0, yaEnlazadas = 0, huerfanas = 0;
        foreach (var tipo in new[] { TipoImagen.Producto, TipoImagen.Categoria })
        {
            var destinos = await _repo.PorCodigoAsync(tipo, ct);
            foreach (var clave in await _almacen.ListarAsync(ClavesDeImagen.Prefijo(tipo) + "/", ct))
            {
                var codigo = ClavesDeImagen.CodigoDe(clave);
                if (codigo is null || !destinos.TryGetValue(codigo, out var destino)) { huerfanas++; continue; }
                if (destino.ImagenClave == clave) { yaEnlazadas++; continue; }
                await _repo.EstablecerClaveAsync(tipo, destino.Id, clave, ct);
                enlazadas++;
            }
        }
        if (enlazadas > 0)
        {
            await _uow.GuardarAsync(ct);
            _eventos.OnEntidadCambiada(new EntidadCambiadaServidorEventArgs
            {
                Entidad = "Imagen", Tipo = TipoCambioServidor.Actualizada, UsuarioId = actorId,
                Detalle = $"Imágenes reenlazadas por código: {enlazadas} nuevas, {yaEnlazadas} ya estaban, {huerfanas} sin producto ni categoría",
            });
        }
        return new ReenlaceImagenesDto(enlazadas, yaEnlazadas, huerfanas);
    }
}
