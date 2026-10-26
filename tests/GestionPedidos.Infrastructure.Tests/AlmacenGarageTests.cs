using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using GestionPedidos.Infrastructure.Almacen;
using Microsoft.Extensions.Options;

namespace GestionPedidos.Infrastructure.Tests;

/// <summary>Garage real (el mismo que usa docker-compose) en un contenedor: guardar, leer, listar y borrar imágenes por clave.</summary>
public sealed class GarageFixture : IAsyncLifetime
{
    private const string Acceso = "GK6a1f0c3e9b27d84a5e10c9f3";
    private const string Secreto = "7c2e5a91d04b6f38a1e9c05d72b4f863e0a19d5c47b2f08e3a6d915c8b0e47a2";
    private IContainer? _contenedor;

    /// <summary>Almacén conectado al Garage del contenedor.</summary>
    public AlmacenImagenesS3 Almacen { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        // La misma configuración que usa docker-compose (docker/garage/garage.toml)
        var raiz = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(raiz, "docker", "garage", "garage.toml"))) raiz = Path.GetDirectoryName(raiz)!;
        _contenedor = new ContainerBuilder()
            .WithImage("dxflrs/garage:v2.5.0")
            .WithResourceMapping(await File.ReadAllBytesAsync(Path.Combine(raiz, "docker", "garage", "garage.toml")), "/etc/garage.toml")
            .WithEnvironment("GARAGE_DEFAULT_ACCESS_KEY", Acceso).WithEnvironment("GARAGE_DEFAULT_SECRET_KEY", Secreto).WithEnvironment("GARAGE_DEFAULT_BUCKET", "gp-imagenes-pruebas")
            .WithCommand("/garage", "server", "--single-node", "--default-bucket")
            .WithPortBinding(3900, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("S3 API server listening"))
            .Build();
        await _contenedor.StartAsync();
        await Task.Delay(1500);   // el diseño del clúster de un nodo termina de asentarse poco después de abrir el puerto
        Almacen = new AlmacenImagenesS3(Options.Create(new OpcionesAlmacen
        {
            Endpoint = $"http://{_contenedor.Hostname}:{_contenedor.GetMappedPublicPort(3900)}", Bucket = "gp-imagenes-pruebas", AccessKey = Acceso, SecretKey = Secreto,
        }));
    }

    public async Task DisposeAsync()
    {
        Almacen?.Dispose();
        if (_contenedor is not null) await _contenedor.DisposeAsync();
    }
}

public class AlmacenGarageTests : IClassFixture<GarageFixture>
{
    private readonly GarageFixture _garage;
    public AlmacenGarageTests(GarageFixture garage) => _garage = garage;

    private static MemoryStream Bytes(string texto) => new(System.Text.Encoding.UTF8.GetBytes(texto));

    [Fact]
    public async Task GuardarYObtener_DevuelveLosMismosBytes_ConSuTipoYEtag()
    {
        await _garage.Almacen.GuardarAsync("productos/PRB-001.jpg", Bytes("contenido de prueba"), "image/jpeg", default);

        var imagen = await _garage.Almacen.ObtenerAsync("productos/PRB-001.jpg", default);
        Assert.NotNull(imagen);
        await using var flujo = imagen!.Contenido;
        using var lector = new StreamReader(flujo);
        Assert.Equal("contenido de prueba", await lector.ReadToEndAsync());
        Assert.Equal("image/jpeg", imagen.TipoContenido);
        Assert.Equal(19, imagen.Longitud);
        Assert.False(string.IsNullOrEmpty(imagen.ETag));
    }

    [Fact]
    public async Task UnObjetoQueNoExiste_DevuelveNulo_YBorrarlo_NoFalla()
    {
        Assert.Null(await _garage.Almacen.ObtenerAsync("productos/NO-EXISTE.jpg", default));
        await _garage.Almacen.EliminarAsync("productos/NO-EXISTE.jpg", default);
    }

    [Fact]
    public async Task Reemplazar_Listar_YEliminar()
    {
        await _garage.Almacen.GuardarAsync("categorias/PRB.jpg", Bytes("uno"), "image/jpeg", default);
        await _garage.Almacen.GuardarAsync("categorias/PRB.jpg", Bytes("dos dos"), "image/jpeg", default);   // reemplaza
        await _garage.Almacen.GuardarAsync("categorias/PRC.png", Bytes("x"), "image/png", default);
        await _garage.Almacen.GuardarAsync("productos/PRC-001.jpg", Bytes("y"), "image/jpeg", default);

        var categorias = await _garage.Almacen.ListarAsync("categorias/", default);
        Assert.Contains("categorias/PRB.jpg", categorias);
        Assert.Contains("categorias/PRC.png", categorias);
        Assert.DoesNotContain("productos/PRC-001.jpg", categorias);   // el prefijo separa las carpetas

        await using (var leida = (await _garage.Almacen.ObtenerAsync("categorias/PRB.jpg", default))!.Contenido)
            Assert.Equal("dos dos", await new StreamReader(leida).ReadToEndAsync());

        await _garage.Almacen.EliminarAsync("categorias/PRB.jpg", default);
        Assert.Null(await _garage.Almacen.ObtenerAsync("categorias/PRB.jpg", default));
        Assert.DoesNotContain("categorias/PRB.jpg", await _garage.Almacen.ListarAsync("categorias/", default));
    }
}
