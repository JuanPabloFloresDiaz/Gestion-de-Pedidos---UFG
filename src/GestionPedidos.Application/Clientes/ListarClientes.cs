using GestionPedidos.Application.Common;
using GestionPedidos.Contracts;

namespace GestionPedidos.Application.Clientes;

/// <summary>Caso de uso: listar clientes con filtros combinados y paginación.</summary>
public sealed class ListarClientes
{
    private readonly IClienteRepository _clientes;

    /// <summary>Crea el caso de uso.</summary>
    public ListarClientes(IClienteRepository clientes) => _clientes = clientes;

    /// <summary>
    /// Devuelve una página de clientes filtrados. Sin filtro de estado devuelve solo los ACTIVOS ([Busquedas.md §1]);
    /// para ver también los dados de baja hay que pedirlo (<paramref name="incluirInactivos"/>) o filtrar por <c>activo=false</c>.
    /// </summary>
    /// <param name="q">Texto a buscar en nombres, apellidos, teléfono o correo (cada palabra debe coincidir).</param>
    /// <param name="departamentoId">Filtra por departamento.</param>
    /// <param name="ciudadId">Filtra por ciudad.</param>
    /// <param name="activo">Estado exacto a buscar; <c>null</c> = el valor por defecto descrito arriba.</param>
    /// <param name="incluirInactivos">Si es <c>true</c> y no se indica <paramref name="activo"/>, devuelve activos e inactivos.</param>
    /// <param name="pagina">Número de página (desde 1).</param>
    /// <param name="tamano">Elementos por página.</param>
    /// <param name="ct">Token de cancelación.</param>
    public async Task<PaginaDto<ClienteDto>> EjecutarAsync(string? q, int? departamentoId, int? ciudadId,
        bool? activo, bool? incluirInactivos, int? pagina, int? tamano, CancellationToken ct)
    {
        activo ??= incluirInactivos == true ? null : true;
        var (p, t) = Paginacion.Normalizar(pagina, tamano);
        var (elementos, total) = await _clientes.BuscarAsync(q?.Trim(), departamentoId, ciudadId, activo, p, t, ct);
        return new PaginaDto<ClienteDto>(elementos.Select(c => c.ADto()).ToList(), total, p, t);
    }
}
