using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun.Api;
using GestionPedidos.Ui.Comun.Eventos;

namespace GestionPedidos.Ui.Modulos.Clientes;

/// <summary>Operaciones de clientes contra la API. Publica <c>EntidadCambiada</c> solo tras confirmar la API ([Clientes.md §5]).</summary>
public sealed class ClientesService : IPublicadorDeCambios
{
    private readonly ApiClient _api;

    /// <summary>Crea el servicio.</summary>
    public ClientesService(ApiClient api) => _api = api;

    /// <summary>Se produce tras crear, editar o cambiar el estado de un cliente.</summary>
    public event EntidadCambiadaEventHandler? EntidadCambiada;

    /// <summary>Busca clientes con filtros combinados y paginación.</summary>
    public Task<PaginaDto<ClienteDto>> BuscarAsync(string? q, int? departamentoId, int? ciudadId, bool? activo,
        int pagina, int tamano, CancellationToken ct)
    {
        var consulta = new List<string> { $"pagina={pagina}", $"tamanoPagina={tamano}" };
        if (!string.IsNullOrWhiteSpace(q)) consulta.Add($"q={Uri.EscapeDataString(q.Trim())}");
        if (departamentoId is not null) consulta.Add($"departamentoId={departamentoId}");
        if (ciudadId is not null) consulta.Add($"ciudadId={ciudadId}");
        if (activo is not null) consulta.Add($"activo={activo.ToString()!.ToLowerInvariant()}");
        return _api.GetAsync<PaginaDto<ClienteDto>>($"/api/v1/clientes?{string.Join('&', consulta)}", ct);
    }

    /// <summary>Obtiene un cliente por identificador.</summary>
    public Task<ClienteDto> ObtenerAsync(int id, CancellationToken ct) =>
        _api.GetAsync<ClienteDto>($"/api/v1/clientes/{id}", ct);

    /// <summary>Lista los departamentos de El Salvador para el selector.</summary>
    public Task<List<DepartamentoDto>> DepartamentosAsync(CancellationToken ct) =>
        _api.GetAsync<List<DepartamentoDto>>("/api/v1/departamentos", ct);

    /// <summary>Lista las ciudades de un departamento para la lista dependiente.</summary>
    public Task<List<CiudadDto>> CiudadesAsync(int departamentoId, CancellationToken ct) =>
        _api.GetAsync<List<CiudadDto>>($"/api/v1/departamentos/{departamentoId}/ciudades", ct);

    /// <summary>Crea un cliente y notifica el ingreso.</summary>
    public async Task<ClienteDto> CrearAsync(CrearClienteSolicitud solicitud, CancellationToken ct)
    {
        var creado = await _api.PostAsync<ClienteDto>("/api/v1/clientes", solicitud, ct);
        OnEntidadCambiada(creado, TipoCambio.Registrada);
        return creado;
    }

    /// <summary>Actualiza un cliente y notifica la actualización.</summary>
    public async Task<ClienteDto> ActualizarAsync(int id, ActualizarClienteSolicitud solicitud, CancellationToken ct)
    {
        var actualizado = await _api.PutAsync<ClienteDto>($"/api/v1/clientes/{id}", solicitud, ct);
        OnEntidadCambiada(actualizado, TipoCambio.Actualizada);
        return actualizado;
    }

    /// <summary>Da de baja o reactiva un cliente y notifica el cambio de estado.</summary>
    public async Task<ClienteDto> CambiarEstadoAsync(int id, bool activo, CancellationToken ct)
    {
        var c = await _api.PatchAsync<ClienteDto>($"/api/v1/clientes/{id}/estado", new CambiarEstadoSolicitud(activo), ct);
        OnEntidadCambiada(c, TipoCambio.EstadoCambiado);
        return c;
    }

    private void OnEntidadCambiada(ClienteDto c, TipoCambio tipo) =>
        EntidadCambiada?.Invoke(this, new EntidadCambiadaEventArgs
        {
            Entidad = TipoEntidad.Cliente,
            Id = c.Id,
            Tipo = tipo,
            Descripcion = c.NombreCompleto,
            Activo = c.Activo,
        });
}
