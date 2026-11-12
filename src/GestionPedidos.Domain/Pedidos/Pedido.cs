using GestionPedidos.Domain.Clientes;
using GestionPedidos.Domain.Productos;
using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Domain.Pedidos;

/// <summary>Estados de negocio de un pedido ([Pedidos.md §3]).</summary>
public static class EstadoPedido
{
    /// <summary>Recién registrado.</summary>
    public const string Pendiente = "Pendiente";
    /// <summary>Entregado (final).</summary>
    public const string Entregado = "Entregado";
    /// <summary>Cancelado (final); repone el stock.</summary>
    public const string Cancelado = "Cancelado";
}

/// <summary>
/// Cabecera de un pedido y raíz del agregado (agrega <see cref="DetallePedido"/>).
/// Controla la coherencia del stock, transiciones de estado y cálculo del total ([Pedidos.md]).
/// </summary>
public class Pedido : IAuditable
{
    /// <summary>Identificador (número de pedido).</summary>
    public int Id { get; set; }
    /// <summary>Dueño del pedido.</summary>
    public int ClienteId { get; set; }
    /// <summary>Navegación al cliente.</summary>
    public Cliente? Cliente { get; set; }
    /// <summary>Quién hizo el pedido (operador, administrador o cliente).</summary>
    public int UsuarioId { get; set; }
    /// <summary>Navegación al usuario.</summary>
    public Usuario? Usuario { get; set; }
    /// <summary>Fecha del pedido (UTC), asignada por el sistema.</summary>
    public DateTimeOffset FechaPedido { get; set; }
    /// <summary>Fecha de entrega prevista.</summary>
    public DateOnly FechaEntrega { get; set; }
    /// <summary>Comentarios opcionales (≤ 500 caracteres).</summary>
    public string? Comentarios { get; set; }
    /// <summary>Estado de negocio: Pendiente, Entregado o Cancelado.</summary>
    public string Estado { get; set; } = EstadoPedido.Pendiente;
    /// <summary>Total del pedido, calculado por la suma de subtotales de las líneas.</summary>
    public decimal Total { get; set; }
    /// <summary>Baja lógica: <c>false</c> = inactivo.</summary>
    public bool Activo { get; set; } = true;
    /// <summary>Cuándo se dio de baja (eliminado suave); <c>null</c> mientras esté activo.</summary>
    public DateTimeOffset? EliminadoEn { get; set; }
    /// <summary>Creación (UTC).</summary>
    public DateTimeOffset CreadoEn { get; set; }
    /// <summary>Última modificación (UTC).</summary>
    public DateTimeOffset ActualizadoEn { get; set; }
    /// <summary>Concurrencia optimista.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    /// <summary>Líneas del pedido.</summary>
    public List<DetallePedido> Detalles { get; set; } = new();

    /// <summary>Crea una nueva cabecera de pedido con estado <see cref="EstadoPedido.Pendiente"/>.</summary>
    /// <param name="clienteId">Identificador del cliente dueño del pedido.</param>
    /// <param name="usuarioId">Identificador del usuario que registra el pedido.</param>
    /// <param name="fechaPedido">Fecha de creación (UTC).</param>
    /// <param name="fechaEntrega">Fecha de entrega prevista (no anterior a la fecha del pedido).</param>
    /// <param name="comentarios">Comentarios u observaciones opcionales (≤ 500 caracteres).</param>
    /// <exception cref="ArgumentOutOfRangeException">El cliente o el usuario no son válidos.</exception>
    /// <exception cref="ArgumentException">La fecha de entrega es anterior a la del pedido o comentarios muy largos.</exception>
    public static Pedido Crear(int clienteId, int usuarioId, DateTimeOffset fechaPedido, DateOnly fechaEntrega, string? comentarios)
    {
        if (clienteId <= 0) throw new ArgumentOutOfRangeException(nameof(clienteId), "El cliente es obligatorio.");
        if (usuarioId <= 0) throw new ArgumentOutOfRangeException(nameof(usuarioId), "El usuario es obligatorio.");

        // La fecha del pedido es la de El Salvador (UTC−6): después de las 6 p. m. el día UTC ya cambió, pero "hoy" no.
        var fechaPedidoDate = DateOnly.FromDateTime(fechaPedido.UtcDateTime.AddHours(-6));
        if (fechaEntrega < fechaPedidoDate)
            throw new ArgumentException("La fecha de entrega no puede ser anterior a la fecha del pedido.", nameof(fechaEntrega));

        if (comentarios?.Trim().Length > 500)
            throw new ArgumentException("Los comentarios no pueden superar 500 caracteres.", nameof(comentarios));

        return new Pedido
        {
            ClienteId = clienteId,
            UsuarioId = usuarioId,
            FechaPedido = fechaPedido,
            FechaEntrega = fechaEntrega,
            Comentarios = string.IsNullOrWhiteSpace(comentarios) ? null : comentarios.Trim(),
            Estado = EstadoPedido.Pendiente,
            Activo = true
        };
    }

