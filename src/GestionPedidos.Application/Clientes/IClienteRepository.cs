using GestionPedidos.Domain.Clientes;

namespace GestionPedidos.Application.Clientes;

/// <summary>Puerto de persistencia para el agregado Cliente ([Clientes.md]).</summary>
public interface IClienteRepository
{
    /// <summary>Obtiene un cliente por su identificador (con su ciudad y departamento cargados).</summary>
    Task<Cliente?> ObtenerPorIdAsync(int id, CancellationToken ct);

    /// <summary>Busca clientes con filtros combinados y paginación.</summary>
    Task<(IReadOnlyList<Cliente> Elementos, int Total)> BuscarAsync(string? q, int? departamentoId, int? ciudadId,
        bool? activo, int pagina, int tamano, CancellationToken ct);

    /// <summary>Comprueba si ya existe un cliente con el correo dado (excluyendo opcionalmente un cliente al editar).</summary>
    Task<bool> ExisteCorreoAsync(string correo, int? exceptoId, CancellationToken ct);

    /// <summary>Comprueba si una ciudad existe.</summary>
    Task<bool> ExisteCiudadAsync(int ciudadId, CancellationToken ct);

    /// <summary>Indica si el cliente tiene pedidos en estado Pendiente.</summary>
    Task<bool> TienePedidosPendientesAsync(int clienteId, CancellationToken ct);

    /// <summary>Fija la versión original esperada para control de concurrencia optimista.</summary>
    void FijarVersionOriginal(Cliente cliente, byte[] version);

    /// <summary>Agrega un nuevo cliente para seguimiento de cambios.</summary>
    void Agregar(Cliente cliente);
}
