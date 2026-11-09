using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Pedidos;
using GestionPedidos.Domain.Productos;

namespace GestionPedidos.Application.Pedidos;

/// <summary>Caso de uso: registro administrativo de un pedido con una o más líneas ([Pedidos.md §4]).</summary>
public sealed class RegistrarPedido
{
    private readonly IPedidoRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly EventosAplicacion _eventos;
    private readonly TimeProvider _reloj;

    /// <summary>Crea el caso de uso.</summary>
    public RegistrarPedido(IPedidoRepository repo, IUnitOfWork uow, EventosAplicacion eventos, TimeProvider reloj)
    {
        _repo = repo;
        _uow = uow;
        _eventos = eventos;
        _reloj = reloj;
    }

    /// <summary>Registra el pedido en una sola transacción validando cliente, existencias y reglas.</summary>
    /// <param name="s">Datos del pedido y sus líneas.</param>
    /// <param name="usuarioId">Identificador del usuario que realiza la operación.</param>
    /// <param name="ct">Token de cancelación.</param>
    public async Task<Resultado<PedidoDetalleDto>> EjecutarAsync(CrearPedidoSolicitud s, int usuarioId, CancellationToken ct)
    {
        static Resultado<PedidoDetalleDto> Valida(string m) =>
            Resultado<PedidoDetalleDto>.Falla(TipoError.Validacion, CodigosError.Validacion, m);

        if (s.Lineas is not { Count: > 0 })
            return Valida("El pedido debe tener al menos una línea de producto.");

        if (s.Lineas.Any(l => l.Cantidad <= 0))
            return Valida("Todas las cantidades deben ser mayores que cero.");

        if (s.Comentarios?.Trim().Length > 500)
            return Valida("Los comentarios no pueden superar 500 caracteres.");

        var ahoraUtc = _reloj.GetUtcNow();
        var hoy = DateOnly.FromDateTime(ahoraUtc.UtcDateTime.AddHours(-6)); // Hora El Salvador
        if (s.FechaEntrega < hoy)
            return Valida("La fecha de entrega no puede ser anterior a hoy.");

        var cliente = await _repo.ObtenerClienteAsync(s.ClienteId, ct);
        if (cliente is null)
            return Valida("El cliente seleccionado no existe.");

        if (!cliente.Activo)
            return Resultado<PedidoDetalleDto>.Falla(TipoError.Regla, CodigosError.Regla,
                "No se pueden registrar pedidos para un cliente inactivo.");

        // Agrupar líneas por producto si vinieran repetidas
        var lineasAgrupadas = s.Lineas
            .GroupBy(l => l.ProductoId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Cantidad));

        var productos = (await _repo.ObtenerProductosAsync(lineasAgrupadas.Keys.ToList(), ct))
            .ToDictionary(p => p.Id);

        var faltantes = lineasAgrupadas.Keys.Where(id => !productos.ContainsKey(id)).ToList();
        if (faltantes.Count > 0)
            return Valida("Uno o más productos seleccionados no existen.");

        var problemas = new List<string>();
        foreach (var (id, cantidad) in lineasAgrupadas)
        {
            var p = productos[id];
            if (!p.Activo)
                problemas.Add($"El producto '{p.Nombre}' está inactivo.");
            else if (cantidad > p.CantidadStock)
                problemas.Add(p.CantidadStock == 0
                    ? $"{p.Nombre}: agotado."
                    : $"{p.Nombre}: solo quedan {p.CantidadStock} unidades disponibles.");
        }

        if (problemas.Count > 0)
            return Resultado<PedidoDetalleDto>.Falla(TipoError.Conflicto, CodigosError.StockInsuficiente, string.Join(" ", problemas));

        var pedido = Pedido.Crear(cliente.Id, usuarioId, ahoraUtc, s.FechaEntrega, s.Comentarios);
        pedido.Cliente = cliente;

        foreach (var (id, cantidad) in lineasAgrupadas)
        {
            var p = productos[id];
            pedido.AgregarLinea(p, cantidad);
        }

        _repo.Agregar(pedido);

        try
        {
            await _uow.GuardarAsync(ct);
        }
        catch (ConcurrenciaException)
        {
            return Resultado<PedidoDetalleDto>.Falla(TipoError.Conflicto, CodigosError.Conflicto,
                "Otro usuario modificó el inventario al mismo tiempo. Por favor verifique las existencias e intente de nuevo.");
        }

        _eventos.OnEntidadCambiada(new EntidadCambiadaServidorEventArgs
        {
            Entidad = "Pedido",
            EntidadId = pedido.Id,
            Tipo = TipoCambioServidor.Registrada,
            UsuarioId = usuarioId,
            Detalle = $"Pedido #{pedido.Id} registrado por ${pedido.Total:0.00} ({cliente.Nombres} {cliente.Apellidos})"
        });

        return Resultado<PedidoDetalleDto>.Ok(pedido.ADetalleDto());
    }
}