    /// <summary>Agrega una línea al pedido descontando el stock del producto.</summary>
    /// <param name="producto">Producto que se vende (debe estar activo).</param>
    /// <param name="cantidad">Cantidad vendida (mayor que cero).</param>
    /// <exception cref="InvalidOperationException">El pedido o producto no está activo, ya contiene el producto o no hay stock.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="producto"/> es <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cantidad"/> no es positiva.</exception>
    public void AgregarLinea(Producto producto, int cantidad)
    {
        AsegurarEditable();
        if (producto is null) throw new ArgumentNullException(nameof(producto));
        if (!producto.Activo) throw new InvalidOperationException($"El producto '{producto.Nombre}' está inactivo.");
        if (cantidad <= 0) throw new ArgumentOutOfRangeException(nameof(cantidad), "La cantidad debe ser mayor que cero.");
        if (Detalles.Any(d => d.ProductoId == producto.Id))
            throw new InvalidOperationException($"El producto '{producto.Nombre}' ya forma parte del pedido.");

        producto.DescontarStock(cantidad);

        var detalle = DetallePedido.Crear(producto, cantidad);
        detalle.Pedido = this;
        Detalles.Add(detalle);
        RecalcularTotal();
    }

    /// <summary>Actualiza la cabecera del pedido (solo en estado Pendiente y activo).</summary>
    /// <param name="fechaEntrega">Nueva fecha de entrega prevista.</param>
    /// <param name="comentarios">Nuevos comentarios (≤ 500 caracteres).</param>
    /// <exception cref="InvalidOperationException">El pedido no es editable.</exception>
    /// <exception cref="ArgumentException">Fecha anterior o comentarios exceden el límite.</exception>
    public void ActualizarCabecera(DateOnly fechaEntrega, string? comentarios)
    {
        AsegurarEditable();

        var fechaPedidoDate = DateOnly.FromDateTime(FechaPedido.UtcDateTime);
        if (fechaEntrega < fechaPedidoDate)
            throw new ArgumentException("La fecha de entrega no puede ser anterior a la fecha del pedido.", nameof(fechaEntrega));

        if (comentarios?.Trim().Length > 500)
            throw new ArgumentException("Los comentarios no pueden superar 500 caracteres.", nameof(comentarios));

        FechaEntrega = fechaEntrega;
        Comentarios = string.IsNullOrWhiteSpace(comentarios) ? null : comentarios.Trim();
    }

