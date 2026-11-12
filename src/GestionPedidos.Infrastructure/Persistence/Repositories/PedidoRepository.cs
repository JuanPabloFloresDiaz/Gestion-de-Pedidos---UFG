using System.Data;
using GestionPedidos.Application.Pedidos;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Clientes;
using GestionPedidos.Domain.Pedidos;
using GestionPedidos.Domain.Productos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GestionPedidos.Infrastructure.Persistence.Repositories;

/// <summary>Fila devuelta por el procedimiento almacenado <c>usp_Pedidos_Buscar</c>.</summary>
public sealed class FilaBusquedaPedido
{
    /// <summary>Identificador del pedido.</summary>
    public int Id { get; set; }
    /// <summary>Identificador del cliente.</summary>
    public int ClienteId { get; set; }
    /// <summary>Nombre completo del cliente.</summary>
    public string Cliente { get; set; } = "";
    /// <summary>Apellidos del cliente.</summary>
    public string ClienteApellidos { get; set; } = "";
    /// <summary>Identificador del usuario que registró el pedido.</summary>
    public int UsuarioId { get; set; }
    /// <summary>Nombre completo del usuario.</summary>
    public string Usuario { get; set; } = "";
    /// <summary>Fecha del pedido (UTC).</summary>
    public DateTimeOffset FechaPedido { get; set; }
    /// <summary>Fecha de entrega prevista.</summary>
    public DateOnly FechaEntrega { get; set; }
    /// <summary>Estado del pedido.</summary>
    public string Estado { get; set; } = "";
    /// <summary>Monto total.</summary>
    public decimal Total { get; set; }
    /// <summary>Baja lógica.</summary>
    public bool Activo { get; set; }
    /// <summary>Comentarios opcionales.</summary>
    public string? Comentarios { get; set; }
    /// <summary>Cantidad de líneas.</summary>
    public int Lineas { get; set; }
    /// <summary>Total de registros coincidentes con los filtros.</summary>
    public int TotalFilas { get; set; }
}

/// <summary>Implementación de <see cref="IPedidoRepository"/> sobre EF Core y SQL Server.</summary>
public sealed class PedidoRepository : IPedidoRepository
{
    private readonly GestionPedidosDbContext _db;

    /// <summary>Crea el repositorio.</summary>
    public PedidoRepository(GestionPedidosDbContext db) => _db = db;

