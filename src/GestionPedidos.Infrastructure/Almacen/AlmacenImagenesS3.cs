using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using GestionPedidos.Application.Imagenes;
using Microsoft.Extensions.Options;

namespace GestionPedidos.Infrastructure.Almacen;

/// <summary>Configuración del almacén de objetos (sección <c>Almacen</c>): Garage en desarrollo, cualquier servicio compatible con S3 en producción.</summary>
public sealed class OpcionesAlmacen
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string Seccion = "Almacen";
    /// <summary>Dirección del servicio S3 (por ejemplo <c>http://garage:3900</c>).</summary>
    public string Endpoint { get; set; } = "http://localhost:4393";
    /// <summary>Región S3 (Garage usa <c>garage</c>).</summary>
    public string Region { get; set; } = "garage";
    /// <summary>Nombre del bucket donde viven las imágenes.</summary>
    public string Bucket { get; set; } = "gp-imagenes";
    /// <summary>Clave de acceso.</summary>
    public string AccessKey { get; set; } = "";
    /// <summary>Clave secreta.</summary>
    public string SecretKey { get; set; } = "";
}

/// <summary>Almacén de imágenes sobre S3 (Garage). Usa direcciones tipo ruta (<c>endpoint/bucket/clave</c>), como exige Garage.</summary>
public sealed class AlmacenImagenesS3 : IAlmacenImagenes, IDisposable
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;

    /// <summary>Crea el cliente S3 a partir de la configuración.</summary>
    public AlmacenImagenesS3(IOptions<OpcionesAlmacen> opciones)
    {
        var o = opciones.Value;
        _bucket = o.Bucket;
        _s3 = new AmazonS3Client(new BasicAWSCredentials(o.AccessKey, o.SecretKey), new AmazonS3Config
        {
            ServiceURL = o.Endpoint, ForcePathStyle = true, AuthenticationRegion = o.Region,
        });
    }

    /// <inheritdoc/>
    public async Task GuardarAsync(string clave, Stream contenido, string tipoContenido, CancellationToken ct) =>
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket, Key = clave, InputStream = contenido, ContentType = tipoContenido, AutoCloseStream = false,
            UseChunkEncoding = false,   // Garage acepta la firma simple; sin trozos firmados
        }, ct);

    /// <inheritdoc/>
    public async Task<ImagenAlmacenada?> ObtenerAsync(string clave, CancellationToken ct)
    {
        try
        {
            var r = await _s3.GetObjectAsync(_bucket, clave, ct);
            return new ImagenAlmacenada(r.ResponseStream, r.Headers.ContentType ?? "application/octet-stream", r.ContentLength, r.ETag, r.LastModified);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { return null; }
    }

    /// <inheritdoc/>
    public async Task EliminarAsync(string clave, CancellationToken ct) => await _s3.DeleteObjectAsync(_bucket, clave, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ListarAsync(string prefijo, CancellationToken ct)
    {
        var claves = new List<string>();
        string? continuar = null;
        do
        {
            var r = await _s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = _bucket, Prefix = prefijo, ContinuationToken = continuar }, ct);
            claves.AddRange(r.S3Objects.Select(o => o.Key));
            continuar = r.IsTruncated ? r.NextContinuationToken : null;
        } while (continuar is not null);
        return claves;
    }

    /// <summary>Libera el cliente S3.</summary>
    public void Dispose() => _s3.Dispose();
}
