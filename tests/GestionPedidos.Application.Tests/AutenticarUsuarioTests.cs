using GestionPedidos.Application.Autenticacion;
using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Seguridad;
using Microsoft.Extensions.Time.Testing;

namespace GestionPedidos.Application.Tests;

public class AutenticarUsuarioTests
{
    private readonly UsuariosFake _usuarios = new();
    private readonly HasherFake _hasher = new();
    private readonly UnitOfWorkFake _uow = new();
    private readonly FakeTimeProvider _reloj = new(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero));
    private readonly EventosAplicacion _eventos = new();
    private readonly List<AccesoRegistradoEventArgs> _accesos = new();
    private readonly AutenticarUsuario _caso;

    public AutenticarUsuarioTests()
    {
        _usuarios.Datos.Add(Fabrica.Usuario(1, "ana", Fabrica.Operador));
        _eventos.AccesoRegistrado += (_, e) => _accesos.Add(e);   // suscriptor falso
        _caso = new AutenticarUsuario(_usuarios, new RolesFake(), _hasher, new TokensFake(), _uow, _reloj, _eventos);
    }

    private Task<Resultado<SesionDto>> Login(string id, string clave) =>
        _caso.EjecutarAsync(new LoginSolicitud(id, clave), CancellationToken.None);

    [Fact]
    public async Task Login_Correcto_DevuelveSesionConPermisosDelRol_YPublicaElEvento()
    {
        var r = await Login("ana", "Clave_Valida_123");
        Assert.True(r.Exito);
        Assert.Equal(NombresRol.Operador, r.Valor!.Usuario.Rol);
        Assert.Contains("pedidos.crear", r.Valor.Usuario.Permisos);
        Assert.DoesNotContain("usuarios.ver", r.Valor.Usuario.Permisos);
        var e = Assert.Single(_accesos);
        Assert.True(e.Exitoso);
        Assert.Equal(1, e.UsuarioId);
    }

    [Fact]
    public async Task Login_PorCorreo_Funciona() =>
        Assert.True((await Login("ANA@ejemplo.com", "Clave_Valida_123")).Exito);

    [Fact]
    public async Task Login_ClaveIncorrecta_MensajeGenerico_YEventoFallido()
    {
        var r = await Login("ana", "mala");
        Assert.False(r.Exito);
        Assert.Equal("Usuario o contraseña incorrectos.", r.Error!.Mensaje);
        Assert.Equal(TipoError.NoAutenticado, r.Error.Tipo);
        Assert.False(Assert.Single(_accesos).Exitoso);
    }

    [Fact]
    public async Task Login_UsuarioInexistente_MismoMensaje_YGastaTiempoDeHash()
    {
        var r = await Login("fantasma", "Clave_Valida_123");
        Assert.Equal("Usuario o contraseña incorrectos.", r.Error!.Mensaje);
        Assert.Equal(1, _hasher.TiemposGastados);   // evita distinguir usuarios por tiempos de respuesta
        Assert.Null(Assert.Single(_accesos).UsuarioId);
    }

    [Fact]
    public async Task Login_UsuarioInactivo_MismoMensajeGenerico()
    {
        _usuarios.Datos.Add(Fabrica.Usuario(2, "baja", Fabrica.Operador, activo: false));
        var r = await Login("baja", "Clave_Valida_123");
        Assert.Equal(CodigosError.CredencialesInvalidas, r.Error!.Codigo);
    }

    [Fact]
    public async Task Login_CincoFallos_BloqueaAunqueLaClaveSeaCorrecta_YSeLiberaAlPasarElTiempo()
    {
        for (var i = 0; i < Usuario.MaximoIntentosFallidos; i++) await Login("ana", "mala");
        Assert.False((await Login("ana", "Clave_Valida_123")).Exito);   // bloqueado: mismo mensaje genérico

        _reloj.Advance(Usuario.DuracionBloqueo + TimeSpan.FromSeconds(1));
        Assert.True((await Login("ana", "Clave_Valida_123")).Exito);
    }

    [Fact]
    public async Task Login_ConCambioObligatorio_LoMarcaEnLaSesion()
    {
        var u = Fabrica.Usuario(3, "nuevo", Fabrica.Operador);
        u.CambiarClave("hash:Temporal_123", debeCambiarClave: true);
        _usuarios.Datos.Add(u);
        var r = await Login("nuevo", "Temporal_123");
        Assert.True(r.Valor!.DebeCambiarClave);
        Assert.EndsWith("-debe", r.Valor.AccessToken);
    }

    [Fact]
    public async Task Login_SinSuscriptores_NoFalla()
    {
        var sinSuscriptores = new AutenticarUsuario(_usuarios, new RolesFake(), _hasher, new TokensFake(), _uow, _reloj, new EventosAplicacion());
        Assert.True((await sinSuscriptores.EjecutarAsync(new LoginSolicitud("ana", "Clave_Valida_123"), CancellationToken.None)).Exito);
    }
}
