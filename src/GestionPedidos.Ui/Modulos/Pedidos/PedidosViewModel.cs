using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun;
using GestionPedidos.Ui.Comun.Eventos;

namespace GestionPedidos.Ui.Modulos.Pedidos;

/// <summary>Acción sobre un pedido que requiere confirmación del usuario.</summary>
public enum AccionPedido
{
    /// <summary>Ninguna acción pendiente.</summary>
    Ninguna,
    /// <summary>Marcar pedido como Entregado.</summary>
    MarcarEntregado,
    /// <summary>Cancelar pedido (repone existencias).</summary>
    Cancelar,
    /// <summary>Dar de baja lógica al pedido.</summary>
    DarDeBaja
}

/// <summary>Línea editable de un pedido para el formulario maestro–detalle.</summary>
public sealed class LineaPedidoEditable
{
    /// <summary>Identificador del producto.</summary>
    public int ProductoId { get; set; }
    /// <summary>Código del producto.</summary>
    public string Codigo { get; set; } = "";
    /// <summary>Nombre del producto.</summary>
    public string Producto { get; set; } = "";
    /// <summary>Precio unitario aplicado a la línea.</summary>
    public decimal PrecioUnitario { get; set; }
    /// <summary>Cantidad de unidades.</summary>
    public int Cantidad { get; set; }
    /// <summary>Existencias disponibles en inventario.</summary>
    public int StockDisponible { get; set; }
    /// <summary>Subtotal calculado de la línea (<c>Cantidad × PrecioUnitario</c>).</summary>
    public decimal Subtotal => Cantidad * PrecioUnitario;
}

/// <summary>
/// Estado y lógica de la pantalla de pedidos ([Pedidos.md §7]):
/// maestro–detalle, filtros combinables, tabla de líneas interactiva, total en vivo y cambios de estado.
/// </summary>
public sealed class PedidosViewModel : ViewModelBase
{
    private readonly PedidosService _pedidos;

    /// <summary>Crea el ViewModel de pedidos y se suscribe a los cambios para refrescar los datos.</summary>
    public PedidosViewModel(PedidosService pedidos)
    {
        _pedidos = pedidos;
        _pedidos.EntidadCambiada += Pedidos_EntidadCambiada;
        _pedidos.StockInsuficiente += Pedidos_StockInsuficiente;
    }

    // --- Filtros de búsqueda (Busquedas.md) ---
    /// <summary>Búsqueda por nombre o apellido de cliente.</summary>
    public string TextoCliente { get; set; } = "";
    /// <summary>Búsqueda por nombre de producto.</summary>
    public string TextoProducto { get; set; } = "";
    /// <summary>Filtro por estado de negocio (null = todos).</summary>
    public string? FiltroEstado { get; set; }
    /// <summary>Rango de fecha de entrega: desde.</summary>
    public DateOnly? FiltroEntregaDesde { get; set; }
    /// <summary>Rango de fecha de entrega: hasta.</summary>
    public DateOnly? FiltroEntregaHasta { get; set; }
    /// <summary>Precio unitario mínimo de líneas.</summary>
    public decimal? FiltroPrecioMin { get; set; }
    /// <summary>Precio unitario máximo de líneas.</summary>
    public decimal? FiltroPrecioMax { get; set; }
    /// <summary>Incluir pedidos dados de baja lógica.</summary>
    public bool FiltroIncluirInactivos { get; set; }

    /// <summary>Página de resultados actual.</summary>
    public PaginaDto<PedidoResumenDto>? Pagina { get; private set; }

    private int _pagina = 1;

    // --- Selectores del formulario ---
    /// <summary>Lista de clientes activos para selección.</summary>
    public IReadOnlyList<ClienteSelectorDto> ClientesDisponibles { get; private set; } = Array.Empty<ClienteSelectorDto>();
    /// <summary>Lista de productos disponibles para agregar a un pedido.</summary>
    public IReadOnlyList<ProductoSelectorDto> ProductosDisponibles { get; private set; } = Array.Empty<ProductoSelectorDto>();

