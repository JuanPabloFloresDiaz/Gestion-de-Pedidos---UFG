using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GestionPedidos.Contracts;

namespace GestionPedidos.Api.Tests;

/// <summary>Categorías, subcategorías, filtros y opciones: permisos, reglas de baja y eliminación definitiva.</summary>
[Collection(ApiCollection.Nombre)]
public class ClasificacionApiTests
{
    private readonly ApiFixture _api;
    public ClasificacionApiTests(ApiFixture api) => _api = api;

    private static string Sufijo() => Guid.NewGuid().ToString("N")[..8];

    private async Task<HttpClient> OperadorAsync()
    {
        var admin = await _api.AdminAsync();
        var n = "op" + Sufijo();
        await admin.PostAsJsonAsync("/api/v1/usuarios", new CrearUsuarioSolicitud(n, "Operador", $"{n}@e.com", 2, "ClaveTemporal_2026"));
        var s = await _api.LoginAsync(n, "ClaveTemporal_2026");
        var cambio = await _api.ClienteCon(s).PostAsJsonAsync("/api/v1/auth/cambiar-clave", new CambiarClaveSolicitud("ClaveTemporal_2026", "ClaveNueva_Segura_2026"));
        return _api.ClienteCon((await cambio.Content.ReadFromJsonAsync<SesionDto>())!);
    }

    private static async Task<string?> Codigo(HttpResponseMessage r)
    {
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("codigo", out var c) ? c.GetString() : null;
    }

