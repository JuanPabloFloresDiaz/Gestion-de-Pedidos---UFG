using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun.Eventos;

namespace GestionPedidos.Ui.Comun.Sesion;

/// <summary>
/// Sesión del usuario en la interfaz: guarda el JWT (60 minutos), vigila su vencimiento y publica
/// <c>SesionIniciada</c>, <c>SesionPorExpirar</c> (5 min antes), <c>SesionCerrada</c> y <c>SesionExpirada</c>.
/// </summary>
public sealed class SesionService : IDisposable
{
    /// <summary>Minutos antes del vencimiento en que se avisa.</summary>
    public const int MinutosDeAviso = 5;

    private readonly ISesionAlmacen _almacen;
    private readonly TimeProvider _reloj;
    private readonly ITimer _temporizador;
    private bool _avisada;

    /// <summary>Crea el servicio y arranca el vigilante de vencimiento (revisa cada 15 s).</summary>
    public SesionService(ISesionAlmacen almacen, TimeProvider reloj)
    {
        _almacen = almacen;
        _reloj = reloj;
        _temporizador = reloj.CreateTimer(_ => Vigilar(), null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
    }

    /// <summary>Se produce al iniciar sesión.</summary>
    public event SesionEventHandler? SesionIniciada;
    /// <summary>Se produce cuando faltan <see cref="MinutosDeAviso"/> minutos para el vencimiento.</summary>
    public event SesionEventHandler? SesionPorExpirar;
    /// <summary>Se produce al cerrar sesión voluntariamente.</summary>
    public event SesionEventHandler? SesionCerrada;
    /// <summary>Se produce cuando el token venció o la API lo rechazó (401).</summary>
    public event SesionEventHandler? SesionExpirada;
    /// <summary>Se produce cuando terminó de leerse la sesión guardada (en la web ocurre tras el primer render).</summary>
    public event EventHandler? Restaurada;

    /// <summary>Sesión actual, o <c>null</c> si no hay.</summary>
    public SesionDto? Actual { get; private set; }
    /// <summary>Indica si ya se intentó restaurar la sesión guardada.</summary>
    public bool Lista { get; private set; }
    /// <summary>Indica si hay una sesión vigente.</summary>
    public bool Autenticada => Actual is not null;
    /// <summary>Usuario de la sesión, o <c>null</c>.</summary>
    public UsuarioActualDto? Usuario => Actual?.Usuario;
    /// <summary>Token para el encabezado <c>Authorization</c>, o <c>null</c>.</summary>
    public string? Token => Actual?.AccessToken;

    /// <summary>Indica si el usuario tiene el permiso dado.</summary>
    public bool TienePermiso(string codigo) => Actual?.Usuario.Permisos.Contains(codigo) == true;

    /// <summary>Lee la sesión guardada (si existe y no venció) y avisa con <see cref="Restaurada"/>.</summary>
    public async Task RestaurarAsync()
    {
        if (Lista) return;
        var guardada = await _almacen.LeerAsync();
        if (guardada is not null && guardada.ExpiraEn > _reloj.GetUtcNow())
        {
            Actual = guardada;
            _avisada = false;
        }
        else if (guardada is not null) await _almacen.BorrarAsync();
        Lista = true;
        Restaurada?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Inicia (o renueva, tras cambiar la clave) la sesión.</summary>
    public async Task IniciarAsync(SesionDto sesion)
    {
        var eraNueva = Actual is null;
        Actual = sesion;
        Lista = true;
        _avisada = false;
        await _almacen.GuardarAsync(sesion);
        if (eraNueva) SesionIniciada?.Invoke(this, Datos());
    }

    /// <summary>Cierra la sesión por decisión del usuario.</summary>
    public async Task CerrarAsync()
    {
        var datos = Datos();
        Actual = null;
        await _almacen.BorrarAsync();
        SesionCerrada?.Invoke(this, datos);
    }

    /// <summary>Marca la sesión como vencida (el token venció o la API respondió 401).</summary>
    public async Task ExpirarAsync()
    {
        if (Actual is null) return;
        var datos = Datos();
        Actual = null;
        await _almacen.BorrarAsync();
        SesionExpirada?.Invoke(this, datos);
    }

    private SesionEventArgs Datos() => new()
    {
        NombreUsuario = Actual?.Usuario.NombreUsuario ?? "",
        Rol = Actual?.Usuario.Rol ?? "",
        MinutosRestantes = Actual is null ? 0 : (int)Math.Max(0, (Actual.ExpiraEn - _reloj.GetUtcNow()).TotalMinutes),
    };

    // Revisa el vencimiento: avisa una vez a 5 minutos y expira al llegar la hora.
    private void Vigilar()
    {
        var sesion = Actual;
        if (sesion is null) return;
        var restante = sesion.ExpiraEn - _reloj.GetUtcNow();
        if (restante <= TimeSpan.Zero) _ = ExpirarAsync();
        else if (restante <= TimeSpan.FromMinutes(MinutosDeAviso) && !_avisada)
        {
            _avisada = true;
            SesionPorExpirar?.Invoke(this, Datos());
        }
    }

    /// <summary>Detiene el vigilante.</summary>
    public void Dispose() => _temporizador.Dispose();
}