    // --- Panel / Formulario de edición maestro–detalle ---
    /// <summary>Indica si el panel de alta/edición está abierto.</summary>
    public bool Editando { get; private set; }
    /// <summary>Pedido original cargado (null si es alta).</summary>
    public PedidoDetalleDto? Original { get; private set; }
    /// <summary>Cliente seleccionado para el pedido.</summary>
    public int? ClienteId { get; set; }
    /// <summary>Fecha prevista de entrega.</summary>
    public DateOnly? FechaEntrega { get; set; }
    /// <summary>Comentarios u observaciones.</summary>
    public string? Comentarios { get; set; }
    /// <summary>Líneas interactivas de productos del pedido.</summary>
    public List<LineaPedidoEditable> Lineas { get; } = new();

    /// <summary>Total calculado en vivo sumando los subtotales de las líneas.</summary>
    public decimal TotalCalculado => Lineas.Sum(l => l.Subtotal);

    /// <summary>Errores de validación por campo.</summary>
    public Dictionary<string, string> ErroresCampos { get; } = new();

    // --- Diálogos de confirmación ---
    /// <summary>Pedido pendiente de acción.</summary>
    public PedidoResumenDto? PedidoAccion { get; private set; }
    /// <summary>Acción que espera confirmación.</summary>
    public AccionPedido AccionPendiente { get; private set; }
    /// <summary>Indica si hay un diálogo de confirmación abierto.</summary>
    public bool HayConfirmacion => PedidoAccion is not null && AccionPendiente != AccionPedido.Ninguna;

    /// <summary>Título del diálogo de confirmación.</summary>
    public string TituloConfirmacion => AccionPendiente switch
    {
        AccionPedido.MarcarEntregado => "¿Marcar pedido como Entregado?",
        AccionPedido.Cancelar => "¿Cancelar pedido?",
        AccionPedido.DarDeBaja => "¿Dar de baja el pedido?",
        _ => ""
    };

    /// <summary>Mensaje descriptivo del diálogo de confirmación.</summary>
    public string MensajeConfirmacion => AccionPendiente switch
    {
        AccionPedido.MarcarEntregado => $"Se registrará la entrega del Pedido #{PedidoAccion?.Id}. Esta acción no se puede deshacer.",
        AccionPedido.Cancelar => $"Al cancelar el Pedido #{PedidoAccion?.Id}, las existencias de todos sus productos se repondrán al inventario.",
        AccionPedido.DarDeBaja => $"El Pedido #{PedidoAccion?.Id} quedará inactivo. Si aún estaba pendiente, se cancelará y se repondrá el stock.",
        _ => ""
    };

    /// <summary>Texto del botón de confirmación.</summary>
    public string TextoConfirmar => AccionPendiente switch
    {
        AccionPedido.MarcarEntregado => "Marcar Entregado",
        AccionPedido.Cancelar => "Cancelar Pedido",
        AccionPedido.DarDeBaja => "Dar de Baja",
        _ => "Confirmar"
    };

    /// <summary>Indica si el botón de confirmación debe mostrarse con estilo de peligro.</summary>
    public bool EsAccionPeligro => AccionPendiente is AccionPedido.Cancelar or AccionPedido.DarDeBaja;

    /// <summary>Inicializa el ViewModel cargando selectores y la primera página de pedidos.</summary>
    public async Task IniciarAsync()
    {
        await CargarSelectoresAsync();
        await BuscarAsync();
    }

    /// <summary>Carga o refresca las listas de clientes y productos para los selectores.</summary>
    public Task CargarSelectoresAsync() =>
        EjecutarAsync(async ct =>
        {
            ClientesDisponibles = await _pedidos.ClientesSelectorAsync(null, ct);
            ProductosDisponibles = await _pedidos.ProductosSelectorAsync(null, ct);
        });

    /// <summary>Ejecuta la búsqueda aplicando los filtros actuales desde la primera página.</summary>
    public Task BuscarAsync()
    {
        _pagina = 1;
        return CargarAsync();
    }

    /// <summary>Limpia los filtros de búsqueda y restablece el listado.</summary>
    public Task LimpiarAsync()
    {
        TextoCliente = "";
        TextoProducto = "";
        FiltroEstado = null;
        FiltroEntregaDesde = null;
        FiltroEntregaHasta = null;
        FiltroPrecioMin = null;
        FiltroPrecioMax = null;
        FiltroIncluirInactivos = false;
        return BuscarAsync();
    }

    /// <summary>Navega a la página solicitada.</summary>
    public Task IrAPaginaAsync(int pagina)
    {
        _pagina = pagina;
        return CargarAsync();
    }