    [Fact]
    public async Task Permisos_SinSesion401_Operador_SoloConsulta_Administrador_Todo()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.CreateClient().GetAsync("/api/v1/categorias")).StatusCode);

        var op = await OperadorAsync();
        Assert.Equal(HttpStatusCode.OK, (await op.GetAsync("/api/v1/categorias")).StatusCode);
        var intento = await op.PostAsJsonAsync("/api/v1/categorias", new GuardarClasificacionSolicitud("No debe poder " + Sufijo(), null));
        Assert.Equal(HttpStatusCode.Forbidden, intento.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await op.DeleteAsync("/api/v1/categorias/1")).StatusCode);

        var admin = await _api.AdminAsync();
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/v1/categorias", new GuardarClasificacionSolicitud("Admin " + Sufijo(), null))).StatusCode);
    }

    [Fact]
    public async Task FlujoCompleto_Crear_Filtrar_BajaSuave_Reactivar_EliminarDefinitivo_YHistorial()
    {
        var admin = await _api.AdminAsync();
        var s = Sufijo();

        var cat = (await (await admin.PostAsJsonAsync("/api/v1/categorias", new GuardarClasificacionSolicitud("Ropa " + s, "Prendas"))).Content.ReadFromJsonAsync<CategoriaDto>())!;
        var sub = (await (await admin.PostAsJsonAsync($"/api/v1/categorias/{cat.Id}/subcategorias", new GuardarClasificacionSolicitud("Camisas", null))).Content.ReadFromJsonAsync<SubcategoriaDto>())!;
        var filtro = (await (await admin.PostAsJsonAsync($"/api/v1/subcategorias/{sub.Id}/filtros", new GuardarFiltroSolicitud("Color"))).Content.ReadFromJsonAsync<FiltroDto>())!;
        var opcion = (await (await admin.PostAsJsonAsync($"/api/v1/filtros/{filtro.Id}/opciones", new GuardarOpcionSolicitud("Negro"))).Content.ReadFromJsonAsync<OpcionFiltroDto>())!;

        // El árbol y los filtros reflejan lo creado
        var arbol = (await admin.GetFromJsonAsync<List<CategoriaDto>>("/api/v1/categorias"))!;
        var mia = arbol.Single(c => c.Id == cat.Id);
        Assert.Equal(1, mia.Subcategorias.Single().TotalFiltros);
        var filtros = (await admin.GetFromJsonAsync<List<FiltroDto>>($"/api/v1/subcategorias/{sub.Id}/filtros"))!;
        Assert.Equal("Negro", filtros.Single().Opciones.Single().Valor);

        // Repetido → 409; vacío → 400
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/v1/filtros/{filtro.Id}/opciones", new GuardarOpcionSolicitud("negro"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/categorias", new GuardarClasificacionSolicitud(" ", null))).StatusCode);

        // Editar con versión vieja → 409
        var editada = await admin.PutAsJsonAsync($"/api/v1/opciones/{opcion.Id}", new GuardarOpcionSolicitud("Negro mate", opcion.RowVersion));
        Assert.Equal(HttpStatusCode.OK, editada.StatusCode);
        var vieja = await admin.PutAsJsonAsync($"/api/v1/opciones/{opcion.Id}", new GuardarOpcionSolicitud("Otro", opcion.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, vieja.StatusCode);

        // Eliminar algo activo → 422; la categoría con subcategorías activas no se da de baja
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.DeleteAsync($"/api/v1/opciones/{opcion.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PatchAsJsonAsync($"/api/v1/categorias/{cat.Id}/estado", new CambiarEstadoSolicitud(false))).StatusCode);

        // Baja de abajo hacia arriba, reactivación y nueva baja
        var bajaOpcion = await admin.PatchAsJsonAsync($"/api/v1/opciones/{opcion.Id}/estado", new CambiarEstadoSolicitud(false));
        Assert.Equal(HttpStatusCode.OK, bajaOpcion.StatusCode);
        Assert.NotNull((await bajaOpcion.Content.ReadFromJsonAsync<OpcionFiltroDto>())!.EliminadoEn);
        var reactivada = (await (await admin.PatchAsJsonAsync($"/api/v1/opciones/{opcion.Id}/estado", new CambiarEstadoSolicitud(true))).Content.ReadFromJsonAsync<OpcionFiltroDto>())!;
        Assert.True(reactivada.Activo);
        Assert.Null(reactivada.EliminadoEn);

        // Eliminación definitiva en orden inverso
        await admin.PatchAsJsonAsync($"/api/v1/opciones/{opcion.Id}/estado", new CambiarEstadoSolicitud(false));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/opciones/{opcion.Id}")).StatusCode);
        await admin.PatchAsJsonAsync($"/api/v1/filtros/{filtro.Id}/estado", new CambiarEstadoSolicitud(false));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/filtros/{filtro.Id}")).StatusCode);
        await admin.PatchAsJsonAsync($"/api/v1/subcategorias/{sub.Id}/estado", new CambiarEstadoSolicitud(false));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/subcategorias/{sub.Id}")).StatusCode);
        await admin.PatchAsJsonAsync($"/api/v1/categorias/{cat.Id}/estado", new CambiarEstadoSolicitud(false));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/categorias/{cat.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/api/v1/categorias/{cat.Id}", new GuardarClasificacionSolicitud("X", null))).StatusCode);

        // El historial registra los cambios con detalle legible
        await Task.Delay(1500);   // el historial se escribe en segundo plano
        var pagina = (await admin.GetFromJsonAsync<PaginaDto<ActividadDto>>($"/api/v1/actividades?entidad=Categoria&q={Uri.EscapeDataString("Ropa " + s)}"))!;
        Assert.Contains(pagina.Elementos, a => a.Accion == "Registrar");
        Assert.Contains(pagina.Elementos, a => a.Accion == "Eliminar");
    }

    [Fact]
    public async Task ReactivarUnHijo_BajoUnPadreInactivo_Da422_YCrearBajoInactivoTambien()
    {
        var admin = await _api.AdminAsync();
        var cat = (await (await admin.PostAsJsonAsync("/api/v1/categorias", new GuardarClasificacionSolicitud("Pad " + Sufijo(), null))).Content.ReadFromJsonAsync<CategoriaDto>())!;
        var sub = (await (await admin.PostAsJsonAsync($"/api/v1/categorias/{cat.Id}/subcategorias", new GuardarClasificacionSolicitud("Hijo", null))).Content.ReadFromJsonAsync<SubcategoriaDto>())!;
        await admin.PatchAsJsonAsync($"/api/v1/subcategorias/{sub.Id}/estado", new CambiarEstadoSolicitud(false));
        await admin.PatchAsJsonAsync($"/api/v1/categorias/{cat.Id}/estado", new CambiarEstadoSolicitud(false));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PatchAsJsonAsync($"/api/v1/subcategorias/{sub.Id}/estado", new CambiarEstadoSolicitud(true))).StatusCode);
        var crear = await admin.PostAsJsonAsync($"/api/v1/categorias/{cat.Id}/subcategorias", new GuardarClasificacionSolicitud("Nuevo", null));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, crear.StatusCode);
        Assert.Equal("REGLA_DE_NEGOCIO", await Codigo(crear));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/subcategorias/999999/filtros")).StatusCode);
    }
}
