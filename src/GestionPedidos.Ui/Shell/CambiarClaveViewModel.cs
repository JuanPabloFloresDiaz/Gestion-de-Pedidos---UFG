using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun;
using GestionPedidos.Ui.Comun.Api;
using GestionPedidos.Ui.Comun.Sesion;

namespace GestionPedidos.Ui.Shell;

/// <summary>Estado y lógica del cambio de contraseña (voluntario u obligatorio).</summary>
public sealed class CambiarClaveViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private readonly SesionService _sesion;

    /// <summary>Crea el ViewModel.</summary>
    public CambiarClaveViewModel(ApiClient api, SesionService sesion)
    {
        _api = api; _sesion = sesion;
    }

    /// <summary>Contraseña actual.</summary>
    public string ClaveActual { get; set; } = "";
    /// <summary>Contraseña nueva.</summary>
    public string ClaveNueva { get; set; } = "";
    /// <summary>Confirmación de la nueva.</summary>
    public string Confirmacion { get; set; } = "";

    /// <summary>Indica si el cambio es obligatorio (la sesión lo exige).</summary>
    public bool Obligatorio => _sesion.Actual?.DebeCambiarClave == true;

    /// <summary>Cambia la contraseña; si sale bien, la sesión se renueva con el token nuevo.</summary>
    public async Task<bool> CambiarAsync()
    {
        Error = Validacion.Primero(ClaveNueva, Validacion.Clave())
            ?? (ClaveNueva == Confirmacion ? null : "La confirmación no coincide con la contraseña nueva.")
            ?? (string.IsNullOrEmpty(ClaveActual) ? "Escriba su contraseña actual." : null);
        if (Error is not null) { OnCambiado(); return false; }

        var ok = await EjecutarAsync(async ct =>
        {
            var nueva = await _api.PostAsync<SesionDto>("/api/v1/auth/cambiar-clave",
                new CambiarClaveSolicitud(ClaveActual, ClaveNueva), ct);
            await _sesion.IniciarAsync(nueva);   // renueva el token (el anterior marcaba "debe cambiar la clave")
        }, cancelarAnterior: false);

        ClaveActual = ClaveNueva = Confirmacion = "";
        return ok;
    }
}