    private Task CargarAsync() =>
        EjecutarAsync(async ct =>
        {
            var filtro = new FiltroPedidosSolicitud(
                Cliente: string.IsNullOrWhiteSpace(TextoCliente) ? null : TextoCliente,
                Producto: string.IsNullOrWhiteSpace(TextoProducto) ? null : TextoProducto,
                Estado: FiltroEstado,
                EntregaDesde: FiltroEntregaDesde,
                EntregaHasta: FiltroEntregaHasta,
                PrecioMin: FiltroPrecioMin,
                PrecioMax: FiltroPrecioMax,
                IncluirInactivos: FiltroIncluirInactivos,
                Pagina: _pagina,
                TamanoPagina: 10);

            Pagina = await _pedidos.BuscarAsync(filtro, ct);
        });

    /// <summary>Abre el formulario para crear un nuevo pedido.</summary>
    public void Nuevo()
    {
        Original = null;
        Editando = true;
        Error = null;
        ErroresCampos.Clear();
        ClienteId = ClientesDisponibles.FirstOrDefault()?.Id;
        FechaEntrega = DateOnly.FromDateTime(DateTime.Today).AddDays(1);
        Comentarios = "";
        Lineas.Clear();
        OnCambiado();
    }

    /// <summary>Carga y abre el formulario para editar un pedido existente.</summary>
    public Task EditarAsync(int pedidoId) =>
        EjecutarAsync(async ct =>
        {
            Original = await _pedidos.ObtenerAsync(pedidoId, ct);
            Editando = true;
            Error = null;
            ErroresCampos.Clear();
            ClienteId = Original.ClienteId;
            FechaEntrega = Original.FechaEntrega;
            Comentarios = Original.Comentarios ?? "";
            Lineas.Clear();

            foreach (var l in Original.Lineas)
            {
                var prodInfo = ProductosDisponibles.FirstOrDefault(p => p.Id == l.ProductoId);
                Lineas.Add(new LineaPedidoEditable
                {
                    ProductoId = l.ProductoId,
                    Producto = l.Producto,
                    Codigo = l.Codigo,
                    PrecioUnitario = l.PrecioUnitario,
                    Cantidad = l.Cantidad,
                    StockDisponible = prodInfo?.CantidadStock ?? 0
                });
            }
        });

    /// <summary>Cierra el formulario de alta o edición descartando los cambios.</summary>
    public void CancelarEdicion()
    {
        Editando = false;
        Original = null;
        Error = null;
        ErroresCampos.Clear();
        Lineas.Clear();
        OnCambiado();
    }

    /// <summary>Agrega un producto a la lista de líneas o incrementa su cantidad si ya existe.</summary>
    public void AgregarProducto(ProductoSelectorDto prod)
    {
        var existente = Lineas.FirstOrDefault(l => l.ProductoId == prod.Id);
        if (existente is not null)
        {
            existente.Cantidad++;
        }
        else
        {
            Lineas.Add(new LineaPedidoEditable
            {
                ProductoId = prod.Id,
                Codigo = prod.Codigo,
                Producto = prod.Nombre,
                PrecioUnitario = prod.PrecioVenta,
                Cantidad = 1,
                StockDisponible = prod.CantidadStock
            });
        }
        ErroresCampos.Remove("Lineas");
        OnCambiado();
    }

    /// <summary>Elimina una línea del pedido en edición.</summary>
    public void QuitarLinea(LineaPedidoEditable linea)
    {
        Lineas.Remove(linea);
        OnCambiado();
    }

    /// <summary>Actualiza la cantidad de una línea asegurando que sea mayor a cero.</summary>
    public void CambiarCantidad(LineaPedidoEditable linea, int nuevaCantidad)
    {
        if (nuevaCantidad <= 0) return;
        linea.Cantidad = nuevaCantidad;
        OnCambiado();
    }

    /// <summary>Valida y guarda el pedido (alta o edición) a través de la API.</summary>
    public async Task GuardarAsync()
    {
        if (!Validar()) { OnCambiado(); return; }

        var fechaDate = FechaEntrega!.Value;
        var lineasSolicitud = Lineas.Select(l => new LineaPedidoSolicitud(l.ProductoId, l.Cantidad)).ToList();

        var ok = await EjecutarAsync(async ct =>
        {
            if (Original is null)
            {
                var sol = new CrearPedidoSolicitud(ClienteId!.Value, fechaDate, Comentarios, lineasSolicitud);
                await _pedidos.CrearAsync(sol, ct);
            }
            else
            {
                var sol = new ActualizarPedidoSolicitud(fechaDate, Comentarios, lineasSolicitud, Original.RowVersion);
                await _pedidos.ActualizarAsync(Original.Id, sol, ct);
            }
        });

        if (ok)
        {
            Editando = false;
            Original = null;
            Lineas.Clear();
            OnCambiado();
        }
    }

