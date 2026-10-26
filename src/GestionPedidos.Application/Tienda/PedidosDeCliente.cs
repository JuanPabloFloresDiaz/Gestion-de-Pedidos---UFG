using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Clientes;
using GestionPedidos.Domain.Pedidos;
using GestionPedidos.Domain.Productos;

namespace GestionPedidos.Application.Tienda;

/// <summary>Puerto de los pedidos que hacen los clientes desde la tienda ([PortalWeb.md §3]).</summary>
public interface IPedidosClienteRepository
{
    /// <summary>Cliente enlazado a la cuenta (con su ciudad y departamento), o <c>null</c>.</summary>
    Task<Cliente?> ObtenerClienteDeUsuarioAsync(int usuarioId, CancellationToken ct);
    /// <summary>Productos que el público puede comprar (activos y con clasificación activa), con seguimiento de cambios.</summary>
    Task<IReadOnlyList<Producto>> ObtenerProductosVendiblesAsync(IReadOnlyCollection<int> ids, CancellationToken ct);
    /// <summary>Marca el pedido como nuevo.</summary>
    void Agregar(Pedido pedido);
    /// <summary>Pedidos del cliente, del más reciente al más antiguo.</summary>
    Task<(IReadOnlyList<Pedido> Elementos, int Total)> ListarAsync(int clienteId, int pagina, int tamano, CancellationToken ct);
    /// <summary>Un pedido del cliente con sus líneas y productos, con seguimiento de cambios (<c>null</c> si no es suyo).</summary>
    Task<Pedido?> ObtenerAsync(int clienteId, int pedidoId, CancellationToken ct);
}

/// <summary>Conversión de pedidos a DTO para el cliente.</summary>
internal static class PedidoClienteMapeo
{
    public static PedidoClienteDto ADto(this Pedido p) => new(p.Id, p.FechaPedido, p.FechaEntrega, p.Estado, p.Total, p.Comentarios,
        p.Activo && p.Estado == EstadoPedido.Pendiente,
        p.Detalles.OrderBy(d => d.Producto?.Nombre).Select(d => new LineaPedidoClienteDto(d.ProductoId, d.Producto?.Nombre ?? "", d.Cantidad,
            d.PrecioUnitario, d.Cantidad * d.PrecioUnitario)).ToList());
}

/// <summary>Caso de uso: datos de entrega que se mostrarán al hacer el pedido.</summary>
public sealed class PrepararPedidoDeCliente
{
    private readonly IPedidosClienteRepository _repo;

    /// <summary>Crea el caso de uso.</summary>
    public PrepararPedidoDeCliente(IPedidosClienteRepository repo) => _repo = repo;

    /// <summary>Devuelve los datos de entrega del cliente de la cuenta.</summary>
    public async Task<Resultado<DatosEntregaDto>> EjecutarAsync(int usuarioId, CancellationToken ct)
    {
        var c = await _repo.ObtenerClienteDeUsuarioAsync(usuarioId, ct);
        return c is null
            ? Resultado<DatosEntregaDto>.Falla(TipoError.Regla, CodigosError.Regla, "Su cuenta no tiene un cliente asociado.")
            : Resultado<DatosEntregaDto>.Ok(new DatosEntregaDto($"{c.Nombres} {c.Apellidos}", c.Telefono, c.Direccion,
                c.Ciudad?.Nombre ?? "", c.Ciudad?.Departamento?.Nombre ?? ""));
    }
}

/// <summary>
/// Caso de uso: un cliente hace un pedido desde la tienda. En UNA transacción valida, copia los precios vigentes, descuenta el
/// stock y crea el pedido <c>Pendiente</c>. Si dos clientes compran la última unidad a la vez, la concurrencia optimista
/// (<c>RowVersion</c> del producto) deja pasar a uno y al otro le responde 409.
/// </summary>
/// <remarks>
/// Hoy las reglas viven aquí; cuando el bloque D (Pedidos) cree el dominio de <c>Pedido</c>, deben moverse a él y este caso
/// de uso pasará a usarlo ([Tareas.md], D-01).
/// </remarks>
public sealed class RealizarPedidoDeCliente
{
    /// <summary>Unidades máximas de un mismo producto por pedido.</summary>
    public const int MaximoPorProducto = 10;
    /// <summary>Días máximos hacia el futuro para la fecha de entrega.</summary>
    public const int MaximoDiasEntrega = 60;

    private readonly IPedidosClienteRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly EventosAplicacion _eventos;
    private readonly TimeProvider _reloj;

    /// <summary>Crea el caso de uso.</summary>
    public RealizarPedidoDeCliente(IPedidosClienteRepository repo, IUnitOfWork uow, EventosAplicacion eventos, TimeProvider reloj)
    {
        _repo = repo; _uow = uow; _eventos = eventos; _reloj = reloj;
    }

