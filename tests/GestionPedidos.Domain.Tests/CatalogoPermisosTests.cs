using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Domain.Tests;

public class CatalogoPermisosTests
{
    [Fact]
    public void Codigos_SonUnicos() =>
        Assert.Equal(CatalogoPermisos.Todos.Count, CatalogoPermisos.Todos.Select(p => p.Codigo).Distinct().Count());

    [Fact]
    public void Administrador_TieneTodoMenosLoDelCliente()
    {
        var admin = CatalogoPermisos.DeRol(NombresRol.Administrador);
        Assert.Equal(24, admin.Count);
        Assert.Contains(CatalogoPermisos.RolesEditar, admin);
        Assert.Contains(CatalogoPermisos.UsuariosEliminar, admin);
        Assert.Contains(CatalogoPermisos.CatalogosVer, admin);
        Assert.Contains(CatalogoPermisos.UsuariosCrear, admin);
        Assert.Contains(CatalogoPermisos.HistorialVer, admin);
        Assert.DoesNotContain(CatalogoPermisos.MisPedidosCrear, admin);
    }

    [Fact]
    public void Operador_NoGestionaUsuariosNiDaDeBajaNiVeHistorial()
    {
        var op = CatalogoPermisos.DeRol(NombresRol.Operador);
        Assert.Contains(CatalogoPermisos.AdminAcceso, op);
        Assert.Contains(CatalogoPermisos.PedidosCrear, op);
        Assert.Contains(CatalogoPermisos.ProductosVer, op);
        Assert.DoesNotContain(CatalogoPermisos.ProductosCrear, op);
        Assert.DoesNotContain(CatalogoPermisos.ClientesDesactivar, op);
        Assert.DoesNotContain(CatalogoPermisos.PedidosDesactivar, op);
        Assert.DoesNotContain(CatalogoPermisos.UsuariosVer, op);
        Assert.DoesNotContain(CatalogoPermisos.HistorialVer, op);
        Assert.DoesNotContain(CatalogoPermisos.RolesEditar, op);
        Assert.DoesNotContain(CatalogoPermisos.UsuariosEliminar, op);
        Assert.Contains(CatalogoPermisos.CatalogosVer, op);   // el formulario de clientes necesita las ciudades
    }

    [Fact]
    public void Cliente_NoEntraALaAdministracion()
    {
        var cliente = CatalogoPermisos.DeRol(NombresRol.Cliente);
        Assert.DoesNotContain(CatalogoPermisos.AdminAcceso, cliente);
        Assert.Contains(CatalogoPermisos.MisPedidosCrear, cliente);
        Assert.Contains(CatalogoPermisos.CatalogoVer, cliente);
    }

    [Fact]
    public void RolDesconocido_NoTienePermisos() => Assert.Empty(CatalogoPermisos.DeRol("Inventado"));

    [Fact]
    public void Catalogo_NuevosPermisosVanAlFinal_ParaNoAlterarLosIdDeLaSemilla()
    {
        // Los Id de la semilla son la posición en la lista: los 25 originales no pueden moverse.
        Assert.Equal(CatalogoPermisos.AdminAcceso, CatalogoPermisos.Todos[0].Codigo);
        Assert.Equal(CatalogoPermisos.PerfilEditar, CatalogoPermisos.Todos[24].Codigo);
        Assert.Equal(28, CatalogoPermisos.Todos.Count);
    }

    [Fact]
    public void PermisosProtegidos_DelAdministrador_SonLosDeAdministrarUsuariosYRoles()
    {
        var p = PermisosProtegidos.DelAdministrador;
        Assert.Contains(CatalogoPermisos.AdminAcceso, p);
        Assert.Contains(CatalogoPermisos.RolesEditar, p);
        Assert.Contains(CatalogoPermisos.UsuariosEliminar, p);
        Assert.DoesNotContain(CatalogoPermisos.HistorialVer, p);
        Assert.All(p, c => Assert.Contains(c, CatalogoPermisos.DeRol(NombresRol.Administrador)));
    }

    [Fact]
    public void Rol_EstablecerPermisos_SoloQuitaLosQueSobranYAnadeLosQueFaltan()
    {
        var rol = new Rol { Id = 2, Nombre = "Operador", Permisos = { new RolPermiso { RolId = 2, PermisoId = 1 }, new RolPermiso { RolId = 2, PermisoId = 2 } } };
        var conservado = rol.Permisos.Single(p => p.PermisoId == 2);
        rol.EstablecerPermisos(new[] { 2, 3 });
        Assert.Equal(new[] { 2, 3 }, rol.Permisos.Select(p => p.PermisoId).OrderBy(i => i));
        Assert.Contains(conservado, rol.Permisos);   // el que ya existía no se reemplaza
    }
}

public class ReglasPorRolTests
{
    [Theory]
    [InlineData(NombresRol.Administrador)]
    [InlineData(NombresRol.Operador)]
    [InlineData(NombresRol.Cliente)]
    public void LosPermisosDeLaSemillaDeCadaRol_RespetanSusPropiasReglas(string rol)
    {
        var (obligatorios, prohibidos) = PermisosProtegidos.ParaRol(rol);
        var semilla = CatalogoPermisos.DeRol(rol);
        Assert.All(obligatorios, c => Assert.Contains(c, semilla));
        Assert.All(semilla, c => Assert.DoesNotContain(c, prohibidos));
    }

    [Fact]
    public void Cliente_TieneProhibidosTodosLosDeAdministracion() =>
        Assert.Contains(CatalogoPermisos.UsuariosEliminar, PermisosProtegidos.ParaRol(NombresRol.Cliente).Prohibidos);

    [Fact]
    public void RolDesconocido_NoTieneReglas() =>
        Assert.Empty(PermisosProtegidos.ParaRol("Otro").Obligatorios);
}