    /// <summary>
    /// Actualiza todas las líneas del pedido ajustando el stock por diferencia.
    /// Mantiene los precios de líneas existentes y asigna el precio vigente a las nuevas.
    /// </summary>
    /// <param name="nuevasLineas">Nueva colección de productos y cantidades.</param>
    /// <exception cref="InvalidOperationException">El pedido no es editable o algún producto está inactivo o sin stock.</exception>
    /// <exception cref="ArgumentException">No hay líneas, hay productos repetidos o cantidades inválidas.</exception>
    public void ActualizarLineas(IReadOnlyCollection<(Producto Producto, int Cantidad)> nuevasLineas)
    {
        AsegurarEditable();
        if (nuevasLineas is null || nuevasLineas.Count == 0)
            throw new ArgumentException("El pedido debe tener al menos una línea de producto.", nameof(nuevasLineas));

        var lista = nuevasLineas.ToList();
        if (lista.Any(l => l.Cantidad <= 0))
            throw new ArgumentOutOfRangeException(nameof(nuevasLineas), "Todas las cantidades deben ser mayores que cero.");

        if (lista.Select(l => l.Producto.Id).Distinct().Count() != lista.Count)
            throw new ArgumentException("Un producto no puede aparecer repetido en el pedido.", nameof(nuevasLineas));

        if (lista.Any(l => !l.Producto.Activo))
            throw new InvalidOperationException("Uno o más productos están inactivos.");

        var diccionarioNuevas = lista.ToDictionary(l => l.Producto.Id);
        var actuales = Detalles.ToList();

        // 1. Quitar líneas que ya no están: reponer su stock
        foreach (var detalle in actuales)
        {
            if (!diccionarioNuevas.ContainsKey(detalle.ProductoId))
            {
                if (detalle.Producto is null)
                    throw new InvalidOperationException("No se puede quitar la línea: falta cargar su producto para reponer el stock.");

                detalle.Producto.ReponerStock(detalle.Cantidad);
                Detalles.Remove(detalle);
            }
        }

        // 2. Modificar existentes (ajuste por diferencia manteniendo precio histórico)
        foreach (var detalle in Detalles)
        {
            var (prod, nuevaCantidad) = diccionarioNuevas[detalle.ProductoId];
            if (nuevaCantidad > detalle.Cantidad)
            {
                var faltante = nuevaCantidad - detalle.Cantidad;
                prod.DescontarStock(faltante);
            }
            else if (nuevaCantidad < detalle.Cantidad)
            {
                var sobrante = detalle.Cantidad - nuevaCantidad;
                prod.ReponerStock(sobrante);
            }
            detalle.ActualizarCantidad(nuevaCantidad);
        }

        // 3. Agregar líneas nuevas (copiando precio vigente del producto)
        var idsActuales = Detalles.Select(d => d.ProductoId).ToHashSet();
        foreach (var (prod, cantidad) in lista)
        {
            if (!idsActuales.Contains(prod.Id))
            {
                prod.DescontarStock(cantidad);
                var nuevo = DetallePedido.Crear(prod, cantidad);
                nuevo.Pedido = this;
                Detalles.Add(nuevo);
            }
        }

        RecalcularTotal();
    }

    /// <summary>Marca el pedido como entregado (estado final, el stock no cambia).</summary>
    /// <exception cref="InvalidOperationException">El pedido no está activo o no está pendiente.</exception>
    public void MarcarEntregado()
    {
        if (!Activo || Estado != EstadoPedido.Pendiente)
            throw new InvalidOperationException($"Solo se pueden marcar como entregados los pedidos pendientes (actual: {Estado.ToLowerInvariant()}).");

        Estado = EstadoPedido.Entregado;
    }

    /// <summary>Cancela el pedido y repone el stock de todas las líneas (estado final).</summary>
    /// <exception cref="InvalidOperationException">El pedido no está activo o no está pendiente.</exception>
    public void Cancelar()
    {
        if (!Activo || Estado != EstadoPedido.Pendiente)
            throw new InvalidOperationException($"Solo se pueden cancelar los pedidos pendientes (actual: {Estado.ToLowerInvariant()}).");

        // Sin el producto cargado no se puede reponer su stock: cancelar "en silencio" dejaría el inventario descuadrado.
        if (Detalles.Any(d => d.Producto is null))
            throw new InvalidOperationException("No se puede cancelar el pedido: falta cargar los productos de sus líneas para reponer el stock.");

        foreach (var d in Detalles)
        {
            d.Producto!.ReponerStock(d.Cantidad);
        }

        Estado = EstadoPedido.Cancelado;
    }