    /// <summary>Fecha de hoy en El Salvador (UTC−6, sin horario de verano).</summary>
    public static DateOnly Hoy(TimeProvider reloj) => DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime.AddHours(-6));

    /// <summary>Registra el pedido del cliente de la cuenta.</summary>
    public async Task<Resultado<PedidoClienteDto>> EjecutarAsync(int usuarioId, CrearPedidoClienteSolicitud s, CancellationToken ct)
    {
        static Resultado<PedidoClienteDto> Valida(string m) => Resultado<PedidoClienteDto>.Falla(TipoError.Validacion, CodigosError.Validacion, m);

        if (s.Lineas is not { Count: > 0 }) return Valida("El pedido debe tener al menos un producto.");
        if (s.Lineas.Any(l => l.Cantidad <= 0)) return Valida("Las cantidades deben ser mayores que cero.");
        var hoy = Hoy(_reloj);
        if (s.FechaEntrega <= hoy) return Valida("La fecha de entrega debe ser posterior a hoy.");
        if (s.FechaEntrega > hoy.AddDays(MaximoDiasEntrega)) return Valida($"La fecha de entrega no puede superar los {MaximoDiasEntrega} días.");
        if (s.Comentarios?.Trim().Length > 500) return Valida("Los comentarios no pueden superar 500 caracteres.");

        var cliente = await _repo.ObtenerClienteDeUsuarioAsync(usuarioId, ct);
        if (cliente is null)
            return Resultado<PedidoClienteDto>.Falla(TipoError.Regla, CodigosError.Regla, "Su cuenta no tiene un cliente asociado.");
        if (!cliente.Activo)
            return Resultado<PedidoClienteDto>.Falla(TipoError.Regla, CodigosError.Regla, "Su cuenta de cliente está inactiva: comuníquese con la tienda.");

        // Un mismo producto repetido se suma (un producto aparece una sola vez por pedido)
        var pedidas = s.Lineas.GroupBy(l => l.ProductoId).ToDictionary(g => g.Key, g => g.Sum(l => l.Cantidad));
        var productos = (await _repo.ObtenerProductosVendiblesAsync(pedidas.Keys.ToList(), ct)).ToDictionary(p => p.Id);

        var faltantes = pedidas.Keys.Where(id => !productos.ContainsKey(id)).ToList();
        if (faltantes.Count > 0)
            return Resultado<PedidoClienteDto>.Falla(TipoError.Regla, CodigosError.Regla,
                "Uno o más productos del carrito ya no están disponibles en la tienda. Revise su carrito.");

        var problemas = new List<string>();
        foreach (var (id, cantidad) in pedidas)
        {
            var p = productos[id];
            if (cantidad > MaximoPorProducto) problemas.Add($"{p.Nombre}: máximo {MaximoPorProducto} unidades por pedido.");
            else if (cantidad > p.CantidadStock)
                problemas.Add(p.CantidadStock == 0 ? $"{p.Nombre}: agotado." : $"{p.Nombre}: solo quedan {p.CantidadStock}.");
        }
        if (problemas.Count > 0)
            return Resultado<PedidoClienteDto>.Falla(TipoError.Conflicto, CodigosError.StockInsuficiente, string.Join(" ", problemas));

        var pedido = new Pedido
        {
            ClienteId = cliente.Id, UsuarioId = usuarioId, FechaPedido = _reloj.GetUtcNow(), FechaEntrega = s.FechaEntrega,
            Comentarios = string.IsNullOrWhiteSpace(s.Comentarios) ? null : s.Comentarios.Trim(), Estado = EstadoPedido.Pendiente,
        };
        foreach (var (id, cantidad) in pedidas)
        {
            var p = productos[id];
            p.DescontarStock(cantidad);   // el precio es el vigente: nunca el que mande el cliente
            pedido.Detalles.Add(new DetallePedido { Producto = p, ProductoId = id, Cantidad = cantidad, PrecioUnitario = p.PrecioVenta });
        }
        pedido.Total = pedido.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario);
        _repo.Agregar(pedido);

        try { await _uow.GuardarAsync(ct); }
        catch (ConcurrenciaException)
        {
            // Otra compra cambió el stock en el mismo instante: nada se guardó y se pide reintentar
            return Resultado<PedidoClienteDto>.Falla(TipoError.Conflicto, CodigosError.Conflicto,
                "Otra compra modificó el stock mientras hacía su pedido. Revise su carrito e inténtelo de nuevo.");
        }

        _eventos.OnEntidadCambiada(new EntidadCambiadaServidorEventArgs
        {
            Entidad = "Pedido", EntidadId = pedido.Id, Tipo = TipoCambioServidor.Registrada, UsuarioId = usuarioId,
            Detalle = $"Pedido #{pedido.Id} registrado desde la tienda por ${pedido.Total:0.00} ({cliente.Nombres} {cliente.Apellidos})",
        });
        return Resultado<PedidoClienteDto>.Ok(pedido.ADto());
    }
}

