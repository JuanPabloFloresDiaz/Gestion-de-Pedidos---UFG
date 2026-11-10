using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GestionPedidos.Contracts;

namespace GestionPedidos.Api.Tests;

/// <summary>Administración de productos (<c>/api/v1/productos</c>): permisos, validaciones, clasificación y concurrencia, contra SQL Server real.</summary>
[Collection(ApiCollection.Nombre)]
public class ProductosApiTests
{
    private readonly ApiFixture _api;
    public ProductosApiTests(ApiFixture api) => _api = api;

    private static string Sufijo() => Guid.NewGuid().ToString("N")[..8];
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static DateOnly Hoy() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-6));

    private static CrearProductoSolicitud Nuevo(string? nombre = null, decimal precio = 19.5m, int stock = 10, int? subcategoria = null, IReadOnlyList<int>? opciones = null, DateOnly? ingreso = null) =>
        new(nombre ?? "Prod " + Sufijo(), "Descripción", precio, stock, ingreso ?? Hoy(), subcategoria, true, opciones);

    private static ActualizarProductoSolicitud Edicion(ProductoDto p, string? version, decimal? precio = null, int? subcategoria = null, IReadOnlyList<int>? opciones = null) =>
        new(p.Nombre, p.Descripcion, precio ?? p.PrecioVenta, p.CantidadStock, p.FechaIngreso, subcategoria, p.MostrarStock, version, opciones);

    private static async Task<ProductoDto> LeerAsync(HttpResponseMessage r) => (await r.Content.ReadFromJsonAsync<ProductoDto>(Web))!;

    private async Task<ProductoDto> CrearAsync(HttpClient admin, CrearProductoSolicitud? s = null)
    {
        var r = await admin.PostAsJsonAsync("/api/v1/productos", s ?? Nuevo());
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return await LeerAsync(r);
    }

    /// <summary>Una subcategoría con dos filtros (Color: Negro/Blanco, Talla: M) para probar la clasificación.</summary>
    private async Task<(int Sub, int Negro, int Blanco, int TallaM)> ClasificacionAsync(HttpClient admin)
    {
        var s = Sufijo();
        var cat = (await (await admin.PostAsJsonAsync("/api/v1/categorias", new GuardarClasificacionSolicitud("Ropa " + s, null))).Content.ReadFromJsonAsync<CategoriaDto>(Web))!;
        var sub = (await (await admin.PostAsJsonAsync($"/api/v1/categorias/{cat.Id}/subcategorias", new GuardarClasificacionSolicitud("Camisas", null))).Content.ReadFromJsonAsync<SubcategoriaDto>(Web))!;
        var color = (await (await admin.PostAsJsonAsync($"/api/v1/subcategorias/{sub.Id}/filtros", new GuardarFiltroSolicitud("Color"))).Content.ReadFromJsonAsync<FiltroDto>(Web))!;
        var talla = (await (await admin.PostAsJsonAsync($"/api/v1/subcategorias/{sub.Id}/filtros", new GuardarFiltroSolicitud("Talla"))).Content.ReadFromJsonAsync<FiltroDto>(Web))!;
        var negro = (await (await admin.PostAsJsonAsync($"/api/v1/filtros/{color.Id}/opciones", new GuardarOpcionSolicitud("Negro"))).Content.ReadFromJsonAsync<OpcionFiltroDto>(Web))!;
        var blanco = (await (await admin.PostAsJsonAsync($"/api/v1/filtros/{color.Id}/opciones", new GuardarOpcionSolicitud("Blanco"))).Content.ReadFromJsonAsync<OpcionFiltroDto>(Web))!;
        var m = (await (await admin.PostAsJsonAsync($"/api/v1/filtros/{talla.Id}/opciones", new GuardarOpcionSolicitud("M"))).Content.ReadFromJsonAsync<OpcionFiltroDto>(Web))!;
        return (sub.Id, negro.Id, blanco.Id, m.Id);
    }

    // ===================== Permisos =====================

    [Fact]
    public async Task SinSesion_TodosLosEndpoints_Dan401()
    {
        var anonimo = _api.CreateClient();
        var json = JsonContent.Create(new { });

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/v1/productos")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/v1/productos/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsync("/api/v1/productos", json)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PutAsync("/api/v1/productos/1", json)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PatchAsync("/api/v1/productos/1/estado", json)).StatusCode);
    }

    [Fact]
    public async Task UnClienteDeLaWeb_NoPuedeAdministrarProductos403()
    {
        var correo = $"cli{Sufijo()}@ejemplo.com";
        var registro = await _api.CreateClient().PostAsJsonAsync("/api/v1/auth/registro",
            new RegistroClienteSolicitud("Cliente", "Web", "78945612", "Col. Escalón", correo, 1, "ClaveCliente_2026"));
        Assert.Equal(HttpStatusCode.Created, registro.StatusCode);
        var web = _api.ClienteCon(await _api.LoginAsync(correo, "ClaveCliente_2026"));

        Assert.Equal(HttpStatusCode.Forbidden, (await web.GetAsync("/api/v1/productos")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await web.PostAsJsonAsync("/api/v1/productos", Nuevo())).StatusCode);
    }

    // ===================== Crear =====================

    [Fact]
    public async Task Crear_Exitoso_201_ConCodigoYVersion()
    {
        var admin = await _api.AdminAsync();

        var r = await admin.PostAsJsonAsync("/api/v1/productos", Nuevo("Camisa " + Sufijo(), 19.5m, 7));

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var p = await LeerAsync(r);
        Assert.StartsWith("PRD-", p.Codigo);
        Assert.Equal(19.5m, p.PrecioVenta);
        Assert.Equal(7, p.CantidadStock);
        Assert.True(p.Activo);
        Assert.False(string.IsNullOrEmpty(p.RowVersion));
        Assert.Equal($"/api/v1/productos/{p.Id}", r.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Crear_NombreRepetido_409()
    {
        var admin = await _api.AdminAsync();
        var nombre = "Unico " + Sufijo();
        await CrearAsync(admin, Nuevo(nombre));

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/productos", Nuevo(nombre))).StatusCode);
    }

    [Fact]
    public async Task Crear_DatosInvalidos_400_NuncaError500()
    {
        var admin = await _api.AdminAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/productos", Nuevo(precio: -1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/productos", Nuevo(precio: 1_000_000_000_000m))).StatusCode);   // no cabe en la columna
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/productos", Nuevo(precio: 1.234m))).StatusCode);              // 3 decimales
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/productos", Nuevo(stock: -1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/productos", Nuevo(nombre: " "))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/productos", Nuevo(ingreso: Hoy().AddDays(2)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/productos", new StringContent("{no es json", Encoding.UTF8, "application/json"))).StatusCode);
    }

    // ---------- Clasificación: antes respondía 500 o descartaba en silencio ----------

    [Fact]
    public async Task Crear_ConClasificacionValida_201_YElDetalleQuedaGuardado()
    {
        var admin = await _api.AdminAsync();
        var c = await ClasificacionAsync(admin);

        var r = await admin.PostAsJsonAsync("/api/v1/productos", Nuevo(subcategoria: c.Sub, opciones: new[] { c.Negro, c.TallaM }));

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var p = await LeerAsync(await admin.GetAsync($"/api/v1/productos/{(await LeerAsync(r)).Id}"));
        Assert.Equal(c.Sub, p.SubcategoriaId);
        Assert.Equal(2, p.Detalles!.Count);
    }

    [Fact]
    public async Task Crear_SubcategoriaInexistente_400_NoError500()
    {
        var admin = await _api.AdminAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/productos", Nuevo(subcategoria: 99999999))).StatusCode);
    }

    [Fact]
    public async Task Crear_DosOpcionesDelMismoFiltro_400_NoError500()
    {
        var admin = await _api.AdminAsync();
        var c = await ClasificacionAsync(admin);

        var r = await admin.PostAsJsonAsync("/api/v1/productos", Nuevo(subcategoria: c.Sub, opciones: new[] { c.Negro, c.Blanco }));

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("una opción por filtro", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Crear_OpcionInexistenteODeOtraSubcategoria_400_NoSeDescartaEnSilencio()
    {
        var admin = await _api.AdminAsync();
        var c = await ClasificacionAsync(admin);
        var otra = await ClasificacionAsync(admin);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/productos", Nuevo(subcategoria: c.Sub, opciones: new[] { 99999999 }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/productos", Nuevo(subcategoria: c.Sub, opciones: new[] { otra.Negro }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/productos", Nuevo(subcategoria: null, opciones: new[] { c.Negro }))).StatusCode);
    }

    // ===================== Editar =====================

    [Fact]
    public async Task Editar_ConLaVersionCorrecta_200_YDevuelveUnaVersionNueva()
    {
        var admin = await _api.AdminAsync();
        var p = await CrearAsync(admin);

        var r = await admin.PutAsJsonAsync($"/api/v1/productos/{p.Id}", Edicion(p, p.RowVersion, precio: 30m));

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var editado = await LeerAsync(r);
        Assert.Equal(30m, editado.PrecioVenta);
        Assert.NotEqual(p.RowVersion, editado.RowVersion);
    }

    [Fact]
    public async Task Editar_ConVersionDesactualizada_409_YNoPisaLoDelOtro()
    {
        var admin = await _api.AdminAsync();
        var p = await CrearAsync(admin);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/productos/{p.Id}", Edicion(p, p.RowVersion, precio: 30m))).StatusCode);

        var vieja = await admin.PutAsJsonAsync($"/api/v1/productos/{p.Id}", Edicion(p, p.RowVersion, precio: 99m));

        Assert.Equal(HttpStatusCode.Conflict, vieja.StatusCode);
        Assert.Equal(30m, (await LeerAsync(await admin.GetAsync($"/api/v1/productos/{p.Id}"))).PrecioVenta);
    }

    [Fact]
    public async Task Editar_SinRowVersion_400_NoPisaElProductoSinAviso()
    {
        var admin = await _api.AdminAsync();
        var p = await CrearAsync(admin, Nuevo(precio: 10m));

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/productos/{p.Id}", Edicion(p, null, precio: 77m))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/productos/{p.Id}", Edicion(p, "", precio: 77m))).StatusCode);

        Assert.Equal(10m, (await LeerAsync(await admin.GetAsync($"/api/v1/productos/{p.Id}"))).PrecioVenta);
    }

    [Fact]
    public async Task Editar_RowVersionQueNoEsBase64_400_NoError500()
    {
        var admin = await _api.AdminAsync();
        var p = await CrearAsync(admin);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/productos/{p.Id}", Edicion(p, "xx!!"))).StatusCode);
    }

    [Fact]
    public async Task Editar_Inexistente_404_YInactivo_422()
    {
        var admin = await _api.AdminAsync();
        var p = await CrearAsync(admin);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync("/api/v1/productos/99999999", Edicion(p, p.RowVersion))).StatusCode);

        var baja = await LeerAsync(await admin.PatchAsJsonAsync($"/api/v1/productos/{p.Id}/estado", new CambiarEstadoSolicitud(false)));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PutAsJsonAsync($"/api/v1/productos/{p.Id}", Edicion(p, baja.RowVersion))).StatusCode);
    }

    [Fact]
    public async Task Editar_ClasificacionInvalida_400_ElProductoNoCambia()
    {
        var admin = await _api.AdminAsync();
        var c = await ClasificacionAsync(admin);
        var p = await CrearAsync(admin, Nuevo(subcategoria: c.Sub, opciones: new[] { c.Negro }));

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/productos/{p.Id}", Edicion(p, p.RowVersion, subcategoria: 99999999))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/productos/{p.Id}", Edicion(p, p.RowVersion, subcategoria: c.Sub, opciones: new[] { c.Negro, c.Blanco }))).StatusCode);

        var igual = await LeerAsync(await admin.GetAsync($"/api/v1/productos/{p.Id}"));
        Assert.Equal(c.Sub, igual.SubcategoriaId);
        Assert.Single(igual.Detalles!);
    }

    [Fact]
    public async Task Editar_CambiarOpcionDeUnFiltro_ReemplazaElDetalle()
    {
        var admin = await _api.AdminAsync();
        var c = await ClasificacionAsync(admin);
        var p = await CrearAsync(admin, Nuevo(subcategoria: c.Sub, opciones: new[] { c.Negro }));

        var r = await admin.PutAsJsonAsync($"/api/v1/productos/{p.Id}", Edicion(p, p.RowVersion, subcategoria: c.Sub, opciones: new[] { c.Blanco, c.TallaM }));

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var detalles = (await LeerAsync(await admin.GetAsync($"/api/v1/productos/{p.Id}"))).Detalles!;
        Assert.Equal(2, detalles.Count);
        Assert.DoesNotContain(detalles, d => d.OpcionId == c.Negro);
    }

    // ===================== Estado, consulta y búsqueda =====================

    [Fact]
    public async Task Estado_DesactivarYReactivar_ConservaElProducto()
    {
        var admin = await _api.AdminAsync();
        var p = await CrearAsync(admin);

        var baja = await admin.PatchAsJsonAsync($"/api/v1/productos/{p.Id}/estado", new CambiarEstadoSolicitud(false));
        Assert.Equal(HttpStatusCode.OK, baja.StatusCode);
        Assert.False((await LeerAsync(baja)).Activo);

        var alta = await admin.PatchAsJsonAsync($"/api/v1/productos/{p.Id}/estado", new CambiarEstadoSolicitud(true));
        Assert.True((await LeerAsync(alta)).Activo);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PatchAsJsonAsync("/api/v1/productos/99999999/estado", new CambiarEstadoSolicitud(false))).StatusCode);
    }

    [Fact]
    public async Task Obtener_Existente_200_Inexistente_404_YRutaNoNumerica_404()
    {
        var admin = await _api.AdminAsync();
        var p = await CrearAsync(admin);

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/productos/{p.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/productos/99999999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/productos/abc")).StatusCode);
    }

    [Fact]
    public async Task Buscar_PorTextoCategoriaYEstado_YPagina()
    {
        var admin = await _api.AdminAsync();
        var marca = "Buscable" + Sufijo();
        var c = await ClasificacionAsync(admin);
        var a = await CrearAsync(admin, Nuevo(marca + " uno", subcategoria: c.Sub));
        await CrearAsync(admin, Nuevo(marca + " dos"));
        var baja = await CrearAsync(admin, Nuevo(marca + " tres"));
        await admin.PatchAsJsonAsync($"/api/v1/productos/{baja.Id}/estado", new CambiarEstadoSolicitud(false));

        async Task<PaginaDto<ProductoDto>> Buscar(string q) => (await admin.GetFromJsonAsync<PaginaDto<ProductoDto>>("/api/v1/productos?" + q, Web))!;

        Assert.Equal(3, (await Buscar($"q={marca}")).Total);
        Assert.Equal(2, (await Buscar($"q={marca}&activo=true")).Total);
        Assert.Equal(1, (await Buscar($"q={marca}&activo=false")).Total);
        Assert.Equal(new[] { a.Id }, (await Buscar($"q={marca}&subcategoriaId={c.Sub}")).Elementos.Select(p => p.Id));
        var pagina = await Buscar($"q={marca}&pagina=2&tamanoPagina=2");
        Assert.Equal(3, pagina.Total);
        Assert.Single(pagina.Elementos);
    }

    [Fact]
    public async Task Buscar_ConInyeccionSql_NoRompeNiDevuelveTodo()
    {
        var admin = await _api.AdminAsync();
        var p = await CrearAsync(admin);

        foreach (var texto in new[] { "' OR 1=1 --", "'; DROP TABLE Productos; --", "x' UNION SELECT 1,2,3 --" })
        {
            var r = await admin.GetAsync("/api/v1/productos?q=" + Uri.EscapeDataString(texto));
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.Empty((await r.Content.ReadFromJsonAsync<PaginaDto<ProductoDto>>(Web))!.Elementos);
        }

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/productos/{p.Id}")).StatusCode);   // la tabla sigue ahí
    }

    [Fact]
    public async Task Buscar_PaginacionRara_NoDa500()
    {
        var admin = await _api.AdminAsync();
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/productos?pagina=0&tamanoPagina=0")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/productos?pagina=-5&tamanoPagina=99999")).StatusCode);
    }
}
