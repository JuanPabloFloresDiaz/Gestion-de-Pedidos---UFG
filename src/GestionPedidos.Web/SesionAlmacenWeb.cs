using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun.Sesion;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace GestionPedidos.Web;

/// <summary>
/// Almacén de sesión de la web: <c>ProtectedSessionStorage</c> (cifrado por ASP.NET Core y ligado a la pestaña del
/// navegador). Sobrevive a F5 y se borra al cerrar la pestaña; nunca se usa <c>localStorage</c>.
/// </summary>
public sealed class SesionAlmacenWeb : ISesionAlmacen
{
    private const string Clave = "gestionpedidos.sesion";
    private readonly ProtectedSessionStorage _almacen;

    /// <summary>Crea el almacén.</summary>
    public SesionAlmacenWeb(ProtectedSessionStorage almacen) => _almacen = almacen;

    /// <inheritdoc/>
    public async Task GuardarAsync(SesionDto sesion) => await _almacen.SetAsync(Clave, sesion);

    /// <inheritdoc/>
    public async Task<SesionDto?> LeerAsync()
    {
        try
        {
            var r = await _almacen.GetAsync<SesionDto>(Clave);
            return r.Success ? r.Value : null;
        }
        catch (InvalidOperationException) { return null; }   // el circuito aún no puede usar JavaScript
        catch (System.Security.Cryptography.CryptographicException) { return null; }   // dato de otra instancia: se ignora
    }

    /// <inheritdoc/>
    public async Task BorrarAsync() => await _almacen.DeleteAsync(Clave);
}