/// <summary>Caso de uso: pedidos del cliente de la cuenta (solo los suyos).</summary>
public sealed class ListarPedidosDeCliente
{
    private readonly IPedidosClienteRepository _repo;

    /// <summary>Crea el caso de uso.</summary>
    public ListarPedidosDeCliente(IPedidosClienteRepository repo) => _repo = repo;

    /// <summary>Una página de pedidos, del más reciente al más antiguo.</summary>
    public async Task<Resultado<PaginaDto<PedidoClienteDto>>> EjecutarAsync(int usuarioId, int? pagina, int? tamano, CancellationToken ct)
    {
        var cliente = await _repo.ObtenerClienteDeUsuarioAsync(usuarioId, ct);
        if (cliente is null)
            return Resultado<PaginaDto<PedidoClienteDto>>.Falla(TipoError.Regla, CodigosError.Regla, "Su cuenta no tiene un cliente asociado.");
        var (p, t) = Paginacion.Normalizar(pagina, tamano ?? 10);
        var (elementos, total) = await _repo.ListarAsync(cliente.Id, p, t, ct);
        return Resultado<PaginaDto<PedidoClienteDto>>.Ok(new PaginaDto<PedidoClienteDto>(elementos.Select(x => x.ADto()).ToList(), total, p, t));
    }

    /// <summary>Un pedido del cliente; si es de otra persona, responde como si no existiera (404).</summary>
    public async Task<Resultado<PedidoClienteDto>> ObtenerAsync(int usuarioId, int pedidoId, CancellationToken ct)
    {
        var cliente = await _repo.ObtenerClienteDeUsuarioAsync(usuarioId, ct);
        var pedido = cliente is null ? null : await _repo.ObtenerAsync(cliente.Id, pedidoId, ct);
        return pedido is null
            ? Resultado<PedidoClienteDto>.Falla(TipoError.NoEncontrado, CodigosError.NoEncontrado, "El pedido no existe.")
            : Resultado<PedidoClienteDto>.Ok(pedido.ADto());
    }
}

/// <summary>Caso de uso: el cliente cancela un pedido suyo que sigue <c>Pendiente</c>; el stock se repone.</summary>
public sealed class CancelarPedidoDeCliente
{
    private readonly IPedidosClienteRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly EventosAplicacion _eventos;

    /// <summary>Crea el caso de uso.</summary>
    public CancelarPedidoDeCliente(IPedidosClienteRepository repo, IUnitOfWork uow, EventosAplicacion eventos)
    {
        _repo = repo; _uow = uow; _eventos = eventos;
    }

    /// <summary>Cancela el pedido y repone las existencias en la misma transacción.</summary>
    public async Task<Resultado<PedidoClienteDto>> EjecutarAsync(int usuarioId, int pedidoId, CancellationToken ct)
    {
        var cliente = await _repo.ObtenerClienteDeUsuarioAsync(usuarioId, ct);
        var pedido = cliente is null ? null : await _repo.ObtenerAsync(cliente.Id, pedidoId, ct);
        if (pedido is null)
            return Resultado<PedidoClienteDto>.Falla(TipoError.NoEncontrado, CodigosError.NoEncontrado, "El pedido no existe.");
        if (!pedido.Activo || pedido.Estado != EstadoPedido.Pendiente)
            return Resultado<PedidoClienteDto>.Falla(TipoError.Regla, CodigosError.Regla,
                $"Solo se pueden cancelar los pedidos pendientes: este está {pedido.Estado.ToLowerInvariant()}.");

        foreach (var d in pedido.Detalles) d.Producto!.ReponerStock(d.Cantidad);
        pedido.Estado = EstadoPedido.Cancelado;

        try { await _uow.GuardarAsync(ct); }
        catch (ConcurrenciaException)
        {
            return Resultado<PedidoClienteDto>.Falla(TipoError.Conflicto, CodigosError.Conflicto,
                "El pedido cambió mientras lo cancelaba. Actualice la página e inténtelo de nuevo.");
        }

        _eventos.OnEntidadCambiada(new EntidadCambiadaServidorEventArgs
        {
            Entidad = "Pedido", EntidadId = pedido.Id, Tipo = TipoCambioServidor.EstadoCambiado, UsuarioId = usuarioId,
            Detalle = $"Pedido #{pedido.Id} cancelado por el cliente; existencias repuestas",
        });
        return Resultado<PedidoClienteDto>.Ok(pedido.ADto());
    }
}
