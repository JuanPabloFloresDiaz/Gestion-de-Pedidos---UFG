using GestionPedidos.Application.Tienda;
using GestionPedidos.Domain.Clientes;
using GestionPedidos.Domain.Pedidos;
using GestionPedidos.Domain.Productos;
using Microsoft.EntityFrameworkCore;

namespace GestionPedidos.Infrastructure.Persistence.Repositories;

/// <summary>Pedidos de los clientes de la tienda sobre EF Core.</summary>
public sealed class PedidosClienteRepository : IPedidosClienteRepository
{
    private readonly GestionPedidosDbContext _db;

    /// <summary>Crea el repositorio.</summary>
    public PedidosClienteRepository(GestionPedidosDbContext db) => _db = db;

    /// <inheritdoc/>
    public Task<Cliente?> ObtenerClienteDeUsuarioAsync(int usuarioId, CancellationToken ct) =>
        _db.Clientes.Include(c => c.Ciudad!).ThenInclude(c => c.Departamento).FirstOrDefaultAsync(c => c.UsuarioId == usuarioId, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Producto>> ObtenerProductosVendiblesAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        await _db.Productos.Where(p => ids.Contains(p.Id) && p.Activo
            && (p.SubcategoriaId == null || (p.Subcategoria!.Activo && p.Subcategoria.Categoria!.Activo))).ToListAsync(ct);

    /// <inheritdoc/>
    public void Agregar(Pedido pedido) => _db.Pedidos.Add(pedido);

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<Pedido> Elementos, int Total)> ListarAsync(int clienteId, int pagina, int tamano, CancellationToken ct)
    {
        var consulta = _db.Pedidos.AsNoTracking().Where(p => p.ClienteId == clienteId && p.Activo);
        var total = await consulta.CountAsync(ct);
        var elementos = await consulta.Include(p => p.Detalles).ThenInclude(d => d.Producto)
            .OrderByDescending(p => p.FechaPedido).ThenByDescending(p => p.Id)
            .Skip((pagina - 1) * tamano).Take(tamano).ToListAsync(ct);
        return (elementos, total);
    }

    /// <inheritdoc/>
    public Task<Pedido?> ObtenerAsync(int clienteId, int pedidoId, CancellationToken ct) =>
        _db.Pedidos.Include(p => p.Detalles).ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(p => p.Id == pedidoId && p.ClienteId == clienteId && p.Activo, ct);
}
