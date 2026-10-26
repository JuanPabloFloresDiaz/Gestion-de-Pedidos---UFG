using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Catalogo;
using GestionPedidos.Domain.Productos;
using GestionPedidos.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace GestionPedidos.Api.Tests;

/// <summary>Imágenes por código: subir, servir con caché, quitar y reenlazar tras restaurar la base.</summary>
[Collection(ApiCollection.Nombre)]
public class ImagenesApiTests
{
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 };
    private static readonly byte[] Jpg = { 0xFF, 0xD8, 0xFF, 0xE0, 5, 6, 7, 8 };

    private readonly ApiFixture _api;
    public ImagenesApiTests(ApiFixture api) => _api = api;

    private static string Sufijo() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static MultipartFormDataContent Archivo(byte[] bytes, string nombre = "foto.png", string tipo = "image/png")
    {
        var contenido = new ByteArrayContent(bytes);
        contenido.Headers.ContentType = new MediaTypeHeaderValue(tipo);
        return new MultipartFormDataContent { { contenido, "archivo", nombre } };
    }

    private async Task<(int Id, string Codigo)> CategoriaAsync()
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GestionPedidosDbContext>();
        var c = new Categoria { Codigo = "T" + Sufijo(), Nombre = "Categoría " + Sufijo() };
        db.Categorias.Add(c);
        await db.SaveChangesAsync();
        return (c.Id, c.Codigo);
    }

    private async Task<(int Id, string Codigo)> ProductoAsync(string? clave = null)
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GestionPedidosDbContext>();
        var p = new Producto { Codigo = "TST-" + Sufijo(), Nombre = "Prod " + Sufijo(), Descripcion = "d", CantidadStock = 3, PrecioVenta = 5, ImagenClave = clave };
        db.Productos.Add(p);
        await db.SaveChangesAsync();
        return (p.Id, p.Codigo);
    }

    [Fact]
    public async Task SubirImagenDeCategoria_LaGuardaConElCodigo_YLaTiendaLaServidaConCache()
    {
        var admin = await _api.AdminAsync();
        var (id, codigo) = await CategoriaAsync();

        var r = await admin.PutAsync($"/api/v1/categorias/{id}/imagen", Archivo(Png));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal($"categorias/{codigo}.png", await r.Content.ReadFromJsonAsync<string>());
        Assert.Contains($"categorias/{codigo}.png", _api.Almacen.Claves);

        var anonimo = _api.CreateClient();   // sin sesión: la tienda la muestra a cualquiera
        var get = await anonimo.GetAsync($"/api/v1/imagenes/categorias/{codigo}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal("image/png", get.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Png, await get.Content.ReadAsByteArrayAsync());
        Assert.Contains("max-age", get.Headers.CacheControl!.ToString());
        Assert.NotNull(get.Headers.ETag);

        var peticion = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/imagenes/categorias/{codigo}");
        peticion.Headers.IfNoneMatch.Add(get.Headers.ETag!);
        Assert.Equal(HttpStatusCode.NotModified, (await anonimo.SendAsync(peticion)).StatusCode);   // el navegador no la vuelve a bajar
    }

    [Fact]
    public async Task SubirImagenDeProducto_Y_CambiarDeFormato_NoDejaLaAnteriorHuerfana()
    {
        var admin = await _api.AdminAsync();
        var (id, codigo) = await ProductoAsync();

        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsync($"/api/v1/productos/{id}/imagen", Archivo(Png))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsync($"/api/v1/productos/{id}/imagen", Archivo(Jpg, "foto.jpg", "image/jpeg"))).StatusCode);

        Assert.Contains($"productos/{codigo}.jpg", _api.Almacen.Claves);
        Assert.DoesNotContain($"productos/{codigo}.png", _api.Almacen.Claves);   // se borró la anterior

        // El catálogo público ya trae la dirección de la imagen y el código
        var pagina = (await _api.CreateClient().GetFromJsonAsync<PaginaDto<ProductoTiendaDto>>($"/api/v1/catalogo?q={Uri.EscapeDataString("Prod ")}&tamanoPagina=48"))!;
        var p = pagina.Elementos.SingleOrDefault(x => x.Codigo == codigo);
        Assert.NotNull(p);
        Assert.Equal($"/imagenes/productos/{codigo}", p!.ImagenUrl);
    }

    [Fact]
    public async Task Validaciones_NoEsImagen_Vacia_Demasiado_Grande_Inexistente()
    {
        var admin = await _api.AdminAsync();
        var (id, _) = await CategoriaAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsync($"/api/v1/categorias/{id}/imagen", Archivo("<html>hola</html>"u8.ToArray(), "x.png", "image/png"))).StatusCode);   // el tipo declarado no engaña
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsync($"/api/v1/categorias/{id}/imagen", Archivo(Array.Empty<byte>()))).StatusCode);
        var grande = new byte[3 * 1024 * 1024]; Png.CopyTo(grande, 0);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsync($"/api/v1/categorias/{id}/imagen", Archivo(grande))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsync("/api/v1/categorias/99999999/imagen", Archivo(Png))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsync("/api/v1/productos/99999999/imagen", Archivo(Png))).StatusCode);
    }

    [Fact]
    public async Task Permisos_SinSesion401_Operador403_YElCodigoNoPermiteRutasRaras()
    {
        var (id, codigo) = await CategoriaAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.CreateClient().PutAsync($"/api/v1/categorias/{id}/imagen", Archivo(Png))).StatusCode);

        var admin = await _api.AdminAsync();
        var n = "op" + Sufijo().ToLowerInvariant();
        await admin.PostAsJsonAsync("/api/v1/usuarios", new CrearUsuarioSolicitud(n, "Operador", $"{n}@e.com", 2, "ClaveTemporal_2026"));
        var s = await _api.LoginAsync(n, "ClaveTemporal_2026");
        var cambio = await _api.ClienteCon(s).PostAsJsonAsync("/api/v1/auth/cambiar-clave", new CambiarClaveSolicitud("ClaveTemporal_2026", "ClaveNueva_Segura_2026"));
        var operador = _api.ClienteCon((await cambio.Content.ReadFromJsonAsync<SesionDto>())!);
        Assert.Equal(HttpStatusCode.Forbidden, (await operador.PutAsync($"/api/v1/categorias/{id}/imagen", Archivo(Png))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operador.PostAsync("/api/v1/imagenes/reenlazar", null)).StatusCode);

        var anonimo = _api.CreateClient();
        foreach (var ruta in new[] { "/api/v1/imagenes/productos/..%2F..%2Fsecreto", "/api/v1/imagenes/usuarios/" + codigo, "/api/v1/imagenes/productos/NO-EXISTE", "/api/v1/imagenes/categorias/" + codigo })
            Assert.Equal(HttpStatusCode.NotFound, (await anonimo.GetAsync(ruta)).StatusCode);   // la última existe pero aún no tiene imagen
    }

    [Fact]
    public async Task QuitarImagen_BorraElObjeto_YLaTiendaDejaDeServirla()
    {
        var admin = await _api.AdminAsync();
        var (id, codigo) = await CategoriaAsync();
        await admin.PutAsync($"/api/v1/categorias/{id}/imagen", Archivo(Png));

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/categorias/{id}/imagen")).StatusCode);
        Assert.DoesNotContain($"categorias/{codigo}.png", _api.Almacen.Claves);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.CreateClient().GetAsync($"/api/v1/imagenes/categorias/{codigo}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/categorias/{id}/imagen")).StatusCode);   // repetirlo no falla
    }

    [Fact]
    public async Task Reenlazar_TrasRestaurarLaBase_LigaCadaImagenConSuProductoPorCodigo()
    {
        var admin = await _api.AdminAsync();
        // Escenario: la base se restauró y los productos perdieron la clave (ImagenClave nula), pero las imágenes siguen en el almacén
        var (idProducto, codigoProducto) = await ProductoAsync(clave: null);
        var (idCategoria, codigoCategoria) = await CategoriaAsync();
        await _api.Almacen.GuardarAsync($"productos/{codigoProducto}.jpg", new MemoryStream(Jpg), "image/jpeg", default);
        await _api.Almacen.GuardarAsync($"categorias/{codigoCategoria}.png", new MemoryStream(Png), "image/png", default);
        await _api.Almacen.GuardarAsync("productos/CODIGO-QUE-YA-NO-EXISTE.jpg", new MemoryStream(Jpg), "image/jpeg", default);   // huérfana
        Assert.Equal(HttpStatusCode.NotFound, (await _api.CreateClient().GetAsync($"/api/v1/imagenes/productos/{codigoProducto}")).StatusCode);

        var r = await admin.PostAsync("/api/v1/imagenes/reenlazar", null);
        var informe = (await r.Content.ReadFromJsonAsync<ReenlaceImagenesDto>())!;
        Assert.True(informe.Enlazadas >= 2);
        Assert.True(informe.SinDestino >= 1);

        Assert.Equal(HttpStatusCode.OK, (await _api.CreateClient().GetAsync($"/api/v1/imagenes/productos/{codigoProducto}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.CreateClient().GetAsync($"/api/v1/imagenes/categorias/{codigoCategoria}")).StatusCode);

        var segunda = (await (await admin.PostAsync("/api/v1/imagenes/reenlazar", null)).Content.ReadFromJsonAsync<ReenlaceImagenesDto>())!;
        Assert.Equal(0, segunda.Enlazadas);   // hacerlo de nuevo no cambia nada
        Assert.True(segunda.YaEnlazadas >= 2);
    }

    [Fact]
    public async Task CodigosGeneradosPorLaBase_CuandoNoSeIndica()
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GestionPedidosDbContext>();
        var p = new Producto { Nombre = "Sin código " + Sufijo(), Descripcion = "d", CantidadStock = 1, PrecioVenta = 1 };   // Codigo sin asignar
        var c = new Categoria { Nombre = "Sin código " + Sufijo() };
        db.Add(p); db.Add(c);
        await db.SaveChangesAsync();
        Assert.Matches("^PRD-\\d{6}$", p.Codigo);   // EF lee de vuelta lo que generó la secuencia
        Assert.Matches("^CAT-\\d{6}$", c.Codigo);
    }
}
