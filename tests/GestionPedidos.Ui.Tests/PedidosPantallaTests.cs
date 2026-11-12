using System.Net;
using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun.Api;
using GestionPedidos.Ui.Comun.Eventos;
using GestionPedidos.Ui.Modulos.Pedidos;

namespace GestionPedidos.Ui.Tests;

/// <summary>
/// Servicio y ViewModel de Pedidos ([Pedidos.md §6 y §7], [Testing.md §3]): edición, validaciones, estados, baja,
/// filtros y eventos. Complementa a <see cref="PedidosTests"/>.
/// </summary>
public class PedidosPantallaTests
{
    private static readonly DateOnly Manana = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
    private static readonly byte[] Version = { 7, 7, 7 };

    private static PedidoResumenDto Resumen(int id, string estado = EstadosPedido.Pendiente, bool activo = true) =>
        new(id, 1, "Cliente Demo", "Demo", 2, "Operador Demo", DateTimeOffset.UtcNow, Manana, estado, 100m, activo, null, 1);

    private static PedidoDetalleDto Detalle(int id, string estado = EstadosPedido.Pendiente, bool activo = true) =>
        new(id, 1, "Cliente Demo", "7777-8888", "San Salvador", 2, "Operador Demo", DateTimeOffset.UtcNow, Manana, estado, 100m, activo,
            "Dejar en portería", Version,
            new[]
            {
                new LineaPedidoDetalleDto(10, "Camisa", "PRD-000010", 2, 50m, 100m),
                new LineaPedidoDetalleDto(20, "Pantalón", "PRD-000020", 1, 30m, 30m),
            });

    private static (Entorno entorno, PedidosService servicio) Armar()
    {
        var e = new Entorno();
        e.Manejador.Responder = r =>
        {
            var ruta = r.RequestUri!.AbsolutePath;
            if (ruta.EndsWith("/clientes-selector")) return (HttpStatusCode.OK, new[] { new ClienteSelectorDto(1, "Cliente Demo", "7777-8888", "San Salvador") });
            if (ruta.EndsWith("/productos-selector"))
                return (HttpStatusCode.OK, new[] { new ProductoSelectorDto(10, "PRD-000010", "Camisa", 50m, 5), new ProductoSelectorDto(20, "PRD-000020", "Pantalón", 30m, 8) });
            if (r.Method == HttpMethod.Get && ruta.Contains("/api/v1/pedidos/")) return (HttpStatusCode.OK, Detalle(1));
            if (r.Method == HttpMethod.Get) return (HttpStatusCode.OK, new PaginaDto<PedidoResumenDto>(new[] { Resumen(1), Resumen(2) }, 2, 1, 10));
            return (HttpStatusCode.OK, Detalle(1));
        };
        return (e, new PedidosService(e.Api));
    }

    // ===================== Servicio =====================

