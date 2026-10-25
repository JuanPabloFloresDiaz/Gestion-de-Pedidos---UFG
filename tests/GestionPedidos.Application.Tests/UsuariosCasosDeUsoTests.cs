using GestionPedidos.Application.Autenticacion;
using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Application.Usuarios;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Application.Tests;

public class UsuariosCasosDeUsoTests
{
    private readonly UsuariosFake _usuarios = new();
    private readonly UnitOfWorkFake _uow = new();
    private readonly EventosAplicacion _eventos = new();
    private readonly List<EntidadCambiadaServidorEventArgs> _cambios = new();

    public UsuariosCasosDeUsoTests()
    {
        _usuarios.Datos.Add(Fabrica.Usuario(1, "admin", Fabrica.Admin));
        _usuarios.Datos.Add(Fabrica.Usuario(2, "ana", Fabrica.Operador));
        _eventos.EntidadCambiada += (_, e) => _cambios.Add(e);
    }

    private CrearUsuario Crear() => new(_usuarios, new RolesFake(), new HasherFake(), _uow, _eventos);
    private ActualizarUsuario Actualizar() => new(_usuarios, new RolesFake(), _uow, _eventos);
    private CambiarEstadoUsuario Estado() => new(_usuarios, _uow, _eventos);
    private CambiarClave Clave() => new(_usuarios, new HasherFake(), _uow, _eventos);

    // ---- Crear ----
    [Fact]
    public async Task Crear_Valido_CreaConClaveTemporal_YPublicaElEventoDespuesDeGuardar()
    {
        var r = await Crear().EjecutarAsync(new CrearUsuarioSolicitud("luis", "Luis Mora", "luis@ejemplo.com", 2, "ClaveTemporal_2026"), 1, default);
        Assert.True(r.Exito);
        Assert.True(r.Valor!.DebeCambiarClave);
        Assert.Equal(1, _uow.Guardados);
        var e = Assert.Single(_cambios);
        Assert.Equal(TipoCambioServidor.Registrada, e.Tipo);
        Assert.Equal(1, e.UsuarioId);   // quién lo hizo
    }

    [Theory]
    [InlineData("ab", "Luis", "luis@ejemplo.com", "ClaveTemporal_2026")]          // usuario muy corto
    [InlineData("luis", "", "luis@ejemplo.com", "ClaveTemporal_2026")]            // nombre vacío
    [InlineData("luis", "Luis", "no-es-correo", "ClaveTemporal_2026")]            // correo inválido
    [InlineData("luis", "Luis", "luis@ejemplo.com", "corta")]                     // clave débil
    public async Task Crear_ConDatosInvalidos_FallaSinGuardarNiPublicar(string usuario, string nombre, string correo, string clave)
    {
        var r = await Crear().EjecutarAsync(new CrearUsuarioSolicitud(usuario, nombre, correo, 2, clave), 1, default);
        Assert.Equal(TipoError.Validacion, r.Error!.Tipo);
        Assert.Equal(0, _uow.Guardados);
        Assert.Empty(_cambios);
    }

    [Fact]
    public async Task Crear_NombreDeUsuarioRepetido_Conflicto()
    {
        var r = await Crear().EjecutarAsync(new CrearUsuarioSolicitud("ana", "Otra Ana", "otra@ejemplo.com", 2, "ClaveTemporal_2026"), 1, default);
        Assert.Equal(TipoError.Conflicto, r.Error!.Tipo);
    }

    [Fact]
    public async Task Crear_ConRolCliente_NoSePermite()
    {
        var r = await Crear().EjecutarAsync(new CrearUsuarioSolicitud("cli", "Cliente", "cli@ejemplo.com", 3, "ClaveTemporal_2026"), 1, default);
        Assert.Equal(TipoError.Regla, r.Error!.Tipo);
    }

    // ---- Estado ----
    [Fact]
    public async Task CambiarEstado_NoPuedeDarseDeBajaASiMismo()
    {
        var r = await Estado().EjecutarAsync(1, false, actorId: 1, default);
        Assert.Equal(TipoError.Regla, r.Error!.Tipo);
        Assert.True(_usuarios.Datos[0].Activo);
    }

