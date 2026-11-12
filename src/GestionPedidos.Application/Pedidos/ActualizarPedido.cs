using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Pedidos;
using GestionPedidos.Domain.Productos;

namespace GestionPedidos.Application.Pedidos;

/// <summary>Caso de uso: edición de un pedido en estado Pendiente ([Pedidos.md §4]).</summary>
public sealed class ActualizarPedido
{
    private readonly IPedidoRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly EventosAplicacion _eventos;
    private readonly TimeProvider _reloj;

    /// <summary>Crea el caso de uso.</summary>
    public ActualizarPedido(IPedidoRepository repo, IUnitOfWork uow, EventosAplicacion eventos, TimeProvider reloj)
    {
        _repo = repo;
        _uow = uow;
        _eventos = eventos;
        _reloj = reloj;
    }

    /// <summary>Actualiza las líneas, fecha de entrega y comentarios ajustando el stock por diferencia.</summary>
    /// <param name="id">Identificador del pedido.</param>
    /// <param name="s">Datos a actualizar.</param>
    /// <param name="usuarioId">Identificador del usuario que realiza la operación.</param>
    /// <param name="ct">Token de cancelación.</param>
    public async Task<Resultado<PedidoDetalleDto>> EjecutarAsync(int id, ActualizarPedidoSolicitud s, int usuarioId, CancellationToken ct)
    {
        static Resultado<PedidoDetalleDto> Valida(string m) =>
            Resultado<PedidoDetalleDto>.Falla(TipoError.Validacion, CodigosError.Validacion, m);

        if (s.Lineas is not { Count: > 0 })
            return Valida("El pedido debe tener al menos una línea de producto.");

        if (s.Lineas.Any(l => l.Cantidad <= 0))
            return Valida("Todas las cantidades deben ser mayores que cero.");

        if (s.Comentarios?.Trim().Length > 500)
            return Valida("Los comentarios no pueden superar 500 caracteres.");

        if (s.RowVersion is not { Length: > 0 })
            return Valida("Falta la versión del pedido. Recargue la pantalla e intente de nuevo.");

        var pedido = await _repo.ObtenerPorIdAsync(id, ct);
        if (pedido is null)
            return Resultado<PedidoDetalleDto>.Falla(TipoError.NoEncontrado, CodigosError.NoEncontrado, "El pedido no existe.");

        if (!pedido.Activo || pedido.Estado != EstadoPedido.Pendiente)
            return Resultado<PedidoDetalleDto>.Falla(TipoError.Regla, CodigosError.Regla,
                $"Solo se pueden modificar pedidos pendientes: este pedido está {pedido.Estado.ToLowerInvariant()}.");

        var hoy = Application.Tienda.RealizarPedidoDeCliente.Hoy(_reloj);
        if (s.FechaEntrega < hoy)
            return Valida("La fecha de entrega no puede ser anterior a hoy.");

        _repo.FijarVersionOriginal(pedido, s.RowVersion);

        // Agrupar líneas por producto
        var lineasAgrupadas = s.Lineas
            .GroupBy(l => l.ProductoId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Cantidad));

        // Obtener productos involucrados (tanto los actuales como los nuevos solicitados)
        var todosIds = pedido.Detalles.Select(d => d.ProductoId).Union(lineasAgrupadas.Keys).Distinct().ToList();
        var productos = (await _repo.ObtenerProductosAsync(todosIds, ct)).ToDictionary(p => p.Id);

        var faltantes = lineasAgrupadas.Keys.Where(pId => !productos.ContainsKey(pId)).ToList();
        if (faltantes.Count > 0)
            return Valida("Uno o más productos seleccionados no existen.");

        // Validar diferencias de stock antes de aplicar
        var detallesActuales = pedido.Detalles.ToDictionary(d => d.ProductoId);
        var problemas = new List<string>();

        foreach (var (pId, nuevaCantidad) in lineasAgrupadas)
        {
            var prod = productos[pId];
            if (!prod.Activo)
            {
                problemas.Add($"El producto '{prod.Nombre}' está inactivo.");
                continue;
            }

            var cantidadActual = detallesActuales.TryGetValue(pId, out var actual) ? actual.Cantidad : 0;
            if (nuevaCantidad > cantidadActual)
            {
                var incremento = nuevaCantidad - cantidadActual;
                if (incremento > prod.CantidadStock)
                {
                    problemas.Add(prod.CantidadStock == 0
                        ? $"{prod.Nombre}: no hay unidades adicionales disponibles (stock agotado)."
                        : $"{prod.Nombre}: se requieren {incremento} adicionales pero solo quedan {prod.CantidadStock}.");
                }
            }
        }

        if (problemas.Count > 0)
            return Resultado<PedidoDetalleDto>.Falla(TipoError.Conflicto, CodigosError.StockInsuficiente, string.Join(" ", problemas));

        try
        {
            pedido.ActualizarCabecera(s.FechaEntrega, s.Comentarios);
            var pares = lineasAgrupadas.Select(kv => (productos[kv.Key], kv.Value)).ToList();
            pedido.ActualizarLineas(pares);

            await _uow.GuardarAsync(ct);
        }
        catch (ConcurrenciaException)
        {
            return Resultado<PedidoDetalleDto>.Falla(TipoError.Conflicto, CodigosError.Conflicto,
                "El pedido o el inventario fueron modificados por otro usuario. Por favor recargue los datos.");
        }

        _eventos.OnEntidadCambiada(new EntidadCambiadaServidorEventArgs
        {
            Entidad = "Pedido",
            EntidadId = pedido.Id,
            Tipo = TipoCambioServidor.Actualizada,
            UsuarioId = usuarioId,
            Detalle = $"Pedido #{pedido.Id} actualizado (total: ${pedido.Total:0.00})"
        });

        return Resultado<PedidoDetalleDto>.Ok(pedido.ADetalleDto());
    }
}
