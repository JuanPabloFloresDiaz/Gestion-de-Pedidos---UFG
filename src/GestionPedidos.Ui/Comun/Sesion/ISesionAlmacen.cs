using GestionPedidos.Contracts;

namespace GestionPedidos.Ui.Comun.Sesion;

/// <summary>
/// Dónde se guarda la sesión. Lo implementa cada hospedaje: memoria del proceso en el escritorio y
/// <c>ProtectedSessionStorage</c> (cifrado, ligado a la pestaña) en la web. Nunca <c>localStorage</c>.
/// </summary>
public interface ISesionAlmacen
{
    /// <summary>Guarda la sesión.</summary>
    Task GuardarAsync(SesionDto sesion);
    /// <summary>Lee la sesión guardada, o <c>null</c>.</summary>
    Task<SesionDto?> LeerAsync();
    /// <summary>Borra la sesión.</summary>
    Task BorrarAsync();
}

/// <summary>Almacén en memoria del proceso (escritorio y pruebas).</summary>
public sealed class SesionAlmacenMemoria : ISesionAlmacen
{
    private SesionDto? _sesion;

    /// <inheritdoc/>
    public Task GuardarAsync(SesionDto sesion) { _sesion = sesion; return Task.CompletedTask; }
    /// <inheritdoc/>
    public Task<SesionDto?> LeerAsync() => Task.FromResult(_sesion);
    /// <inheritdoc/>
    public Task BorrarAsync() { _sesion = null; return Task.CompletedTask; }
}