    [Fact]
    public async Task CambiarEstado_NoPuedeDarseDeBajaAlUltimoAdministrador()
    {
        _usuarios.Datos.Add(Fabrica.Usuario(3, "admin2", Fabrica.Operador));   // otro usuario, pero NO administrador
        var r = await Estado().EjecutarAsync(1, false, actorId: 3, default);
        Assert.Equal("Debe quedar al menos un administrador activo.", r.Error!.Mensaje);
    }

    [Fact]
    public async Task CambiarEstado_ConOtroAdministradorActivo_PermiteLaBaja_YPublica()
    {
        _usuarios.Datos.Add(Fabrica.Usuario(3, "admin2", Fabrica.Admin));
        var r = await Estado().EjecutarAsync(1, false, actorId: 3, default);
        Assert.True(r.Exito);
        Assert.False(_usuarios.Datos[0].Activo);
        Assert.Equal(TipoCambioServidor.EstadoCambiado, Assert.Single(_cambios).Tipo);
    }

    [Fact]
    public async Task CambiarEstado_SinCambioReal_NoPublicaNada()
    {
        var r = await Estado().EjecutarAsync(2, true, actorId: 1, default);   // ya estaba activo
        Assert.True(r.Exito);
        Assert.Empty(_cambios);
    }

    // ---- Actualizar ----
    [Fact]
    public async Task Actualizar_NoPuedeCambiarSuPropioRol()
    {
        var r = await Actualizar().EjecutarAsync(1, new ActualizarUsuarioSolicitud("Admin", "admin@ejemplo.com", 2, null), actorId: 1, default);
        Assert.Equal("No puede cambiar su propio rol.", r.Error!.Mensaje);
    }

    [Fact]
    public async Task Actualizar_UsuarioInactivo_PideReactivarlo()
    {
        _usuarios.Datos.Add(Fabrica.Usuario(4, "baja", Fabrica.Operador, activo: false));
        var r = await Actualizar().EjecutarAsync(4, new ActualizarUsuarioSolicitud("Baja", "baja@ejemplo.com", 2, null), 1, default);
        Assert.Equal(TipoError.Regla, r.Error!.Tipo);
    }

    [Fact]
    public async Task Actualizar_Valido_CambiaDatos_YPublica()
    {
        var r = await Actualizar().EjecutarAsync(2, new ActualizarUsuarioSolicitud("Ana María", "ana.maria@ejemplo.com", 2, null), 1, default);
        Assert.True(r.Exito);
        Assert.Equal("Ana María", r.Valor!.NombreCompleto);
        Assert.Equal(TipoCambioServidor.Actualizada, Assert.Single(_cambios).Tipo);
    }

    // ---- Cambiar clave ----
    [Fact]
    public async Task CambiarClave_ConClaveActualIncorrecta_Falla()
    {
        var r = await Clave().EjecutarAsync(2, new CambiarClaveSolicitud("incorrecta", "NuevaClave_Segura_1"), default);
        Assert.Equal(CodigosError.ClaveInvalida, r.Error!.Codigo);
    }

    [Fact]
    public async Task CambiarClave_NuevaIgualAActual_Falla()
    {
        var r = await Clave().EjecutarAsync(2, new CambiarClaveSolicitud("Clave_Valida_123", "Clave_Valida_123"), default);
        Assert.False(r.Exito);
    }

    [Fact]
    public async Task CambiarClave_Valida_ActualizaElHash_YPublica_SinGuardarLaClaveEnElDetalle()
    {
        var r = await Clave().EjecutarAsync(2, new CambiarClaveSolicitud("Clave_Valida_123", "NuevaClave_Segura_1"), default);
        Assert.True(r.Exito);
        Assert.Equal("hash:NuevaClave_Segura_1", _usuarios.Datos[1].ClaveHash);
        var e = Assert.Single(_cambios);
        Assert.DoesNotContain("NuevaClave", e.Detalle);
    }
}
