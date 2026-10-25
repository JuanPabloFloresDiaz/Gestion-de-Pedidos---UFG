using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun.Eventos;

namespace GestionPedidos.Ui.Tests;

public class SesionServiceTests
{
    private static readonly string[] Admin = { "admin.acceso", "usuarios.ver" };

    [Fact]
    public async Task Iniciar_PublicaSesionIniciada_YGuardaEnElAlmacen()
    {
        var e = new Entorno();
        var eventos = new List<string>();
        e.Sesion.SesionIniciada += (_, a) => eventos.Add(a.NombreUsuario);
        await e.Sesion.IniciarAsync(Entorno.SesionDe("Administrador", Admin, e.Reloj.GetUtcNow().AddMinutes(60)));
        Assert.Equal(new[] { "administrador" }, eventos);
        Assert.True(e.Sesion.Autenticada);
        Assert.True(e.Sesion.TienePermiso("usuarios.ver"));
        Assert.False(e.Sesion.TienePermiso("usuarios.crear"));
        Assert.NotNull(await e.Almacen.LeerAsync());
    }

    [Fact]
    public async Task Restaurar_ConSesionVigente_LaRecupera_YAvisaConElEventoRestaurada()
    {
        var e = new Entorno();
        await e.Almacen.GuardarAsync(Entorno.SesionDe("Operador", Admin, e.Reloj.GetUtcNow().AddMinutes(30)));
        var avisos = 0;
        e.Sesion.Restaurada += (_, _) => avisos++;
        await e.Sesion.RestaurarAsync();
        Assert.True(e.Sesion.Lista);
        Assert.True(e.Sesion.Autenticada);
        Assert.Equal(1, avisos);
    }

    [Fact]
    public async Task Restaurar_ConSesionVencida_NoLaRecupera_YLaBorra()
    {
        var e = new Entorno();
        await e.Almacen.GuardarAsync(Entorno.SesionDe("Operador", Admin, e.Reloj.GetUtcNow().AddMinutes(-1)));
        await e.Sesion.RestaurarAsync();
        Assert.False(e.Sesion.Autenticada);
        Assert.Null(await e.Almacen.LeerAsync());
    }

    [Fact]
    public async Task Avisa5MinutosAntes_UnaSolaVez_YExpiraAlLlegarLaHora()
    {
        var e = new Entorno();
        var porExpirar = new List<int>(); var expiradas = 0;
        e.Sesion.SesionPorExpirar += (_, a) => porExpirar.Add(a.MinutosRestantes);
        e.Sesion.SesionExpirada += (_, _) => expiradas++;
        await e.Sesion.IniciarAsync(Entorno.SesionDe("Administrador", Admin, e.Reloj.GetUtcNow().AddMinutes(60)));

        e.Reloj.Advance(TimeSpan.FromMinutes(54));            // faltan 6 min: aún no avisa
        Assert.Empty(porExpirar);
        e.Reloj.Advance(TimeSpan.FromMinutes(1.5));           // faltan 4,5 min: avisa
        e.Reloj.Advance(TimeSpan.FromMinutes(1));             // sigue vigente: NO avisa de nuevo
        Assert.Single(porExpirar);
        Assert.Equal(0, expiradas);

        e.Reloj.Advance(TimeSpan.FromMinutes(5));             // ya pasó la hora
        await Task.Delay(50);
        Assert.Equal(1, expiradas);
        Assert.False(e.Sesion.Autenticada);
    }

    [Fact]
    public async Task Cerrar_PublicaSesionCerrada_YBorraElAlmacen()
    {
        var e = new Entorno();
        var cerradas = 0;
        e.Sesion.SesionCerrada += (_, _) => cerradas++;
        await e.Sesion.IniciarAsync(Entorno.SesionDe("Administrador", Admin, e.Reloj.GetUtcNow().AddMinutes(60)));
        await e.Sesion.CerrarAsync();
        Assert.Equal(1, cerradas);
        Assert.False(e.Sesion.Autenticada);
        Assert.Null(await e.Almacen.LeerAsync());
    }
}
