using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun;

namespace GestionPedidos.Ui.Tienda;

/// <summary>Estado y lógica de la pantalla "Hacer pedido": datos de entrega, fecha, comentarios y confirmación.</summary>
public sealed class PedidoViewModel : ViewModelBase
{
    private readonly PedidosClienteService _pedidos;
    private readonly CarritoService _carrito;
    private readonly TimeProvider _reloj;

    /// <summary>Crea el ViewModel.</summary>
    public PedidoViewModel(PedidosClienteService pedidos, CarritoService carrito, TimeProvider reloj)
    {
        _pedidos = pedidos; _carrito = carrito; _reloj = reloj;
        FechaEntrega = Hoy.AddDays(3);
    }

    /// <summary>Hoy en El Salvador (UTC−6).</summary>
    public DateOnly Hoy => DateOnly.FromDateTime(_reloj.GetUtcNow().UtcDateTime.AddHours(-6));
    /// <summary>Primer día que se puede elegir.</summary>
    public DateOnly Minima => Hoy.AddDays(1);
    /// <summary>Último día que se puede elegir.</summary>
    public DateOnly Maxima => Hoy.AddDays(60);

    /// <summary>Datos de entrega del cliente.</summary>
    public DatosEntregaDto? Entrega { get; private set; }
    /// <summary>Fecha de entrega deseada.</summary>
    public DateOnly? FechaEntrega { get; set; }
    /// <summary>Comentarios opcionales.</summary>
    public string Comentarios { get; set; } = "";
    /// <summary>Error de la fecha, o <c>null</c>.</summary>
    public string? ErrorFecha { get; private set; }
    /// <summary>Error de los comentarios, o <c>null</c>.</summary>
    public string? ErrorComentarios { get; private set; }

    /// <summary>Carga los datos de entrega.</summary>
    public Task IniciarAsync() => EjecutarAsync(async ct => Entrega = await _pedidos.EntregaAsync(ct));

    /// <summary>Valida y registra el pedido; si sale bien vacía el carrito y devuelve el pedido, si no deja el motivo en <c>Error</c>.</summary>
    public async Task<PedidoClienteDto?> ConfirmarAsync()
    {
        ErrorFecha = FechaEntrega is null ? "Elija la fecha de entrega."
            : FechaEntrega < Minima ? "La fecha de entrega debe ser posterior a hoy."
            : FechaEntrega > Maxima ? "Elija una fecha dentro de los próximos 60 días." : null;
        ErrorComentarios = Comentarios.Trim().Length > 500 ? "Los comentarios no pueden superar 500 caracteres." : null;
        if (_carrito.Lineas.Count == 0) Error = "Su carrito está vacío.";
        if (ErrorFecha is not null || ErrorComentarios is not null || _carrito.Lineas.Count == 0) { OnCambiado(); return null; }

        var solicitud = new CrearPedidoClienteSolicitud(_carrito.Lineas.Select(l => new LineaPedidoSolicitud(l.ProductoId, l.Cantidad)).ToList(),
            FechaEntrega!.Value, string.IsNullOrWhiteSpace(Comentarios) ? null : Comentarios.Trim());
        PedidoClienteDto? pedido = null;
        var ok = await EjecutarAsync(async ct => pedido = await _pedidos.CrearAsync(solicitud, ct), cancelarAnterior: false);
        if (!ok || pedido is null) return null;

        await _carrito.VaciarAsync();   // el pedido ya está hecho: el carrito queda vacío
        return pedido;
    }
}
