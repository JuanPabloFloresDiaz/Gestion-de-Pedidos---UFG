using GestionPedidos.Application.Common;
using GestionPedidos.Contracts;

namespace GestionPedidos.Application.Clientes;

/// <summary>Caso de uso: listar clientes con filtros combinados y paginación.</summary>
public sealed class ListarClientes
{
    private readonly IClienteRepository _clientes;

    /// <summary>Crea el caso de uso.</summary>
    public ListarClientes(IClienteRepository clientes) => _clientes = clientes;

    /// <summary>Devuelve una página de clientes filtrados.</summary>
    public async Task<PaginaDto<ClienteDto>> EjecutarAsync(string? q, int? departamentoId, int? ciudadId,
        bool? activo, int? pagina, int? tamano, CancellationToken ct)
    {
        var (p, t) = Paginacion.Normalizar(pagina, tamano);
        var (elementos, total) = await _clientes.BuscarAsync(q?.Trim(), departamentoId, ciudadId, activo, p, t, ct);
        return new PaginaDto<ClienteDto>(elementos.Select(c => c.ADto()).ToList(), total, p, t);
    }
}
