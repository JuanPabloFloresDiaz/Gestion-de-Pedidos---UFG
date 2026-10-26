using GestionPedidos.Application.Catalogos;
using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Application.Historial;
using GestionPedidos.Application.Roles;
using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Application.Tests;

public class RolesCatalogosHistorialTests
{
    private readonly RolesFake _roles = new();
    private readonly UnitOfWorkFake _uow = new();
    private readonly EventosAplicacion _eventos = new();
    private readonly List<EntidadCambiadaServidorEventArgs> _cambios = new();

    public RolesCatalogosHistorialTests() => _eventos.EntidadCambiada += (_, e) => _cambios.Add(e);

    private Task<Resultado<GestionPedidos.Contracts.RolDto>> Actualizar(int rolId, params string[] codigos) =>
        new ActualizarPermisosDeRol(_roles, _uow, _eventos).EjecutarAsync(rolId, codigos, actorId: 1, default);

    private static string[] Con(int rolId, Func<IEnumerable<string>, IEnumerable<string>> cambio) =>
        cambio(CatalogoPermisos.DeRol(new[] { "", NombresRol.Administrador, NombresRol.Operador, NombresRol.Cliente }[rolId])).ToArray();

    // ---- Permisos de un rol ----
    [Fact]
    public async Task Operador_QuitarUnPermisoYAnadirOtro_Guarda_YPublicaElDetalleDelCambio()
    {
        var nuevos = Con(2, p => p.Where(c => c != CatalogoPermisos.ClientesEditar).Append(CatalogoPermisos.ProductosCrear));
        var r = await Actualizar(2, nuevos);
        Assert.True(r.Exito);
        Assert.Equal(1, _uow.Guardados);
        var catalogo = await _roles.ListarPermisosAsync(default);
        var idProductosCrear = catalogo.First(x => x.Codigo == CatalogoPermisos.ProductosCrear).Id;
        var idClientesEditar = catalogo.First(x => x.Codigo == CatalogoPermisos.ClientesEditar).Id;
        var ids = _roles.Editable(2).Permisos.Select(p => p.PermisoId).ToList();
        Assert.Contains(idProductosCrear, ids);
        Assert.DoesNotContain(idClientesEditar, ids);
        var e = Assert.Single(_cambios);
        Assert.Equal("Rol", e.Entidad);
        Assert.Contains("+1 -1", e.Detalle);
        Assert.Contains("productos.crear", e.Detalle);
        Assert.Contains("clientes.editar", e.Detalle);
    }

    [Fact]
    public async Task SinCambios_NoGuardaNiPublica()
    {
        var r = await Actualizar(2, Con(2, p => p));
        Assert.True(r.Exito);
        Assert.Equal(0, _uow.Guardados);
        Assert.Empty(_cambios);
    }

    [Theory]
    [InlineData(CatalogoPermisos.AdminAcceso)]
    [InlineData(CatalogoPermisos.UsuariosVer)]
    [InlineData(CatalogoPermisos.RolesEditar)]
    [InlineData(CatalogoPermisos.UsuariosEliminar)]
    public async Task Administrador_NoPuedePerderSusPermisosDeAdministracion(string protegido)
    {
        var r = await Actualizar(1, Con(1, p => p.Where(c => c != protegido)));
        Assert.Equal(TipoError.Regla, r.Error!.Tipo);
        Assert.Contains(protegido, r.Error.Mensaje);
        Assert.Equal(0, _uow.Guardados);
    }

    [Fact]
    public async Task Administrador_PuedePerderPermisosNoProtegidos()
    {
        var r = await Actualizar(1, Con(1, p => p.Where(c => c != CatalogoPermisos.HistorialVer)));
        Assert.True(r.Exito);
        Assert.DoesNotContain(CatalogoPermisos.HistorialVer, r.Valor!.Permisos);
    }

    [Fact]
    public async Task Operador_SinAdminAcceso_NoSePermite()
    {
        var r = await Actualizar(2, Con(2, p => p.Where(c => c != CatalogoPermisos.AdminAcceso)));
        Assert.Contains("admin.acceso", r.Error!.Mensaje);   // obligatorio para el personal
    }

    [Fact]
    public async Task Cliente_NoPuedeRecibirPermisosDeAdministracion()
    {
        var r = await Actualizar(3, Con(3, p => p.Append(CatalogoPermisos.UsuariosVer)));
        Assert.Equal(TipoError.Regla, r.Error!.Tipo);
        Assert.Contains("usuarios.ver", r.Error.Mensaje);
    }

    [Fact]
    public async Task Personal_NoPuedeRecibirPermisosExclusivosDelCliente()
    {
        var r = await Actualizar(2, Con(2, p => p.Append(CatalogoPermisos.MisPedidosCrear)));
        Assert.Contains("no se pueden asignar", r.Error!.Mensaje);
    }

    [Fact]
    public async Task PermisoInexistente_DaValidacion()
    {
        var r = await Actualizar(2, Con(2, p => p.Append("inventado.permiso")));
        Assert.Equal(TipoError.Validacion, r.Error!.Tipo);
        Assert.Contains("inventado.permiso", r.Error.Mensaje);
    }

    [Fact]
    public async Task RolInexistente_DaNoEncontrado() =>
        Assert.Equal(TipoError.NoEncontrado, (await Actualizar(99, "admin.acceso")).Error!.Tipo);

    [Fact]
    public async Task ListarPermisos_DevuelveElCatalogoCompleto() =>
        Assert.Equal(CatalogoPermisos.Todos.Count, (await new ListarPermisos(_roles).EjecutarAsync(default)).Count);

    // ---- Catálogos ----
    [Fact]
    public async Task Ciudades_DepartamentoInexistente_DaNoEncontrado()
    {
        var r = await new ListarCiudades(new CatalogoGeoFake()).EjecutarAsync(99, null, null, null, default);
        Assert.Equal(TipoError.NoEncontrado, r.Error!.Tipo);
    }

    [Fact]
    public async Task Ciudades_NormalizaPaginacion_YRecortaElTexto()
    {
        var geo = new CatalogoGeoFake();
        var r = await new ListarCiudades(geo).EjecutarAsync(1, "  soya  ", 0, 5000, default);
        Assert.True(r.Exito);
        Assert.Equal((1, "soya", 1, 100), geo.UltimaBusqueda);   // página mínima 1, tamaño máximo 100
    }

    // ---- Historial ----
    [Fact]
    public async Task Historial_FechaInicialPosteriorALaFinal_DaValidacion()
    {
        var r = await new ListarActividades(new ActividadesFake()).EjecutarAsync(null, null, null, null,
            new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 1), null, null, default);
        Assert.Equal(TipoError.Validacion, r.Error!.Tipo);
    }

    [Fact]
    public async Task Historial_ElDiaFinalSeIncluyeCompleto_YLosFiltrosVaciosSeIgnoran()
    {
        var repo = new ActividadesFake();
        var r = await new ListarActividades(repo).EjecutarAsync(null, " ", "  ", "Login", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 25), 1, 20, default);
        Assert.True(r.Exito);
        var u = repo.Ultima!.Value;
        Assert.Null(u.Entidad);                                   // vacío = sin filtro
        Assert.Equal("Login", u.Accion);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), u.Desde);
        Assert.Equal(new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero), u.Hasta);   // hasta el 25 inclusive = < 26 a las 00:00
    }
}
