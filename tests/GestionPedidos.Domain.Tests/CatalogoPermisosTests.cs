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
        Assert.Equal(21, admin.Count);
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
}