    [Fact]
    public async Task Servicio_Actualizar_PublicaActualizada_UnaSolaVez_YUsaPut()
    {
        var (e, servicio) = Armar();
        var recibidos = new List<EntidadCambiadaEventArgs>();
        servicio.EntidadCambiada += (_, a) => recibidos.Add(a);

        await servicio.ActualizarAsync(1, new ActualizarPedidoSolicitud(Manana, "x", new[] { new LineaPedidoSolicitud(10, 1) }, Version), default);

        var ev = Assert.Single(recibidos);
        Assert.Equal(TipoCambio.Actualizada, ev.Tipo);
        Assert.Equal(HttpMethod.Put, e.Manejador.Peticiones.Last().Method);
        Assert.EndsWith("/api/v1/pedidos/1", e.Manejador.Peticiones.Last().RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Servicio_Actualizar_ConErrorDeLaApi_NoPublicaNada_YStockInsuficienteDisparaSuEvento()
    {
        var (e, servicio) = Armar();
        var recibidos = new List<EntidadCambiadaEventArgs>();
        string? stock = null;
        servicio.EntidadCambiada += (_, a) => recibidos.Add(a);
        servicio.StockInsuficiente += (_, m) => stock = m;
        e.Manejador.Responder = _ => (HttpStatusCode.Conflict, new { title = "Camisa: solo quedan 1 unidades.", codigo = CodigosError.StockInsuficiente });

        await Assert.ThrowsAsync<ApiException>(() =>
            servicio.ActualizarAsync(1, new ActualizarPedidoSolicitud(Manana, null, new[] { new LineaPedidoSolicitud(10, 9) }, Version), default));

        Assert.Empty(recibidos);
        Assert.Contains("solo quedan 1", stock);
    }

    [Fact]
    public async Task Servicio_ConflictoDeVersion_NoDisparaStockInsuficiente()
    {
        var (e, servicio) = Armar();
        var disparado = false;
        servicio.StockInsuficiente += (_, _) => disparado = true;
        e.Manejador.Responder = _ => (HttpStatusCode.Conflict, new { title = "El pedido fue modificado.", codigo = CodigosError.Conflicto });

        await Assert.ThrowsAsync<ApiException>(() => servicio.CambiarEstadoAsync(1, new CambiarEstadoPedidoSolicitud(EstadosPedido.Cancelado, Version), default));

        Assert.False(disparado);
    }

    [Fact]
    public async Task Servicio_CambiarEstado_Publica_EstadoCambiado_ConElEstadoActivo()
    {
        var (_, servicio) = Armar();
        var recibidos = new List<EntidadCambiadaEventArgs>();
        servicio.EntidadCambiada += (_, a) => recibidos.Add(a);

        await servicio.CambiarEstadoAsync(1, new CambiarEstadoPedidoSolicitud(EstadosPedido.Entregado, Version), default);

        var ev = Assert.Single(recibidos);
        Assert.Equal(TipoCambio.EstadoCambiado, ev.Tipo);
        Assert.True(ev.Activo);
    }

    [Fact]
    public async Task Servicio_Desactivar_Publica_ActivoFalso_SoloTrasExito_YUsaPatch()
    {
        var (e, servicio) = Armar();
        var recibidos = new List<EntidadCambiadaEventArgs>();
        servicio.EntidadCambiada += (_, a) => recibidos.Add(a);

        await servicio.DesactivarAsync(5, default);

        var ev = Assert.Single(recibidos);
        Assert.False(ev.Activo);
        Assert.Equal(5, ev.Id);
        Assert.Equal(HttpMethod.Patch, e.Manejador.Peticiones.Last().Method);
        Assert.EndsWith("/api/v1/pedidos/5/desactivar", e.Manejador.Peticiones.Last().RequestUri!.AbsolutePath);

        e.Manejador.Responder = _ => (HttpStatusCode.NotFound, new { title = "El pedido no existe.", codigo = CodigosError.NoEncontrado });
        await Assert.ThrowsAsync<ApiException>(() => servicio.DesactivarAsync(6, default));
        Assert.Single(recibidos);
    }

    [Fact]
    public async Task Servicio_SinSuscriptores_NoHayExcepcion()
    {
        var (_, servicio) = Armar();
        await servicio.CrearAsync(new CrearPedidoSolicitud(1, Manana, null, new[] { new LineaPedidoSolicitud(10, 1) }), default);
        await servicio.DesactivarAsync(1, default);
    }

    [Fact]
    public async Task Servicio_Buscar_ConstruyeLaConsultaConTodosLosFiltros_YEscapaElTexto()
    {
        var (e, servicio) = Armar();

        await servicio.BuscarAsync(new FiltroPedidosSolicitud(
            ClienteId: 3, Cliente: "  O'Brien & Co ", Producto: "camisa azul", PrecioMin: 1.5m, PrecioMax: 20m, Estado: EstadosPedido.Entregado,
            EntregaDesde: new DateOnly(2026, 11, 1), EntregaHasta: new DateOnly(2026, 11, 30), UsuarioId: 2, IncluirInactivos: true, Pagina: 2, TamanoPagina: 10), default);

        var url = e.Manejador.Peticiones.Last().RequestUri!.PathAndQuery;
        Assert.Contains("clienteId=3", url);
        Assert.Contains("cliente=O%27Brien%20%26%20Co", url);
        Assert.Contains("producto=camisa%20azul", url);
        Assert.Contains("estado=Entregado", url);
        Assert.Contains("entregaDesde=2026-11-01", url);
        Assert.Contains("entregaHasta=2026-11-30", url);
        Assert.Contains("usuarioId=2", url);
        Assert.Contains("incluirInactivos=true", url);
        Assert.Contains("pagina=2", url);
        Assert.Contains("tamanoPagina=10", url);
    }

    [Fact]
    public async Task Servicio_Buscar_SinFiltros_NoAgregaQueryString()
    {
        var (e, servicio) = Armar();
        await servicio.BuscarAsync(new FiltroPedidosSolicitud(), default);
        Assert.Equal("/api/v1/pedidos", e.Manejador.Peticiones.Last().RequestUri!.PathAndQuery);
    }

    // ===================== ViewModel =====================

    [Fact]
    public async Task ViewModel_Editar_CargaCabeceraLineasYStockDisponible()
    {
        var (_, servicio) = Armar();
        using var vm = new PedidosViewModel(servicio);
        await vm.IniciarAsync();

        await vm.EditarAsync(1);

        Assert.True(vm.Editando);
        Assert.Equal(1, vm.Original!.Id);
        Assert.Equal(1, vm.ClienteId);
        Assert.Equal("Dejar en portería", vm.Comentarios);
        Assert.Equal(2, vm.Lineas.Count);
        Assert.Equal(130m, vm.TotalCalculado);   // 2*50 + 1*30
    }

    [Fact]
    public async Task ViewModel_GuardarEdicion_EnviaPut_ConElRowVersionOriginal_YCierraElPanel()
    {
        var (e, servicio) = Armar();
        using var vm = new PedidosViewModel(servicio);
        await vm.IniciarAsync();
        await vm.EditarAsync(1);
        vm.CambiarCantidad(vm.Lineas.First(), 3);

        await vm.GuardarAsync();

        var put = e.Manejador.Peticiones.Last(p => p.Method == HttpMethod.Put);
        Assert.EndsWith("/api/v1/pedidos/1", put.RequestUri!.AbsolutePath);
        var cuerpo = e.Manejador.Cuerpos[e.Manejador.Peticiones.IndexOf(put)];
        Assert.Contains("\"cantidad\":3", cuerpo);
        Assert.Contains(Convert.ToBase64String(Version), cuerpo);
        Assert.False(vm.Editando);
        Assert.Empty(vm.Lineas);
    }

    [Fact]
    public async Task ViewModel_Nuevo_SinProductos_ValidaYNoLlamaALaApi()
    {
        var (e, servicio) = Armar();
        using var vm = new PedidosViewModel(servicio);
        await vm.IniciarAsync();
        var antes = e.Manejador.Peticiones.Count;

        vm.Nuevo();
        await vm.GuardarAsync();

        Assert.Equal(antes, e.Manejador.Peticiones.Count);
        Assert.NotNull(vm.ErrorDe("Lineas"));
    }

    [Fact]
    public async Task ViewModel_Guardar_SinCliente_FechaPasada_YComentariosLargos_MuestraCadaError()
    {
        var (e, servicio) = Armar();
        using var vm = new PedidosViewModel(servicio);
        await vm.IniciarAsync();
        vm.Nuevo();
        vm.AgregarProducto(vm.ProductosDisponibles.First());
        vm.ClienteId = null;
        vm.FechaEntrega = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
        vm.Comentarios = new string('x', 501);
        var antes = e.Manejador.Peticiones.Count;

        await vm.GuardarAsync();

        Assert.NotNull(vm.ErrorDe(nameof(vm.ClienteId)));
        Assert.NotNull(vm.ErrorDe(nameof(vm.FechaEntrega)));
        Assert.NotNull(vm.ErrorDe(nameof(vm.Comentarios)));
        Assert.Equal(antes, e.Manejador.Peticiones.Count);
        Assert.True(vm.Editando);
    }

    [Fact]
    public async Task ViewModel_Guardar_SinFechaDeEntrega_Valida()
    {
        var (_, servicio) = Armar();
        using var vm = new PedidosViewModel(servicio);
        await vm.IniciarAsync();
        vm.Nuevo();
        vm.AgregarProducto(vm.ProductosDisponibles.First());
        vm.FechaEntrega = null;

        await vm.GuardarAsync();

        Assert.NotNull(vm.ErrorDe(nameof(vm.FechaEntrega)));
    }

    [Fact]
    public async Task ViewModel_AgregarElMismoProductoDosVeces_SumaUnaUnidad_NoDuplicaLaLinea()
    {
        var (_, servicio) = Armar();
        using var vm = new PedidosViewModel(servicio);
        await vm.IniciarAsync();
        vm.Nuevo();
        var camisa = vm.ProductosDisponibles.First();

        vm.AgregarProducto(camisa);
        vm.AgregarProducto(camisa);

        var linea = Assert.Single(vm.Lineas);
        Assert.Equal(2, linea.Cantidad);
        Assert.Equal(100m, vm.TotalCalculado);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task ViewModel_CambiarCantidad_NoPositiva_SeIgnora(int cantidad)
    {
        var (_, servicio) = Armar();
        using var vm = new PedidosViewModel(servicio);
        await vm.IniciarAsync();
        vm.Nuevo();
        vm.AgregarProducto(vm.ProductosDisponibles.First());

        vm.CambiarCantidad(vm.Lineas.First(), cantidad);

        Assert.Equal(1, vm.Lineas.First().Cantidad);
    }

    [Fact]
    public async Task ViewModel_StockInsuficiente_MuestraElMensajeEnError_YMantieneElPanelAbierto()
    {
        var (e, servicio) = Armar();
        using var vm = new PedidosViewModel(servicio);
        await vm.IniciarAsync();
        vm.Nuevo();
        vm.ClienteId = vm.ClientesDisponibles.First().Id;   // la persona elige el cliente
        vm.AgregarProducto(vm.ProductosDisponibles.First());
        e.Manejador.Responder = r => r.Method == HttpMethod.Post
            ? (HttpStatusCode.Conflict, new { title = "Camisa: solo quedan 1 unidades disponibles.", codigo = CodigosError.StockInsuficiente })
            : (HttpStatusCode.OK, null);

        await vm.GuardarAsync();

        Assert.Contains("solo quedan 1", vm.Error);
        Assert.True(vm.Editando);
    }

    [Fact]
    public async Task ViewModel_CancelarEdicion_LimpiaTodo()
    {
        var (_, servicio) = Armar();
        using var vm = new PedidosViewModel(servicio);
        await vm.IniciarAsync();
        await vm.EditarAsync(1);

        vm.CancelarEdicion();

        Assert.False(vm.Editando);
        Assert.Null(vm.Original);
        Assert.Empty(vm.Lineas);
        Assert.Empty(vm.ErroresCampos);
    }

    [Theory]
    [InlineData(AccionPedido.MarcarEntregado, EstadosPedido.Entregado)]
    [InlineData(AccionPedido.Cancelar, EstadosPedido.Cancelado)]
    public async Task ViewModel_ConfirmarEstado_EnviaPatch_ConElEstadoYLaVersion(AccionPedido accion, string esperado)
    {
        var (e, servicio) = Armar();
        using var vm = new PedidosViewModel(servicio);
        await vm.IniciarAsync();
        var pedido = vm.Pagina!.Elementos.First();
        if (accion == AccionPedido.MarcarEntregado) vm.SolicitarEntrega(pedido); else vm.SolicitarCancelacion(pedido);

        await vm.ConfirmarAccionAsync();

        var patch = e.Manejador.Peticiones.Last(p => p.Method == HttpMethod.Patch);
        Assert.EndsWith($"/api/v1/pedidos/{pedido.Id}/estado", patch.RequestUri!.AbsolutePath);
        var cuerpo = e.Manejador.Cuerpos[e.Manejador.Peticiones.IndexOf(patch)];
        Assert.Contains(esperado, cuerpo);
        Assert.Contains(Convert.ToBase64String(Version), cuerpo);
    }

    [Fact]
    public async Task ViewModel_ConfirmarBaja_EnviaPatchDesactivar()
    {
        var (e, servicio) = Armar();
        using var vm = new PedidosViewModel(servicio);
        await vm.IniciarAsync();
        var pedido = vm.Pagina!.Elementos.First();

        vm.SolicitarBaja(pedido);
        Assert.True(vm.EsAccionPeligro);
        await vm.ConfirmarAccionAsync();

        var patch = e.Manejador.Peticiones.Last(p => p.Method == HttpMethod.Patch);
        Assert.EndsWith($"/api/v1/pedidos/{pedido.Id}/desactivar", patch.RequestUri!.AbsolutePath);
        Assert.False(vm.HayConfirmacion);
    }

    [Fact]
    public async Task ViewModel_CancelarSolicitud_CierraElDialogo_SinLlamarALaApi()
    {
        var (e, servicio) = Armar();
        using var vm = new PedidosViewModel(servicio);
        await vm.IniciarAsync();
        vm.SolicitarCancelacion(vm.Pagina!.Elementos.First());
        var antes = e.Manejador.Peticiones.Count;

        vm.CancelarSolicitud();

        Assert.False(vm.HayConfirmacion);
        Assert.Equal(antes, e.Manejador.Peticiones.Count);
    }

    [Fact]
    public async Task ViewModel_ConfirmarAccion_ConErrorDeLaApi_MuestraElError_YCierraElDialogo()
    {
        var (e, servicio) = Armar();
        using var vm = new PedidosViewModel(servicio);
        await vm.IniciarAsync();
        vm.SolicitarEntrega(vm.Pagina!.Elementos.First());
        e.Manejador.Responder = r => r.Method == HttpMethod.Patch
            ? (HttpStatusCode.UnprocessableEntity, new { title = "Solo se pueden cambiar de estado pedidos pendientes.", codigo = CodigosError.Regla })
            : (HttpStatusCode.OK, Detalle(1));

        await vm.ConfirmarAccionAsync();

        Assert.Contains("pendientes", vm.Error);
        Assert.False(vm.HayConfirmacion);
    }

    [Fact]
    public async Task ViewModel_LimpiarFiltros_RestableceTodoYBuscaDeNuevo()
    {
        var (e, servicio) = Armar();
        using var vm = new PedidosViewModel(servicio);
        await vm.IniciarAsync();
        vm.TextoCliente = "ana"; vm.TextoProducto = "x"; vm.FiltroEstado = EstadosPedido.Cancelado; vm.FiltroIncluirInactivos = true;
        var antes = e.Manejador.Peticiones.Count;

        await vm.LimpiarAsync();

        Assert.Equal("", vm.TextoCliente);
        Assert.Equal("", vm.TextoProducto);
        Assert.Null(vm.FiltroEstado);
        Assert.False(vm.FiltroIncluirInactivos);
        Assert.True(e.Manejador.Peticiones.Count > antes);
        Assert.DoesNotContain("estado=", e.Manejador.Peticiones.Last().RequestUri!.Query);
    }

    [Fact]
    public async Task ViewModel_Buscar_UsaLosFiltrosYVuelveALaPrimeraPagina()
    {
        var (e, servicio) = Armar();
        using var vm = new PedidosViewModel(servicio);
        await vm.IniciarAsync();
        await vm.IrAPaginaAsync(3);
        vm.TextoCliente = "lópez";
        vm.FiltroEstado = EstadosPedido.Pendiente;

        await vm.BuscarAsync();

        var q = e.Manejador.Peticiones.Last().RequestUri!.Query;
        Assert.Contains("cliente=l%C3%B3pez", q);
        Assert.Contains("estado=Pendiente", q);
        Assert.Contains("pagina=1", q);
    }

    // ---------- Eventos ----------

    [Fact]
    public async Task ViewModel_AlRecibirUnaEntidadCambiada_RefrescaElListado()
    {
        var (e, servicio) = Armar();
        using var vm = new PedidosViewModel(servicio);
        await vm.IniciarAsync();
        var antes = e.Manejador.Peticiones.Count(p => p.Method == HttpMethod.Get && p.RequestUri!.AbsolutePath == "/api/v1/pedidos");

        await servicio.DesactivarAsync(1, default);
        await Task.Delay(100);   // el manejador del evento es asíncrono (async void)

        var despues = e.Manejador.Peticiones.Count(p => p.Method == HttpMethod.Get && p.RequestUri!.AbsolutePath == "/api/v1/pedidos");
        Assert.True(despues > antes);
    }

    [Fact]
    public async Task ViewModel_TrasDispose_YaNoReaccionaAlosEventos()
    {
        var (e, servicio) = Armar();
        var vm = new PedidosViewModel(servicio);
        await vm.IniciarAsync();
        vm.Dispose();
        var antes = e.Manejador.Peticiones.Count(p => p.Method == HttpMethod.Get && p.RequestUri!.AbsolutePath == "/api/v1/pedidos");

        await servicio.DesactivarAsync(1, default);
        await Task.Delay(100);

        var despues = e.Manejador.Peticiones.Count(p => p.Method == HttpMethod.Get && p.RequestUri!.AbsolutePath == "/api/v1/pedidos");
        Assert.Equal(antes, despues);
    }

    [Fact]
    public async Task ViewModel_TrasDispose_StockInsuficienteYaNoCambiaElError()
    {
        var (e, servicio) = Armar();
        var vm = new PedidosViewModel(servicio);
        await vm.IniciarAsync();
        vm.Dispose();
        e.Manejador.Responder = _ => (HttpStatusCode.Conflict, new { title = "Sin stock.", codigo = CodigosError.StockInsuficiente });

        await Assert.ThrowsAsync<ApiException>(() =>
            servicio.CrearAsync(new CrearPedidoSolicitud(1, Manana, null, new[] { new LineaPedidoSolicitud(10, 99) }), default));

        Assert.Null(vm.Error);
    }

    [Fact]
    public async Task ViewModel_ElCambioDeEstadoNotificaAlaPaginaPorElEventoCambiado()
    {
        var (_, servicio) = Armar();
        using var vm = new PedidosViewModel(servicio);
        await vm.IniciarAsync();
        var avisos = 0;
        vm.Cambiado += (_, _) => avisos++;

        vm.SolicitarCancelacion(vm.Pagina!.Elementos.First());

        Assert.True(avisos >= 1);
    }

    // ---------- Pendientes de la revisión (rojas hasta corregirlas) ----------

    /// <summary>Revisión del PR #2 (<c>PedidosViewModel.cs:240</c>): la cantidad no debe superar el stock disponible.</summary>
    [Fact]
    public async Task ViewModel_CambiarCantidad_NoDebePasarDelStockDisponible()
    {
        var (_, servicio) = Armar();
        using var vm = new PedidosViewModel(servicio);
        await vm.IniciarAsync();
        vm.Nuevo();
        var camisa = vm.ProductosDisponibles.First();   // stock 5
        vm.AgregarProducto(camisa);

        vm.CambiarCantidad(vm.Lineas.First(), 99);

        Assert.True(vm.Lineas.First().Cantidad <= camisa.CantidadStock);
    }

    /// <summary>
    /// Revisión del PR #2 (<c>PedidosViewModel.cs:240</c>): al editar, lo disponible de una línea es el stock más lo que el propio
    /// pedido ya reservó.
    /// </summary>
    [Fact]
    public async Task ViewModel_Editar_ElStockDisponibleIncluyeLoQueElPedidoYaReservo()
    {
        var (_, servicio) = Armar();
        using var vm = new PedidosViewModel(servicio);
        await vm.IniciarAsync();

        await vm.EditarAsync(1);

        var camisa = vm.Lineas.Single(l => l.ProductoId == 10);   // stock del selector 5 + 2 ya reservadas
        Assert.Equal(7, camisa.StockDisponible);
    }

    /// <summary>Revisión del PR #2 (<c>PedidosViewModel.cs:211</c>): "Nuevo pedido" no debe elegir un cliente por el usuario.</summary>
    [Fact]
    public async Task ViewModel_Nuevo_NoDebePreseleccionarUnCliente()
    {
        var (_, servicio) = Armar();
        using var vm = new PedidosViewModel(servicio);
        await vm.IniciarAsync();

        vm.Nuevo();

        Assert.Null(vm.ClienteId);
    }
}
