using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun.Api;
using GestionPedidos.Ui.Comun.Eventos;

namespace GestionPedidos.Ui.Modulos.Roles;

/// <summary>Operaciones de roles y permisos contra la API. Publica <c>EntidadCambiada</c> solo tras confirmar la API.</summary>
public sealed class RolesService : IPublicadorDeCambios
{
    private readonly ApiClient _api;

    /// <summary>Crea el servicio.</summary>
    public RolesService(ApiClient api) => _api = api;

    /// <summary>Se produce tras guardar los permisos de un rol.</summary>
    public event EntidadCambiadaEventHandler? EntidadCambiada;

    /// <summary>Lista los roles con sus permisos y reglas.</summary>
    public Task<List<RolDto>> RolesAsync(CancellationToken ct) => _api.GetAsync<List<RolDto>>("/api/v1/roles", ct);

    /// <summary>Lista el catálogo completo de permisos.</summary>
    public Task<List<PermisoDto>> PermisosAsync(CancellationToken ct) => _api.GetAsync<List<PermisoDto>>("/api/v1/permisos", ct);

    /// <summary>Guarda la lista completa de permisos de un rol y notifica el cambio.</summary>
    public async Task<RolDto> GuardarPermisosAsync(int rolId, IReadOnlyList<string> permisos, CancellationToken ct)
    {
        var rol = await _api.PutAsync<RolDto>($"/api/v1/roles/{rolId}/permisos", new ActualizarPermisosRolSolicitud(permisos), ct);
        EntidadCambiada?.Invoke(this, new EntidadCambiadaEventArgs
        {
            Entidad = TipoEntidad.Rol, Id = rol.Id, Tipo = TipoCambio.Actualizada, Descripcion = rol.Nombre,
        });
        return rol;
    }
}
