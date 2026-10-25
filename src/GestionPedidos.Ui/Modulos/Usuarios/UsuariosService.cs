using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun.Api;
using GestionPedidos.Ui.Comun.Eventos;

namespace GestionPedidos.Ui.Modulos.Usuarios;

/// <summary>Operaciones de usuarios contra la API. Publica <c>EntidadCambiada</c> solo tras confirmar la API.</summary>
public sealed class UsuariosService : IPublicadorDeCambios
{
    private readonly ApiClient _api;

    /// <summary>Crea el servicio.</summary>
    public UsuariosService(ApiClient api) => _api = api;

    /// <summary>Se produce tras crear, editar o cambiar el estado de un usuario.</summary>
    public event EntidadCambiadaEventHandler? EntidadCambiada;

    /// <summary>Busca usuarios con filtros y paginación.</summary>
    public Task<PaginaDto<UsuarioDto>> BuscarAsync(string? q, int? rolId, bool? activo, int pagina, int tamano, CancellationToken ct)
    {
        var consulta = new List<string> { $"pagina={pagina}", $"tamanoPagina={tamano}" };
        if (!string.IsNullOrWhiteSpace(q)) consulta.Add($"q={Uri.EscapeDataString(q.Trim())}");
        if (rolId is not null) consulta.Add($"rolId={rolId}");
        if (activo is not null) consulta.Add($"activo={activo.ToString()!.ToLowerInvariant()}");
        return _api.GetAsync<PaginaDto<UsuarioDto>>($"/api/v1/usuarios?{string.Join('&', consulta)}", ct);
    }

    /// <summary>Lista los roles.</summary>
    public Task<List<RolDto>> RolesAsync(CancellationToken ct) => _api.GetAsync<List<RolDto>>("/api/v1/roles", ct);

    /// <summary>Crea un usuario y notifica el ingreso.</summary>
    public async Task<UsuarioDto> CrearAsync(CrearUsuarioSolicitud solicitud, CancellationToken ct)
    {
        var creado = await _api.PostAsync<UsuarioDto>("/api/v1/usuarios", solicitud, ct);   // si falla, no se notifica nada
        OnEntidadCambiada(creado, TipoCambio.Registrada);
        return creado;
    }

    /// <summary>Actualiza un usuario y notifica la actualización.</summary>
    public async Task<UsuarioDto> ActualizarAsync(int id, ActualizarUsuarioSolicitud solicitud, CancellationToken ct)
    {
        var actualizado = await _api.PutAsync<UsuarioDto>($"/api/v1/usuarios/{id}", solicitud, ct);
        OnEntidadCambiada(actualizado, TipoCambio.Actualizada);
        return actualizado;
    }

    /// <summary>Da de baja o reactiva un usuario y notifica el cambio de estado.</summary>
    public async Task<UsuarioDto> CambiarEstadoAsync(int id, bool activo, CancellationToken ct)
    {
        var u = await _api.PatchAsync<UsuarioDto>($"/api/v1/usuarios/{id}/estado", new CambiarEstadoSolicitud(activo), ct);
        OnEntidadCambiada(u, TipoCambio.EstadoCambiado);
        return u;
    }

    private void OnEntidadCambiada(UsuarioDto u, TipoCambio tipo) =>
        EntidadCambiada?.Invoke(this, new EntidadCambiadaEventArgs
        {
            Entidad = TipoEntidad.Usuario, Id = u.Id, Tipo = tipo, Descripcion = u.NombreCompleto, Activo = u.Activo,
        });
}