    /// <summary>
    /// Aplica baja lógica al pedido. Si está Pendiente, primero se cancela reponiendo el stock;
    /// si ya está en estado final, solo se marca como inactivo. Un pedido inactivo no se reactiva.
    /// </summary>
    /// <param name="ahora">Fecha y hora (UTC) de la baja.</param>
    public void Desactivar(DateTimeOffset ahora)
    {
        if (!Activo) return;

        if (Estado == EstadoPedido.Pendiente)
        {
            Cancelar();
        }

        Activo = false;
        EliminadoEn = ahora;
    }

    /// <summary>Recalcula el total del pedido sumando el subtotal de cada línea.</summary>
    public void RecalcularTotal()
    {
        Total = Detalles.Sum(d => d.Cantidad * d.PrecioUnitario);
    }

    private void AsegurarEditable()
    {
        if (!Activo) throw new InvalidOperationException("El pedido está dado de baja y no se puede modificar.");
        if (Estado != EstadoPedido.Pendiente)
            throw new InvalidOperationException($"Solo se pueden modificar pedidos pendientes: este pedido está {Estado.ToLowerInvariant()}.");
    }
}

/// <summary>Línea de un pedido: producto, cantidad y precio por unidad copiado al crearla.</summary>
public class DetallePedido
{
    /// <summary>Identificador.</summary>
    public int Id { get; set; }
    /// <summary>Pedido al que pertenece.</summary>
    public int PedidoId { get; set; }
    /// <summary>Navegación al pedido.</summary>
    public Pedido? Pedido { get; set; }
    /// <summary>Producto vendido.</summary>
    public int ProductoId { get; set; }
    /// <summary>Navegación al producto.</summary>
    public Producto? Producto { get; set; }
    /// <summary>Cantidad (mayor que cero).</summary>
    public int Cantidad { get; set; }
    /// <summary>Precio por unidad copiado en el momento de crear la línea.</summary>
    public decimal PrecioUnitario { get; set; }
    /// <summary>Subtotal calculado (<c>Cantidad × PrecioUnitario</c>).</summary>
    public decimal Subtotal { get; internal set; }

    /// <summary>Crea una nueva línea de pedido asociada a un producto.</summary>
    /// <param name="producto">Producto vendido.</param>
    /// <param name="cantidad">Cantidad vendida (> 0).</param>
    /// <exception cref="ArgumentNullException"><paramref name="producto"/> es <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cantidad"/> no es positiva.</exception>
    public static DetallePedido Crear(Producto producto, int cantidad)
    {
        if (producto is null) throw new ArgumentNullException(nameof(producto));
        if (cantidad <= 0) throw new ArgumentOutOfRangeException(nameof(cantidad), "La cantidad debe ser mayor que cero.");

        return new DetallePedido
        {
            ProductoId = producto.Id,
            Producto = producto,
            Cantidad = cantidad,
            PrecioUnitario = producto.PrecioVenta,
            Subtotal = cantidad * producto.PrecioVenta
        };
    }

    /// <summary>Actualiza la cantidad de la línea recalculando su subtotal.</summary>
    /// <param name="nuevaCantidad">Nueva cantidad (> 0).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="nuevaCantidad"/> no es positiva.</exception>
    internal void ActualizarCantidad(int nuevaCantidad)
    {
        if (nuevaCantidad <= 0) throw new ArgumentOutOfRangeException(nameof(nuevaCantidad), "La cantidad debe ser mayor que cero.");
        Cantidad = nuevaCantidad;
        Subtotal = nuevaCantidad * PrecioUnitario;
    }
}
