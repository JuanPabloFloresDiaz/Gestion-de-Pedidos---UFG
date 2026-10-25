using System.Net;
using GestionPedidos.Contracts;
using GestionPedidos.Ui.Shell;

namespace GestionPedidos.Ui.Tests;

public class LoginViewModelTests
{
    private static LoginViewModel Crear(Entorno e) => new(e.Api, e.Sesion, e.Opciones);

    private static void Programar(Entorno e, string rol, string[] permisos, bool debeCambiar = false) =>
        e.Manejador.Responder = _ => (HttpStatusCode.OK, Entorno.SesionDe(rol, permisos, e.Reloj.GetUtcNow().AddMinutes(60), debeCambiar));

    [Fact]
    public async Task Login_Correcto_IniciaLaSesion_YLimpiaLaClaveDeLaPantalla()
    {
        var e = new Entorno(); Programar(e, "Administrador", new[] { "admin.acceso" });
        var vm = Crear(e); vm.Identificador = " admin "; vm.Clave = "secreta";
        var sesion = await vm.IniciarAsync();
        Assert.NotNull(sesion);
        Assert.True(e.Sesion.Autenticada);
        Assert.Equal("", vm.Clave);   // la contraseña no se conserva
        Assert.Contains("\"identificador\":\"admin\"", e.Manejador.Cuerpos[0]);
    }

    [Fact]
    public async Task Login_ConCamposVacios_NoLlamaALaApi()
    {
        var e = new Entorno();
        var vm = Crear(e);
        Assert.Null(await vm.IniciarAsync());
        Assert.Equal("Escriba su usuario y su contraseña.", vm.Error);
        Assert.Empty(e.Manejador.Peticiones);
    }

    [Fact]
    public async Task Login_Rechazado401_MuestraElMensajeDeLaApi_SinSesion()
    {
        var e = new Entorno();
        e.Manejador.Responder = _ => (HttpStatusCode.Unauthorized, new { title = "Usuario o contraseña incorrectos.", codigo = "CREDENCIALES_INVALIDAS" });
        var vm = Crear(e); vm.Identificador = "admin"; vm.Clave = "mala";
        Assert.Null(await vm.IniciarAsync());
        Assert.Equal("Usuario o contraseña incorrectos.", vm.Error);
        Assert.False(e.Sesion.Autenticada);
    }

    [Fact]
    public async Task Escritorio_RechazaALosClientesDeLaTienda_ConElMensajeGenerico()
    {
        var e = new Entorno(); e.Opciones.RequiereAdminAcceso = true;
        Programar(e, "Cliente", new[] { "catalogo.ver", "mispedidos.ver" });   // credenciales válidas, pero sin admin.acceso
        var vm = Crear(e); vm.Identificador = "carla@e.com"; vm.Clave = "ClaveCliente_2026";
        Assert.Null(await vm.IniciarAsync());
        Assert.Equal("Usuario o contraseña incorrectos.", vm.Error);
        Assert.False(e.Sesion.Autenticada);
    }

    [Fact]
    public async Task Escritorio_AceptaAlPersonalConAccesoAdmin()
    {
        var e = new Entorno(); e.Opciones.RequiereAdminAcceso = true;
        Programar(e, "Operador", new[] { "admin.acceso" });
        var vm = Crear(e); vm.Identificador = "ana"; vm.Clave = "ClaveSegura_2026";
        Assert.NotNull(await vm.IniciarAsync());
    }

    [Fact]
    public async Task SinServidor_MuestraUnMensajeEntendible()
    {
        var e = new Entorno();
        e.Manejador.Responder = _ => throw new HttpRequestException("boom");
        var vm = Crear(e); vm.Identificador = "a"; vm.Clave = "b";
        await vm.IniciarAsync();
        Assert.Contains("No se pudo conectar", vm.Error);
    }

    [Fact]
    public async Task MientrasCarga_SeAvisaAlaPagina_ConElEventoCambiado()
    {
        var e = new Entorno(); Programar(e, "Administrador", new[] { "admin.acceso" });
        var vm = Crear(e); vm.Identificador = "admin"; vm.Clave = "x";
        var cambios = new List<bool>();
        vm.Cambiado += (_, _) => cambios.Add(vm.Cargando);
        await vm.IniciarAsync();
        Assert.Equal(new[] { true, false }, cambios);   // empieza a cargar → termina
    }
}
