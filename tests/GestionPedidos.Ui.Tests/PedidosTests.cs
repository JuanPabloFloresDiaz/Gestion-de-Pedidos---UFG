using System.Net;
using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun.Eventos;
using GestionPedidos.Ui.Modulos.Pedidos;

namespace GestionPedidos.Ui.Tests;

public class PedidosTests
{
    private static PedidoResumenDto Resumen(int id, string estado = EstadosPedido.Pendiente, bool activo = true) =>
        new(id, 1, "Cliente Demo", "Demo", 2, "Operador Demo",
            DateTimeOffset.UtcNow, DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
            estado, 100.00m, activo, "Comentarios", 1);

    private static PedidoDetalleDto Detalle(int id, string estado = EstadosPedido.Pendiente, bool activo = true) =>
        new(id, 1, "Cliente Demo", "7777-8888", "San Salvador", 2, "Operador Demo",
            DateTimeOffset.UtcNow, DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
            estado, 100.00m, activo, "Comentarios", Array.Empty<byte>(),
            new[] { new LineaPedidoDetalleDto(10, "Camisa", "PRD-000010", 2, 50.00m, 100.00m) });

    private static (Entorno entorno, PedidosService servicio) Crear()
    {
        var e = new Entorno();
        e.Manejador.Responder = r =>
        {
            var ruta = r.RequestUri!.AbsolutePath;
            if (ruta.EndsWith("/clientes-selector"))
                return (HttpStatusCode.OK, new[] { new ClienteSelectorDto(1, "Cliente Demo", "7777-8888", "San Salvador") });

            if (ruta.EndsWith("/productos-selector"))
                return (HttpStatusCode.OK, new[] { new ProductoSelectorDto(10, "PRD-000010", "Camisa", 50.00m, 5) });

            if (r.Method == HttpMethod.Get && ruta.Contains("/api/v1/pedidos/"))
                return (HttpStatusCode.OK, Detalle(1));

            if (r.Method == HttpMethod.Get)
                return (HttpStatusCode.OK, new PaginaDto<PedidoResumenDto>(new[] { Resumen(1), Resumen(2) }, 2, 1, 10));

            return (HttpStatusCode.OK, Detalle(1));
        };
        return (e, new PedidosService(e.Api));
    }

    [Fact]
    public async Task Servicio_Crear_PublicaEntidadCambiada_SoloTrasExito()
    {
        var (e, servicio) = Crear();
        var recibidos = new List<EntidadCambiadaEventArgs>();
        servicio.EntidadCambiada += (s, a) => { Assert.Same(servicio, s); recibidos.Add(a); };

        var sol = new CrearPedidoSolicitud(1, DateOnly.FromDateTime(DateTime.Today.AddDays(3)), "Test",
            new[] { new LineaPedidoSolicitud(10, 1) });

        await servicio.CrearAsync(sol, default);

        var ev = Assert.Single(recibidos);
        Assert.Equal(TipoEntidad.Pedido, ev.Entidad);
        Assert.Equal(TipoCambio.Registrada, ev.Tipo);

        // Si la API falla, no se publica nada
        e.Manejador.Responder = _ => (HttpStatusCode.Conflict, new { title = "Stock insuficiente", codigo = CodigosError.StockInsuficiente });
        await Assert.ThrowsAsync<GestionPedidos.Ui.Comun.Api.ApiException>(() => servicio.CrearAsync(sol, default));
        Assert.Single(recibidos); // sin nuevo evento
    }

    [Fact]
    public async Task Servicio_StockInsuficiente_DisparaEventoPropio()
    {
        var (e, servicio) = Crear();
        string? mensajeStock = null;
        servicio.StockInsuficiente += (_, m) => mensajeStock = m;

        e.Manejador.Responder = _ => (HttpStatusCode.Conflict, new { title = "Solo quedan 2 unidades.", codigo = CodigosError.StockInsuficiente });

        var sol = new CrearPedidoSolicitud(1, DateOnly.FromDateTime(DateTime.Today.AddDays(3)), null,
            new[] { new LineaPedidoSolicitud(10, 5) });

        await Assert.ThrowsAsync<GestionPedidos.Ui.Comun.Api.ApiException>(() => servicio.CrearAsync(sol, default));
        Assert.NotNull(mensajeStock);
        Assert.Contains("Solo quedan 2 unidades", mensajeStock);
    }

    [Fact]
    public async Task ViewModel_Iniciar_CargaSelectoresYPrimeraPagina()
    {
        var (_, servicio) = Crear();
        using var vm = new PedidosViewModel(servicio);

        await vm.IniciarAsync();

        Assert.Single(vm.ClientesDisponibles);
        Assert.Single(vm.ProductosDisponibles);
        Assert.Equal(2, vm.Pagina!.Total);
    }

    [Fact]
    public async Task ViewModel_AgregarYQuitarProductos_RecalculaTotalEnVivo()
    {
        var (_, servicio) = Crear();
        using var vm = new PedidosViewModel(servicio);
        await vm.IniciarAsync();

        vm.Nuevo();
        Assert.Equal(0m, vm.TotalCalculado);

        var prod = vm.ProductosDisponibles.First();
        vm.AgregarProducto(prod); // 1 unidad a $50.00
        Assert.Equal(50.00m, vm.TotalCalculado);

        vm.CambiarCantidad(vm.Lineas.First(), 3); // 3 unidades a $50.00
        Assert.Equal(150.00m, vm.TotalCalculado);

        vm.QuitarLinea(vm.Lineas.First());
        Assert.Equal(0m, vm.TotalCalculado);
    }

    [Fact]
    public async Task ViewModel_GuardarSinLineas_MuestraError_SinLlamarALaApi()
    {
        var (e, servicio) = Crear();
        using var vm = new PedidosViewModel(servicio);
        await vm.IniciarAsync();
        var peticionesAntes = e.Manejador.Peticiones.Count;

        vm.Nuevo(); // sin líneas
        await vm.GuardarAsync();

        Assert.NotNull(vm.ErrorDe("Lineas"));
        Assert.Equal(peticionesAntes, e.Manejador.Peticiones.Count);
        Assert.True(vm.Editando);
    }

    [Fact]
    public async Task ViewModel_ConfirmarAccion_Entregar_LlamaApiYCierraDialogo()
    {
        var (_, servicio) = Crear();
        using var vm = new PedidosViewModel(servicio);
        await vm.IniciarAsync();

        var pedido = vm.Pagina!.Elementos.First();
        vm.SolicitarEntrega(pedido);

        Assert.True(vm.HayConfirmacion);
        Assert.Equal(AccionPedido.MarcarEntregado, vm.AccionPendiente);

        await vm.ConfirmarAccionAsync();

        Assert.False(vm.HayConfirmacion);
        Assert.Equal(AccionPedido.Ninguna, vm.AccionPendiente);
    }
}
