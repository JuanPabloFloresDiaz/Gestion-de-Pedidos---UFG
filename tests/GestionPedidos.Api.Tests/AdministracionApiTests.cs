using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GestionPedidos.Contracts;
using GestionPedidos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GestionPedidos.Api.Tests;

/// <summary>Roles y permisos, catálogos, historial y eliminación (suave y definitiva) de usuarios.</summary>
[Collection(ApiCollection.Nombre)]
public class AdministracionApiTests
{
    private readonly ApiFixture _api;
    public AdministracionApiTests(ApiFixture api) => _api = api;

    private static string Sufijo() => Guid.NewGuid().ToString("N")[..8];

    private static async Task<string?> Codigo(HttpResponseMessage r)
    {
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("codigo", out var c) ? c.GetString() : null;
    }

    /// <summary>Operador con sesión plenamente operativa (clave ya cambiada).</summary>
    private async Task<HttpClient> OperadorAsync()
    {
        var admin = await _api.AdminAsync();
        var n = "op" + Sufijo();
        await admin.PostAsJsonAsync("/api/v1/usuarios", new CrearUsuarioSolicitud(n, "Operador", $"{n}@e.com", 2, "ClaveTemporal_2026"));
        var s = await _api.LoginAsync(n, "ClaveTemporal_2026");
        var cambio = await _api.ClienteCon(s).PostAsJsonAsync("/api/v1/auth/cambiar-clave", new CambiarClaveSolicitud("ClaveTemporal_2026", "ClaveNueva_Segura_2026"));
        return _api.ClienteCon((await cambio.Content.ReadFromJsonAsync<SesionDto>())!);
    }

    // ---------------- Roles y permisos ----------------
    [Fact]
    public async Task Permisos_ListaElCatalogoCompleto_SoloConRolesVer()
    {
        var admin = await _api.AdminAsync();
        var permisos = (await admin.GetFromJsonAsync<List<PermisoDto>>("/api/v1/permisos"))!;
        Assert.Equal(28, permisos.Count);
        Assert.Contains(permisos, p => p.Codigo == "roles.editar");
        Assert.Equal(HttpStatusCode.Forbidden, (await (await OperadorAsync()).GetAsync("/api/v1/permisos")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.CreateClient().GetAsync("/api/v1/permisos")).StatusCode);
    }

    [Fact]
    public async Task EditarPermisosDeUnRol_SurteEfectoEnElSiguienteLogin_YQuedaEnElHistorial()
    {
        var admin = await _api.AdminAsync();
        var roles = (await admin.GetFromJsonAsync<List<RolDto>>("/api/v1/roles"))!;
        var operador = roles.Single(r => r.Nombre == "Operador");
        var sinEditar = operador.Permisos.Where(p => p != "clientes.editar").ToList();
        try
        {
            var r = await admin.PutAsJsonAsync($"/api/v1/roles/{operador.Id}/permisos", new ActualizarPermisosRolSolicitud(sinEditar));
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            var actualizado = (await r.Content.ReadFromJsonAsync<RolDto>())!;
            Assert.DoesNotContain("clientes.editar", actualizado.Permisos);

            var enLista = (await admin.GetFromJsonAsync<List<RolDto>>("/api/v1/roles"))!.Single(x => x.Id == operador.Id);
            Assert.DoesNotContain("clientes.editar", enLista.Permisos);

            // Un operador que inicie sesión AHORA ya no tiene ese permiso
            var op = await OperadorAsync();
            var yo = (await op.GetFromJsonAsync<UsuarioActualDto>("/api/v1/auth/yo"))!;
            Assert.DoesNotContain("clientes.editar", yo.Permisos);

            using var scope = _api.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GestionPedidosDbContext>();
            for (var i = 0; i < 40 && !await db.Actividades.AnyAsync(a => a.Entidad == "Rol" && a.Detalle!.Contains("quitados: clientes.editar")); i++) await Task.Delay(250);
            Assert.True(await db.Actividades.AnyAsync(a => a.Entidad == "Rol" && a.Detalle!.Contains("quitados: clientes.editar")));
        }
        finally
        {
            await admin.PutAsJsonAsync($"/api/v1/roles/{operador.Id}/permisos", new ActualizarPermisosRolSolicitud(operador.Permisos));   // deja el rol como estaba
        }
    }

    [Fact]
    public async Task EditarPermisos_ReglasQueProtegenElSistema()
    {
        var admin = await _api.AdminAsync();
        var roles = (await admin.GetFromJsonAsync<List<RolDto>>("/api/v1/roles"))!;
        var rolAdmin = roles.Single(r => r.Nombre == "Administrador");
        var rolCliente = roles.Single(r => r.Nombre == "Cliente");

        // El administrador no puede quitarse roles.editar (se quedaría sin poder arreglarlo)
        var sinRolesEditar = rolAdmin.Permisos.Where(p => p != "roles.editar").ToList();
        var r1 = await admin.PutAsJsonAsync($"/api/v1/roles/{rolAdmin.Id}/permisos", new ActualizarPermisosRolSolicitud(sinRolesEditar));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, r1.StatusCode);
        Assert.Equal("REGLA_DE_NEGOCIO", await Codigo(r1));

