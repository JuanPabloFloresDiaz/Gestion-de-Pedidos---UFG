using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun;

namespace GestionPedidos.Ui.Tienda;

/// <summary>Estado y lógica de "Mis pedidos": lista paginada y cancelación con confirmación.</summary>
public sealed class MisPedidosViewModel : ViewModelBase
{
    private readonly PedidosClienteService _pedidos;
    private int _pagina = 1;

    /// <summary>Crea el ViewModel.</summary>
    public MisPedidosViewModel(PedidosClienteService pedidos) => _pedidos = pedidos;

    /// <summary>Página de pedidos.</summary>
    public PaginaDto<PedidoClienteDto>? Pagina { get; private set; }
    /// <summary>Total de páginas.</summary>
    public int TotalPaginas => Pagina is null ? 0 : (int)Math.Ceiling(Pagina.Total / (double)Pagina.TamanoPagina);
    /// <summary>Pedido por cancelar (espera confirmación), o <c>null</c>.</summary>
    public PedidoClienteDto? PorCancelar { get; private set; }
    /// <summary>Indica si ya hubo una primera carga.</summary>
    public bool Cargado { get; private set; }

    /// <summary>Carga la primera página.</summary>
    public Task IniciarAsync() => IrAPaginaAsync(1);

    /// <summary>Va a una página.</summary>
    public async Task IrAPaginaAsync(int pagina)
    {
        _pagina = pagina;
        await EjecutarAsync(async ct => Pagina = await _pedidos.ListarAsync(_pagina, ct));
        Cargado = true;
        OnCambiado();
    }

    /// <summary>Pide confirmar la cancelación de un pedido pendiente.</summary>
    public void SolicitarCancelacion(PedidoClienteDto pedido) { PorCancelar = pedido; OnCambiado(); }

    /// <summary>Cierra el diálogo sin cancelar nada.</summary>
    public void DescartarCancelacion() { PorCancelar = null; OnCambiado(); }

    /// <summary>Cancela el pedido confirmado; el diálogo se cierra y un error (por ejemplo, que ya no esté pendiente) se muestra en pantalla.</summary>
    public async Task ConfirmarCancelacionAsync()
    {
        var pedido = PorCancelar;
        if (pedido is null) return;
        var ok = await EjecutarAsync(ct => _pedidos.CancelarAsync(pedido.Id, ct), cancelarAnterior: false);
        PorCancelar = null;
        var mensaje = Error;
        await IrAPaginaAsync(_pagina);   // refresca el estado real (también si falló)
        if (!ok) { Error = mensaje; OnCambiado(); }
    }
}
