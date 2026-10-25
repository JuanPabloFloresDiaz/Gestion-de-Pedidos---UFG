using GestionPedidos.Application.Autenticacion;
using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Clientes;

namespace GestionPedidos.Application.Tests;

public class RegistrarClienteWebTests
{
    private readonly UsuariosFake _usuarios = new();
    private readonly ClientesFake _clientes = new();
    private readonly UnitOfWorkFake _uow = new();
    private readonly EventosAplicacion _eventos = new();
    private readonly List<EntidadCambiadaServidorEventArgs> _cambios = new();

    public RegistrarClienteWebTests() => _eventos.EntidadCambiada += (_, e) => _cambios.Add(e);

    private RegistrarClienteWeb Caso() => new(_usuarios, new RolesFake(), _clientes, new HasherFake(), _uow, _eventos);

    private static RegistroClienteSolicitud Solicitud(string correo = "ana@ejemplo.com", string? confirmacion = null) =>
        new("Ana", "Pérez", "7777-1234", "Col. Escalón", correo, 5, "ClaveSegura_2026", confirmacion);

    [Fact]
    public async Task Registro_Nuevo_CreaUsuarioConRolCliente_YClienteEnlazado_EnUnaSolaTransaccion()
    {
        var r = await Caso().EjecutarAsync(Solicitud(), default);
        Assert.True(r.Exito);
        var usuario = Assert.Single(_usuarios.Agregados);
        Assert.Equal("Cliente", usuario.Rol!.Nombre);
        Assert.Equal("ana@ejemplo.com", usuario.NombreUsuario);
        var cliente = Assert.Single(_clientes.Agregados);
        Assert.Same(usuario, cliente.Usuario);
        Assert.Equal(1, _uow.Guardados);
        Assert.Equal(TipoCambioServidor.Registrada, Assert.Single(_cambios).Tipo);
    }

    [Fact]
    public async Task Registro_CorreoConCuenta_AvisaQueYaExiste()
    {
        _usuarios.Datos.Add(Fabrica.Usuario(1, "ana", Fabrica.Operador));   // correo ana@ejemplo.com
        var r = await Caso().EjecutarAsync(Solicitud(), default);
        Assert.Equal(CodigosError.CorreoEnUso, r.Error!.Codigo);
        Assert.Empty(_clientes.Agregados);
    }

    [Fact]
    public async Task Registro_ClienteExistenteSinCuenta_PideConfirmarElTelefono()
    {
        _clientes.Datos.Add(new Cliente { Nombres = "Ana", Apellidos = "Pérez", Telefono = "7777-1234", Correo = "ana@ejemplo.com" });
        var r = await Caso().EjecutarAsync(Solicitud(), default);
        Assert.Equal(CodigosError.ClienteExistente, r.Error!.Codigo);
        Assert.Empty(_usuarios.Agregados);   // nada se crea hasta confirmar
    }

    [Fact]
    public async Task Registro_ConTelefonoCorrecto_EnlazaSinDuplicarElCliente()
    {
        var existente = new Cliente { Nombres = "Ana", Apellidos = "Pérez", Telefono = "7777-1234", Correo = "ana@ejemplo.com" };
        _clientes.Datos.Add(existente);
        var r = await Caso().EjecutarAsync(Solicitud(confirmacion: "+503 7777 1234"), default);   // mismo número, otro formato
        Assert.True(r.Exito);
        Assert.Empty(_clientes.Agregados);              // no se duplica
        Assert.NotNull(existente.Usuario);               // se enlazó
        Assert.Equal(TipoCambioServidor.CuentaEnlazada, Assert.Single(_cambios).Tipo);
    }

    [Fact]
    public async Task Registro_ConTelefonoIncorrecto_NoEnlaza()
    {
        _clientes.Datos.Add(new Cliente { Nombres = "Ana", Apellidos = "Pérez", Telefono = "7777-1234", Correo = "ana@ejemplo.com" });
        var r = await Caso().EjecutarAsync(Solicitud(confirmacion: "2222-0000"), default);
        Assert.Equal(CodigosError.TelefonoNoCoincide, r.Error!.Codigo);
        Assert.Empty(_usuarios.Agregados);
    }

    [Fact]
    public async Task Registro_ClienteYaEnlazadoAOtraCuenta_NoSePuedeReclamar()
    {
        var c = new Cliente { Nombres = "Ana", Apellidos = "Pérez", Telefono = "7777-1234", Correo = "ana@ejemplo.com", UsuarioId = 99 };
        _clientes.Datos.Add(c);
        var r = await Caso().EjecutarAsync(Solicitud(confirmacion: "7777-1234"), default);
        Assert.Equal(CodigosError.CorreoEnUso, r.Error!.Codigo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-es-correo")]
    public async Task Registro_CorreoInvalido_Falla(string correo) =>
        Assert.Equal(TipoError.Validacion, (await Caso().EjecutarAsync(Solicitud(correo), default)).Error!.Tipo);

    [Fact]
    public async Task Registro_CiudadInexistente_Falla()
    {
        var s = Solicitud() with { CiudadId = 9999 };
        Assert.Equal(TipoError.Validacion, (await Caso().EjecutarAsync(s, default)).Error!.Tipo);
    }
}
