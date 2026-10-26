using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun;
using GestionPedidos.Ui.Comun.Api;
using GestionPedidos.Ui.Comun.Sesion;

namespace GestionPedidos.Ui.Tienda;

/// <summary>Estado y lógica del registro de clientes en la tienda: validación, departamento → ciudad y enlace con un cliente existente.</summary>
public sealed class RegistroViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private readonly SesionService _sesion;

    /// <summary>Crea el ViewModel.</summary>
    public RegistroViewModel(ApiClient api, SesionService sesion) { _api = api; _sesion = sesion; }

    /// <summary>Nombres.</summary>
    public string Nombres { get; set; } = "";
    /// <summary>Apellidos.</summary>
    public string Apellidos { get; set; } = "";
    /// <summary>Teléfono.</summary>
    public string Telefono { get; set; } = "";
    /// <summary>Correo (será su usuario).</summary>
    public string Correo { get; set; } = "";
    /// <summary>Dirección de entrega.</summary>
    public string Direccion { get; set; } = "";
    /// <summary>Contraseña.</summary>
    public string Clave { get; set; } = "";
    /// <summary>Repetición de la contraseña.</summary>
    public string RepetirClave { get; set; } = "";
    /// <summary>Teléfono para confirmar el enlace con un cliente existente.</summary>
    public string TelefonoConfirmacion { get; set; } = "";
    /// <summary>Departamento elegido.</summary>
    public int? DepartamentoId { get; private set; }
    /// <summary>Ciudad (distrito) elegida.</summary>
    public int? CiudadId { get; set; }
    /// <summary>Departamentos.</summary>
    public IReadOnlyList<DepartamentoDto> Departamentos { get; private set; } = Array.Empty<DepartamentoDto>();
    /// <summary>Ciudades del departamento elegido.</summary>
    public IReadOnlyList<CiudadDto> Ciudades { get; private set; } = Array.Empty<CiudadDto>();
    /// <summary>Errores por campo.</summary>
    public Dictionary<string, string> ErroresCampos { get; } = new();
    /// <summary>Indica que el correo ya pertenece a un cliente sin cuenta: hay que confirmar el teléfono.</summary>
    public bool PedirConfirmacion { get; private set; }

    /// <summary>Error de un campo, o <c>null</c>.</summary>
    public string? ErrorDe(string campo) => ErroresCampos.GetValueOrDefault(campo);

    /// <summary>Carga los departamentos.</summary>
    public Task IniciarAsync() => EjecutarAsync(async ct => Departamentos = await _api.GetAsync<List<DepartamentoDto>>("/api/v1/departamentos", ct));

    /// <summary>Elige un departamento y carga sus ciudades.</summary>
    public async Task ElegirDepartamentoAsync(int? id)
    {
        DepartamentoId = id; CiudadId = null; Ciudades = Array.Empty<CiudadDto>();
        if (id is not null) await EjecutarAsync(async ct => Ciudades = await _api.GetAsync<List<CiudadDto>>($"/api/v1/departamentos/{id}/ciudades", ct));
        else OnCambiado();
    }

    /// <summary>Valida en la interfaz (la API vuelve a validar) y registra; si sale bien, inicia la sesión. Devuelve la sesión o <c>null</c>.</summary>
    public async Task<SesionDto?> RegistrarAsync()
    {
        if (!Validar()) { OnCambiado(); return null; }
        SesionDto? sesion = null;
        var ok = await EjecutarAsync(async ct =>
        {
            await _api.PostAsync("/api/v1/auth/registro", new RegistroClienteSolicitud(Nombres.Trim(), Apellidos.Trim(), Telefono.Trim(),
                Direccion.Trim(), Correo.Trim(), CiudadId!.Value, Clave, PedirConfirmacion ? TelefonoConfirmacion.Trim() : null), ct);
            sesion = await _api.PostAsync<SesionDto>("/api/v1/auth/login", new LoginSolicitud(Correo.Trim(), Clave), ct);
            await _sesion.IniciarAsync(sesion);
        }, cancelarAnterior: false);
        Clave = RepetirClave = "";   // las contraseñas no se conservan en la pantalla
        return ok ? sesion : null;
    }

    /// <inheritdoc/>
    protected override bool InterpretarError(ApiException ex)
    {
        // Hay un cliente con ese correo (lo registró el personal): se pide el teléfono para enlazar la cuenta
        if (ex.Codigo == CodigosError.ClienteExistente) { PedirConfirmacion = true; Error = ex.MensajeParaUsuario; return true; }
        return false;
    }

    private bool Validar()
    {
        ErroresCampos.Clear();
        void R(string campo, string? error) { if (error is not null) ErroresCampos[campo] = error; }
        R(nameof(Nombres), Validacion.Primero(Nombres, Validacion.Texto("Los nombres", 100)));
        R(nameof(Apellidos), Validacion.Primero(Apellidos, Validacion.Texto("Los apellidos", 100)));
        R(nameof(Telefono), Telefono.Count(char.IsDigit) is >= 8 and <= 15 ? null : "Escriba un teléfono de 8 dígitos.");
        R(nameof(Correo), Validacion.Primero(Correo, Validacion.Correo()));
        R(nameof(Direccion), Validacion.Primero(Direccion, Validacion.Texto("La dirección", 250)));
        if (CiudadId is null) ErroresCampos[nameof(CiudadId)] = "Elija su departamento y su distrito.";
        R(nameof(Clave), Validacion.Primero(Clave, Validacion.Clave()));
        if (Clave != RepetirClave) ErroresCampos[nameof(RepetirClave)] = "Las contraseñas no coinciden.";
        if (PedirConfirmacion && Telefono.Count(char.IsDigit) >= 8 && TelefonoConfirmacion.Count(char.IsDigit) < 8)
            ErroresCampos[nameof(TelefonoConfirmacion)] = "Escriba el teléfono con el que lo registraron.";
        return ErroresCampos.Count == 0;
    }
}