        // El cliente no puede recibir permisos de administración
        var r2 = await admin.PutAsJsonAsync($"/api/v1/roles/{rolCliente.Id}/permisos", new ActualizarPermisosRolSolicitud(rolCliente.Permisos.Append("usuarios.ver").ToList()));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, r2.StatusCode);

        // Permisos inexistentes y roles inexistentes
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/roles/{rolAdmin.Id}/permisos", new ActualizarPermisosRolSolicitud(rolAdmin.Permisos.Append("inventado.x").ToList()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync("/api/v1/roles/9999/permisos", new ActualizarPermisosRolSolicitud(new[] { "admin.acceso" }))).StatusCode);
    }

    [Fact]
    public async Task EditarPermisos_UnOperadorNoPuede_403()
    {
        var op = await OperadorAsync();
        var r = await op.PutAsJsonAsync("/api/v1/roles/2/permisos", new ActualizarPermisosRolSolicitud(new[] { "admin.acceso" }));
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    // ---------------- Catálogos ----------------
    [Fact]
    public async Task Departamentos_SonPublicos_YLos14ConSuCantidadDeCiudades()
    {
        var departamentos = (await _api.CreateClient().GetFromJsonAsync<List<DepartamentoDto>>("/api/v1/departamentos"))!;
        Assert.Equal(14, departamentos.Count);
        Assert.Equal(262, departamentos.Sum(d => d.TotalCiudades));
        Assert.Equal(19, departamentos.Single(d => d.Codigo == "SS").TotalCiudades);
    }

    [Fact]
    public async Task CiudadesDeUnDepartamento_SonPublicas_ConBusquedaSinAcentos()
    {
        var c = _api.CreateClient();
        var ahuachapan = (await c.GetFromJsonAsync<List<DepartamentoDto>>("/api/v1/departamentos"))!.Single(d => d.Codigo == "AH");
        var todas = (await c.GetFromJsonAsync<List<CiudadDto>>($"/api/v1/departamentos/{ahuachapan.Id}/ciudades"))!;
        Assert.Equal(12, todas.Count);
        var filtradas = (await c.GetFromJsonAsync<List<CiudadDto>>($"/api/v1/departamentos/{ahuachapan.Id}/ciudades?q=ahuachapan"))!;   // sin tilde
        Assert.Contains(filtradas, x => x.Nombre == "Ahuachapán");
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/v1/departamentos/999/ciudades")).StatusCode);
    }

    [Fact]
    public async Task CiudadesPaginadas_ParaLaAdministracion_RequierenPermiso()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.CreateClient().GetAsync("/api/v1/ciudades")).StatusCode);

        var admin = await _api.AdminAsync();
        var pagina = (await admin.GetFromJsonAsync<PaginaDto<CiudadDto>>("/api/v1/ciudades?tamanoPagina=50"))!;
        Assert.Equal(262, pagina.Total);
        Assert.Equal(50, pagina.Elementos.Count);
        var soya = (await admin.GetFromJsonAsync<PaginaDto<CiudadDto>>("/api/v1/ciudades?q=SOYAPANGO"))!;
        Assert.Contains(soya.Elementos, x => x.Nombre == "Soyapango" && x.Municipio == "San Salvador Este");

        Assert.Equal(HttpStatusCode.OK, (await (await OperadorAsync()).GetAsync("/api/v1/ciudades")).StatusCode);   // el operador lo necesita para clientes

        var correo = $"cli{Sufijo()}@e.com";
        await _api.CreateClient().PostAsJsonAsync("/api/v1/auth/registro", new RegistroClienteSolicitud("Cli", "Ente", "7000-2222", "X", correo, 1, "ClaveCliente_2026"));
        var cliente = _api.ClienteCon(await _api.LoginAsync(correo, "ClaveCliente_2026"));
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/v1/ciudades")).StatusCode);
    }

    // ---------------- Historial ----------------
    [Fact]
    public async Task Historial_SoloElAdministrador_ConFiltrosYPaginacion()
    {
        var admin = await _api.AdminAsync();
        var n = "hist" + Sufijo();
        await admin.PostAsJsonAsync("/api/v1/usuarios", new CrearUsuarioSolicitud(n, "Para historial", $"{n}@e.com", 2, "ClaveTemporal_2026"));

        PaginaDto<ActividadDto> pagina = null!;
        for (var i = 0; i < 40; i++)
        {
            pagina = (await admin.GetFromJsonAsync<PaginaDto<ActividadDto>>($"/api/v1/actividades?entidad=Usuario&accion=Registrar&q={n}"))!;
            if (pagina.Total > 0) break;
            await Task.Delay(250);   // se escribe en segundo plano
        }
        var fila = Assert.Single(pagina.Elementos);
        Assert.Equal("admin", fila.Usuario);
        Assert.Contains(n, fila.Detalle);

        var filtros = (await admin.GetFromJsonAsync<FiltrosHistorialDto>("/api/v1/actividades/filtros"))!;
        Assert.Contains("Usuario", filtros.Entidades);
        Assert.Contains("Registrar", filtros.Acciones);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/actividades?desde=2026-10-25&hasta=2026-10-01")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await OperadorAsync()).GetAsync("/api/v1/actividades")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.CreateClient().GetAsync("/api/v1/actividades")).StatusCode);
        var limitado = (await admin.GetFromJsonAsync<PaginaDto<ActividadDto>>("/api/v1/actividades?tamanoPagina=9999"))!;
        Assert.Equal(100, limitado.TamanoPagina);
    }

    // ---------------- Eliminado suave y eliminación definitiva ----------------
    [Fact]
    public async Task DarDeBaja_GuardaEliminadoEn_ReactivarLoBorra_YElUsuarioNoPuedeIniciarSesion()
    {
        var admin = await _api.AdminAsync();
        var n = "suave" + Sufijo();
        var u = (await (await admin.PostAsJsonAsync("/api/v1/usuarios", new CrearUsuarioSolicitud(n, "Eliminado suave", $"{n}@e.com", 2, "ClaveTemporal_2026"))).Content.ReadFromJsonAsync<UsuarioDto>())!;
        Assert.Null(u.EliminadoEn);

        var baja = (await (await admin.PatchAsJsonAsync($"/api/v1/usuarios/{u.Id}/estado", new CambiarEstadoSolicitud(false))).Content.ReadFromJsonAsync<UsuarioDto>())!;
        Assert.False(baja.Activo);
        Assert.NotNull(baja.EliminadoEn);
        Assert.True(baja.EliminadoEn > DateTimeOffset.UtcNow.AddMinutes(-2));
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new LoginSolicitud(n, "ClaveTemporal_2026"))).StatusCode);

        var reactivado = (await (await admin.PatchAsJsonAsync($"/api/v1/usuarios/{u.Id}/estado", new CambiarEstadoSolicitud(true))).Content.ReadFromJsonAsync<UsuarioDto>())!;
        Assert.True(reactivado.Activo);
        Assert.Null(reactivado.EliminadoEn);
        Assert.True((await _api.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new LoginSolicitud(n, "ClaveTemporal_2026"))).IsSuccessStatusCode);
    }

    [Fact]
    public async Task EliminarDefinitivamente_SoloSiYaEstaDadoDeBaja_YDejaElRastroEnElHistorial()
    {
        var admin = await _api.AdminAsync();
        var n = "borrar" + Sufijo();
        var u = (await (await admin.PostAsJsonAsync("/api/v1/usuarios", new CrearUsuarioSolicitud(n, "Para eliminar", $"{n}@e.com", 2, "ClaveTemporal_2026"))).Content.ReadFromJsonAsync<UsuarioDto>())!;
        await _api.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new LoginSolicitud(n, "ClaveTemporal_2026"));   // genera historial a su nombre

        // Activo: no se puede (primero se da de baja)
        var activo = await admin.DeleteAsync($"/api/v1/usuarios/{u.Id}");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, activo.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/usuarios/{u.Id}")).StatusCode);

        await admin.PatchAsJsonAsync($"/api/v1/usuarios/{u.Id}/estado", new CambiarEstadoSolicitud(false));
        var eliminado = await admin.DeleteAsync($"/api/v1/usuarios/{u.Id}");
        Assert.Equal(HttpStatusCode.NoContent, eliminado.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/usuarios/{u.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/v1/usuarios/{u.Id}")).StatusCode);

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GestionPedidosDbContext>();
        for (var i = 0; i < 40 && !await db.Actividades.AnyAsync(a => a.Accion == "Eliminar" && a.Detalle!.Contains(n)); i++) await Task.Delay(250);
        Assert.True(await db.Actividades.AnyAsync(a => a.Accion == "Eliminar" && a.EntidadId == u.Id && a.Detalle!.Contains(n)));
        Assert.True(await db.Actividades.AnyAsync(a => a.Accion == "Login" && a.Detalle!.Contains(n)));   // su historial anterior se conserva
    }

    [Fact]
    public async Task Eliminar_NoSePuedeUnoMismo_NiUnOperador()
    {
        var admin = await _api.AdminAsync();
        var yo = (await admin.GetFromJsonAsync<UsuarioActualDto>("/api/v1/auth/yo"))!;
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.DeleteAsync($"/api/v1/usuarios/{yo.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await OperadorAsync()).DeleteAsync($"/api/v1/usuarios/{yo.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.CreateClient().DeleteAsync($"/api/v1/usuarios/{yo.Id}")).StatusCode);
    }
}
