using System.Net;
using System.Net.Http.Json;
using GestionPedidos.Contracts;

namespace GestionPedidos.Api.Tests;

[Collection(ApiCollection.Nombre)]
public class UsuariosApiTests
{
    private readonly ApiFixture _api;
    public UsuariosApiTests(ApiFixture api) => _api = api;

    private static string Sufijo() => Guid.NewGuid().ToString("N")[..8];

    [Fact]
    public async Task Roles_ListaLosTresRolesConSusPermisos()
    {
        var admin = await _api.AdminAsync();
        var roles = (await admin.GetFromJsonAsync<List<RolDto>>("/api/v1/roles"))!;
        Assert.Equal(new[] { "Administrador", "Operador", "Cliente" }, roles.Select(r => r.Nombre));
        Assert.Equal(21, roles[0].Permisos.Count);
    }

    [Fact]
    public async Task CrearUsuario_Valido_Da201_YConDatosInvalidosDa400()
    {
        var admin = await _api.AdminAsync();
        var n = "nuevo" + Sufijo();
        var ok = await admin.PostAsJsonAsync("/api/v1/usuarios", new CrearUsuarioSolicitud(n, "Persona Nueva", $"{n}@e.com", 2, "ClaveTemporal_2026"));
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        Assert.NotNull(ok.Headers.Location);
        var u = (await ok.Content.ReadFromJsonAsync<UsuarioDto>())!;
        Assert.True(u.DebeCambiarClave);
        Assert.DoesNotContain("ClaveTemporal", await ok.Content.ReadAsStringAsync());   // jamás se devuelve la clave ni su hash

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/usuarios", new CrearUsuarioSolicitud("ab", "X", "malcorreo", 2, "corta"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/usuarios", new CrearUsuarioSolicitud(n, "Otra", $"otro{n}@e.com", 2, "ClaveTemporal_2026"))).StatusCode);
    }

    [Fact]
    public async Task Listar_FiltraPorTextoYEstado_YPagina()
    {
        var admin = await _api.AdminAsync();
        var marca = "zeta" + Sufijo();
        for (var i = 0; i < 3; i++)
            await admin.PostAsJsonAsync("/api/v1/usuarios", new CrearUsuarioSolicitud($"{marca}{i}", $"Persona {marca} {i}", $"{marca}{i}@e.com", 2, "ClaveTemporal_2026"));

        var pagina = (await admin.GetFromJsonAsync<PaginaDto<UsuarioDto>>($"/api/v1/usuarios?q={marca}&pagina=1&tamanoPagina=2"))!;
        Assert.Equal(3, pagina.Total);
        Assert.Equal(2, pagina.Elementos.Count);
        Assert.Equal(2, pagina.TotalPaginas);
        var segunda = (await admin.GetFromJsonAsync<PaginaDto<UsuarioDto>>($"/api/v1/usuarios?q={marca}&pagina=2&tamanoPagina=2"))!;
        Assert.Single(segunda.Elementos);

        var tope = (await admin.GetFromJsonAsync<PaginaDto<UsuarioDto>>("/api/v1/usuarios?tamanoPagina=5000"))!;
        Assert.Equal(100, tope.TamanoPagina);   // el tamaño de página tiene tope
        var inactivos = (await admin.GetFromJsonAsync<PaginaDto<UsuarioDto>>($"/api/v1/usuarios?q={marca}&activo=false"))!;
        Assert.Equal(0, inactivos.Total);
    }

    [Fact]
    public async Task Inyeccion_EnElFiltroDeTexto_NoRompeNiDevuelveNada()
    {
        var admin = await _api.AdminAsync();
        var r = await admin.GetAsync("/api/v1/usuarios?q=%27%3B%20DROP%20TABLE%20Usuarios%3B%20--");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(0, (await r.Content.ReadFromJsonAsync<PaginaDto<UsuarioDto>>())!.Total);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/usuarios")).StatusCode);   // la tabla sigue ahí
    }

    [Fact]
    public async Task NoSePuedeDarDeBajaUnoMismo_NiAlUltimoAdministrador()
    {
        var admin = await _api.AdminAsync();
        var yo = (await admin.GetFromJsonAsync<UsuarioActualDto>("/api/v1/auth/yo"))!;
        var r = await admin.PatchAsJsonAsync($"/api/v1/usuarios/{yo.Id}/estado", new CambiarEstadoSolicitud(false));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
    }

    [Fact]
    public async Task ActualizarConVersionVieja_Da409()
    {
        var admin = await _api.AdminAsync();
        var n = "ver" + Sufijo();
        var u = (await (await admin.PostAsJsonAsync("/api/v1/usuarios", new CrearUsuarioSolicitud(n, "Versión", $"{n}@e.com", 2, "ClaveTemporal_2026"))).Content.ReadFromJsonAsync<UsuarioDto>())!;
        var primera = await admin.PutAsJsonAsync($"/api/v1/usuarios/{u.Id}", new ActualizarUsuarioSolicitud("Primera edición", u.Correo, 2, u.RowVersion));
        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);
        var vieja = await admin.PutAsJsonAsync($"/api/v1/usuarios/{u.Id}", new ActualizarUsuarioSolicitud("Segunda con versión vieja", u.Correo, 2, u.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, vieja.StatusCode);
    }

    [Fact]
    public async Task UsuarioInexistente_Da404()
    {
        var admin = await _api.AdminAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/usuarios/999999")).StatusCode);
    }
}