    private bool Validar()
    {
        ErroresCampos.Clear();
        if (!ClienteId.HasValue || ClienteId.Value <= 0)
            ErroresCampos[nameof(ClienteId)] = "Seleccione un cliente para el pedido.";

        if (!FechaEntrega.HasValue)
            ErroresCampos[nameof(FechaEntrega)] = "La fecha de entrega es obligatoria.";
        else if (FechaEntrega.Value < DateOnly.FromDateTime(DateTime.Today))
            ErroresCampos[nameof(FechaEntrega)] = "La fecha de entrega no puede ser anterior a hoy.";

        if (Lineas.Count == 0)
            ErroresCampos["Lineas"] = "Debe agregar al menos un producto al pedido.";

        if (Lineas.Any(l => l.Cantidad <= 0))
            ErroresCampos["Lineas"] = "Las cantidades de los productos deben ser mayores que cero.";

        if (Comentarios?.Trim().Length > 500)
            ErroresCampos[nameof(Comentarios)] = "Los comentarios no pueden superar 500 caracteres.";

        return ErroresCampos.Count == 0;
    }

    /// <summary>Devuelve el error asociado a un campo, o <c>null</c>.</summary>
    public string? ErrorDe(string campo) => ErroresCampos.GetValueOrDefault(campo);

    // --- Confirmaciones de Estado ---
    /// <summary>Abre el diálogo para confirmar la entrega del pedido.</summary>
    public void SolicitarEntrega(PedidoResumenDto p)
    {
        PedidoAccion = p;
        AccionPendiente = AccionPedido.MarcarEntregado;
        OnCambiado();
    }

    /// <summary>Abre el diálogo para confirmar la cancelación del pedido.</summary>
    public void SolicitarCancelacion(PedidoResumenDto p)
    {
        PedidoAccion = p;
        AccionPendiente = AccionPedido.Cancelar;
        OnCambiado();
    }

    /// <summary>Abre el diálogo para confirmar la baja lógica del pedido.</summary>
    public void SolicitarBaja(PedidoResumenDto p)
    {
        PedidoAccion = p;
        AccionPendiente = AccionPedido.DarDeBaja;
        OnCambiado();
    }

    /// <summary>Cancela la solicitud de confirmación cerrando el diálogo.</summary>
    public void CancelarSolicitud()
    {
        PedidoAccion = null;
        AccionPendiente = AccionPedido.Ninguna;
        OnCambiado();
    }

    /// <summary>Ejecuta la acción de estado o baja confirmada por el usuario.</summary>
    public async Task ConfirmarAccionAsync()
    {
        var p = PedidoAccion;
        var accion = AccionPendiente;
        if (p is null || accion == AccionPedido.Ninguna) return;

        await EjecutarAsync(async ct =>
        {
            if (accion == AccionPedido.DarDeBaja)
            {
                await _pedidos.DesactivarAsync(p.Id, ct);
            }
            else
            {
                // Obtenemos el RowVersion del pedido para concurrencia
                var detalle = await _pedidos.ObtenerAsync(p.Id, ct);
                var nuevoEstado = accion == AccionPedido.MarcarEntregado ? EstadosPedido.Entregado : EstadosPedido.Cancelado;
                await _pedidos.CambiarEstadoAsync(p.Id, new CambiarEstadoPedidoSolicitud(nuevoEstado, detalle.RowVersion), ct);
            }
        });

        PedidoAccion = null;
        AccionPendiente = AccionPedido.Ninguna;
        OnCambiado();
    }

    private async void Pedidos_EntidadCambiada(object sender, EntidadCambiadaEventArgs e) => await CargarAsync();

    private void Pedidos_StockInsuficiente(object? sender, string mensaje)
    {
        Error = mensaje;
        OnCambiado();
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        _pedidos.EntidadCambiada -= Pedidos_EntidadCambiada;
        _pedidos.StockInsuficiente -= Pedidos_StockInsuficiente;
        base.Dispose();
    }
}
