using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun;
using GestionPedidos.Ui.Comun.Api;
using GestionPedidos.Ui.Comun.Sesion;

namespace GestionPedidos.Ui.Shell;

/// <summary>Estado y lógica de la pantalla de inicio de sesión (sin referencias a Blazor).</summary>
public sealed class LoginViewModel : ViewModelBase
{
    private const string MensajeGenerico = "Usuario o contraseña incorrectos.";

    private readonly ApiClient _api;
    private readonly SesionService _sesion;
    private readonly UiOptions _opciones;

    /// <summary>Crea el ViewModel.</summary>
    public LoginViewModel(ApiClient api, SesionService sesion, UiOptions opciones)
    {
        _api = api; _sesion = sesion; _opciones = opciones;
    }

    /// <summary>Usuario o correo escrito.</summary>
    public string Identificador { get; set; } = "";
    /// <summary>Contraseña escrita.</summary>
    public string Clave { get; set; } = "";

    /// <summary>Intenta iniciar sesión. Devuelve la sesión si fue correcta, o <c>null</c> (con <see cref="ViewModelBase.Error"/>).</summary>
    public async Task<SesionDto?> IniciarAsync()
    {
        if (string.IsNullOrWhiteSpace(Identificador) || string.IsNullOrEmpty(Clave))
        {
            Error = "Escriba su usuario y su contraseña.";
            OnCambiado();
            return null;
        }

        SesionDto? resultado = null;
        var ok = await EjecutarAsync(async ct =>
        {
            var sesion = await _api.PostAsync<SesionDto>("/api/v1/auth/login", new LoginSolicitud(Identificador.Trim(), Clave), ct);
            // El escritorio es solo para el personal: un cliente de la tienda recibe el mensaje genérico.
            if (_opciones.RequiereAdminAcceso && !sesion.Usuario.Permisos.Contains(GestionPedidos.Contracts.Permisos.AdminAcceso))
                throw new ApiException(MensajeGenerico, 401);
            await _sesion.IniciarAsync(sesion);
            resultado = sesion;
        }, cancelarAnterior: false);

        Clave = "";   // la contraseña no se conserva en memoria de la pantalla
        return ok ? resultado : null;
    }
}
