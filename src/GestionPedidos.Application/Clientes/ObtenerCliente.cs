using GestionPedidos.Application.Common;
using GestionPedidos.Contracts;

namespace GestionPedidos.Application.Clientes;

/// <summary>Caso de uso: obtener el detalle de un cliente por identificador.</summary>
public sealed class ObtenerCliente
{
    private readonly IClienteRepository _clientes;

    /// <summary>Crea el caso de uso.</summary>
    public ObtenerCliente(IClienteRepository clientes) => _clientes = clientes;

    /// <summary>Devuelve el cliente o falla con no encontrado.</summary>
    public async Task<Resultado<ClienteDto>> EjecutarAsync(int id, CancellationToken ct)
    {
        var c = await _clientes.ObtenerPorIdAsync(id, ct);
        return c is null
            ? Resultado<ClienteDto>.Falla(TipoError.NoEncontrado, CodigosError.NoEncontrado, "El cliente no existe.")
            : Resultado<ClienteDto>.Ok(c.ADto());
    }
}