    /// <inheritdoc/>
    public Task<Pedido?> ObtenerPorIdAsync(int id, CancellationToken ct) =>
        _db.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Usuario)
            .Include(p => p.Detalles)
                .ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    /// <inheritdoc/>
    public Task<Cliente?> ObtenerClienteAsync(int clienteId, CancellationToken ct) =>
        _db.Clientes.FirstOrDefaultAsync(c => c.Id == clienteId, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Producto>> ObtenerProductosAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        await _db.Productos.Where(p => ids.Contains(p.Id)).ToListAsync(ct);

    /// <inheritdoc/>
    public void Agregar(Pedido pedido) => _db.Pedidos.Add(pedido);

    /// <inheritdoc/>
    public void FijarVersionOriginal(Pedido pedido, byte[] rowVersion) =>
        _db.Entry(pedido).Property(p => p.RowVersion).OriginalValue = rowVersion;

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<PedidoResumenDto> Elementos, int Total)> BuscarAsync(FiltroPedidosSolicitud f, CancellationToken ct)
    {
        if (_db.Database.IsSqlServer())
        {
            var pClienteId = new SqlParameter("@ClienteId", SqlDbType.Int) { Value = (object?)f.ClienteId ?? DBNull.Value };
            var pCliente = new SqlParameter("@Cliente", SqlDbType.NVarChar, 200) { Value = (object?)f.Cliente?.Trim() ?? DBNull.Value };
            var pProductoId = new SqlParameter("@ProductoId", SqlDbType.Int) { Value = (object?)f.ProductoId ?? DBNull.Value };
            var pProducto = new SqlParameter("@Producto", SqlDbType.NVarChar, 150) { Value = (object?)f.Producto?.Trim() ?? DBNull.Value };
            var pPrecioMin = new SqlParameter("@PrecioMin", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = (object?)f.PrecioMin ?? DBNull.Value };
            var pPrecioMax = new SqlParameter("@PrecioMax", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = (object?)f.PrecioMax ?? DBNull.Value };
            var pEstado = new SqlParameter("@Estado", SqlDbType.NVarChar, 20) { Value = (object?)f.Estado ?? DBNull.Value };
            var pPedidoDesde = new SqlParameter("@PedidoDesde", SqlDbType.DateTimeOffset) { Value = (object?)f.PedidoDesde ?? DBNull.Value };
            var pPedidoHasta = new SqlParameter("@PedidoHasta", SqlDbType.DateTimeOffset) { Value = (object?)f.PedidoHasta ?? DBNull.Value };
            var pEntregaDesde = new SqlParameter("@EntregaDesde", SqlDbType.Date) { Value = (object?)f.EntregaDesde ?? DBNull.Value };
            var pEntregaHasta = new SqlParameter("@EntregaHasta", SqlDbType.Date) { Value = (object?)f.EntregaHasta ?? DBNull.Value };
            var pUsuarioId = new SqlParameter("@UsuarioId", SqlDbType.Int) { Value = (object?)f.UsuarioId ?? DBNull.Value };
            var pIncluirInactivos = new SqlParameter("@IncluirInactivos", SqlDbType.Bit) { Value = f.IncluirInactivos == true };
            var pPagina = new SqlParameter("@Pagina", SqlDbType.Int) { Value = f.Pagina ?? 1 };
            var pTamanoPagina = new SqlParameter("@TamanoPagina", SqlDbType.Int) { Value = f.TamanoPagina ?? 20 };

            var sql = "EXEC dbo.usp_Pedidos_Buscar @ClienteId, @Cliente, @ProductoId, @Producto, @PrecioMin, @PrecioMax, @Estado, @PedidoDesde, @PedidoHasta, @EntregaDesde, @EntregaHasta, @UsuarioId, @IncluirInactivos, @Pagina, @TamanoPagina";

            var filas = await _db.Database.SqlQueryRaw<FilaBusquedaPedido>(
                sql,
                pClienteId, pCliente, pProductoId, pProducto, pPrecioMin, pPrecioMax,
                pEstado, pPedidoDesde, pPedidoHasta, pEntregaDesde, pEntregaHasta,
                pUsuarioId, pIncluirInactivos, pPagina, pTamanoPagina).ToListAsync(ct);

            var total = filas.FirstOrDefault()?.TotalFilas ?? 0;
            var dtos = filas.Select(x => new PedidoResumenDto(
                x.Id,
                x.ClienteId,
                x.Cliente,
                x.ClienteApellidos,
                x.UsuarioId,
                x.Usuario,
                x.FechaPedido,
                x.FechaEntrega,
                x.Estado,
                x.Total,
                x.Activo,
                x.Comentarios,
                x.Lineas)).ToList();

            return (dtos, total);
        }

        // Respaldo LINQ (para pruebas o proveedores en memoria)
        var consulta = _db.Pedidos.AsNoTracking().Include(p => p.Cliente).Include(p => p.Usuario).Include(p => p.Detalles).AsQueryable();

        if (f.IncluirInactivos != true) consulta = consulta.Where(p => p.Activo);
        if (f.ClienteId.HasValue) consulta = consulta.Where(p => p.ClienteId == f.ClienteId.Value);
        if (!string.IsNullOrWhiteSpace(f.Cliente))
        {
            var txt = f.Cliente.Trim();
            consulta = consulta.Where(p => p.Cliente != null && (p.Cliente.Nombres.Contains(txt) || p.Cliente.Apellidos.Contains(txt)));
        }
        if (f.Estado is not null) consulta = consulta.Where(p => p.Estado == f.Estado);
        if (f.PedidoDesde.HasValue) consulta = consulta.Where(p => p.FechaPedido >= f.PedidoDesde.Value);
        if (f.PedidoHasta.HasValue) consulta = consulta.Where(p => p.FechaPedido <= f.PedidoHasta.Value);
        if (f.EntregaDesde.HasValue) consulta = consulta.Where(p => p.FechaEntrega >= f.EntregaDesde.Value);
        if (f.EntregaHasta.HasValue) consulta = consulta.Where(p => p.FechaEntrega <= f.EntregaHasta.Value);
        if (f.UsuarioId.HasValue) consulta = consulta.Where(p => p.UsuarioId == f.UsuarioId.Value);

        var totalLinq = await consulta.CountAsync(ct);
        var pagina = f.Pagina ?? 1;
        var tamano = f.TamanoPagina ?? 20;

        var elementos = await consulta
            .OrderByDescending(p => p.FechaPedido).ThenByDescending(p => p.Id)
            .Skip((pagina - 1) * tamano).Take(tamano)
            .Select(p => new PedidoResumenDto(
                p.Id,
                p.ClienteId,
                p.Cliente != null ? p.Cliente.Nombres + " " + p.Cliente.Apellidos : "",
                p.Cliente != null ? p.Cliente.Apellidos : "",
                p.UsuarioId,
                p.Usuario != null ? p.Usuario.NombreCompleto : "",
                p.FechaPedido,
                p.FechaEntrega,
                p.Estado,
                p.Total,
                p.Activo,
                p.Comentarios,
                p.Detalles.Count))
            .ToListAsync(ct);

        return (elementos, totalLinq);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ClienteSelectorDto>> ListarClientesSelectorAsync(string? q, CancellationToken ct)
    {
        var consulta = _db.Clientes.AsNoTracking().Where(c => c.Activo);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var txt = q.Trim();
            consulta = consulta.Where(c => c.Nombres.Contains(txt) || c.Apellidos.Contains(txt) || c.Telefono.Contains(txt));
        }

        return await consulta
            .OrderBy(c => c.Nombres).ThenBy(c => c.Apellidos)
            .Take(500)   // tope de seguridad: con filtro por texto nunca se acerca; sin filtro caben todos los clientes de la demostración
            .Select(c => new ClienteSelectorDto(c.Id, $"{c.Nombres} {c.Apellidos}", c.Telefono, c.Direccion))
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProductoSelectorDto>> ListarProductosSelectorAsync(string? q, CancellationToken ct)
    {
        // Solo productos activos y con existencias: un producto agotado no se puede pedir.
        var consulta = _db.Productos.AsNoTracking().Where(p => p.Activo && p.CantidadStock > 0);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var txt = q.Trim();
            consulta = consulta.Where(p => p.Nombre.Contains(txt) || p.Codigo.Contains(txt));
        }

        return await consulta
            .OrderBy(p => p.Nombre)
            .Take(500)   // tope de seguridad (el catálogo de la demostración tiene ~110 productos)
            .Select(p => new ProductoSelectorDto(p.Id, p.Codigo, p.Nombre, p.PrecioVenta, p.CantidadStock))
            .ToListAsync(ct);
    }
}
