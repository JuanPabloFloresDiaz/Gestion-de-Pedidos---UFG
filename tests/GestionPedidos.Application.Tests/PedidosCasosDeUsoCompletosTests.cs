using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Application.Pedidos;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Clientes;
using GestionPedidos.Domain.Pedidos;
using GestionPedidos.Domain.Productos;
using Microsoft.Extensions.Time.Testing;

namespace GestionPedidos.Application.Tests;

/// <summary>Unidad de trabajo que simula que otra persona modificó los mismos datos (concurrencia optimista).</summary>
internal sealed class UnidadDeTrabajoConConflicto : IUnitOfWork
{
    public Task GuardarAsync(CancellationToken ct) => throw new ConcurrenciaException();
}

/// <summary>
/// Casos de uso de pedidos ([Pedidos.md §4]): validaciones, stock, estados, baja lógica, concurrencia y eventos
/// (una vez y solo si la operación tuvo éxito, [Testing.md §3]). Complementa a <see cref="PedidosCasosDeUsoTests"/>.
/// </summary>
public class PedidosCasosDeUsoCompletosTests
{
    // 12:00 UTC del 8-nov = 06:00 del 8-nov en El Salvador
    private static readonly DateTimeOffset Ahora = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoy = new(2026, 11, 8);
    private static readonly DateOnly FechaEntrega = new(2026, 11, 15);
    private static readonly byte[] Version = { 1, 2, 3 };

    private readonly FakeTimeProvider _reloj = new(Ahora);
    private readonly UnitOfWorkFake _uow = new();
    private readonly EventosAplicacion _eventos = new();
    private readonly PedidoRepositoryFake _repo = new();
    private readonly List<EntidadCambiadaServidorEventArgs> _publicados = new();

    private readonly Producto _teclado = new() { Id = 10, Codigo = "PRD-000010", Nombre = "Teclado", PrecioVenta = 50m, CantidadStock = 5, Activo = true };
    private readonly Producto _mouse = new() { Id = 20, Codigo = "PRD-000020", Nombre = "Mouse", PrecioVenta = 25m, CantidadStock = 10, Activo = true };
    private readonly Producto _inactivo = new() { Id = 30, Codigo = "PRD-000030", Nombre = "Descontinuado", PrecioVenta = 9m, CantidadStock = 10, Activo = false };

    public PedidosCasosDeUsoCompletosTests()
    {
        _repo.Clientes.Add(new Cliente { Id = 1, Nombres = "Carlos", Apellidos = "López", Telefono = "7777-8888", Direccion = "San Salvador", Activo = true });
        _repo.Clientes.Add(new Cliente { Id = 2, Nombres = "Inactivo", Apellidos = "Pérez", Telefono = "7000-0000", Direccion = "Santa Ana", Activo = false });
        _repo.Productos.AddRange(new[] { _teclado, _mouse, _inactivo });
        _eventos.EntidadCambiada += (_, e) => _publicados.Add(e);
    }

    private RegistrarPedido Registrar(IUnitOfWork? uow = null, TimeProvider? reloj = null) => new(_repo, uow ?? _uow, _eventos, reloj ?? _reloj);
    private ActualizarPedido Actualizar(IUnitOfWork? uow = null) => new(_repo, uow ?? _uow, _eventos, _reloj);
    private CambiarEstadoPedido CambiarEstado(IUnitOfWork? uow = null) => new(_repo, uow ?? _uow, _eventos);
    private DesactivarPedido Desactivar(IUnitOfWork? uow = null) => new(_repo, uow ?? _uow, _eventos, _reloj);

    private static CrearPedidoSolicitud Crear(DateOnly? entrega = null, string? comentarios = null, int clienteId = 1, params (int Producto, int Cantidad)[] lineas) =>
        new(clienteId, entrega ?? FechaEntrega, comentarios, lineas.Select(l => new LineaPedidoSolicitud(l.Producto, l.Cantidad)).ToList());

    private static ActualizarPedidoSolicitud Editar(DateOnly? entrega = null, string? comentarios = null, byte[]? version = null, params (int Producto, int Cantidad)[] lineas) =>
        new(entrega ?? FechaEntrega, comentarios, lineas.Select(l => new LineaPedidoSolicitud(l.Producto, l.Cantidad)).ToList(), version ?? Version);

    /// <summary>Un pedido Pendiente ya guardado con las líneas indicadas (descuenta el stock como lo haría el registro).</summary>
    private Pedido PedidoExistente(int id, params (Producto Producto, int Cantidad)[] lineas)
    {
        var p = Pedido.Crear(1, 99, Ahora, FechaEntrega, null);
        p.Id = id;
        p.Cliente = _repo.Clientes[0];
        foreach (var (producto, cantidad) in lineas) p.AgregarLinea(producto, cantidad);
        _repo.Pedidos.Add(p);
        return p;
    }

    private static void AssertError(Resultado res, TipoError tipo, string? codigo = null)
    {
        Assert.False(res.Exito);
        Assert.Equal(tipo, res.Error!.Tipo);
        if (codigo is not null) Assert.Equal(codigo, res.Error.Codigo);
    }

    // ===================== RegistrarPedido =====================

    [Fact]
    public async Task Registrar_SinLineas_Validacion_SinGuardarNiEvento()
    {
        var res = await Registrar().EjecutarAsync(Crear(), 99, default);

        AssertError(res, TipoError.Validacion);
        Assert.Equal(0, _uow.Guardados);
        Assert.Empty(_publicados);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public async Task Registrar_CantidadNoPositiva_Validacion(int cantidad)
    {
        var res = await Registrar().EjecutarAsync(Crear(lineas: (10, cantidad)), 99, default);

        AssertError(res, TipoError.Validacion);
        Assert.Equal(5, _teclado.CantidadStock);
    }

    [Fact]
    public async Task Registrar_Comentarios501_Validacion() =>
        AssertError(await Registrar().EjecutarAsync(Crear(comentarios: new string('x', 501), lineas: (10, 1)), 99, default), TipoError.Validacion);

    [Fact]
    public async Task Registrar_EntregaAnteriorAHoy_Validacion() =>
        AssertError(await Registrar().EjecutarAsync(Crear(entrega: Hoy.AddDays(-1), lineas: (10, 1)), 99, default), TipoError.Validacion);

    [Fact]
    public async Task Registrar_EntregaHoy_EsValida()
    {
        var res = await Registrar().EjecutarAsync(Crear(entrega: Hoy, lineas: (10, 1)), 99, default);
        Assert.True(res.Exito);
    }

    [Fact]
    public async Task Registrar_ClienteInexistente_Validacion() =>
        AssertError(await Registrar().EjecutarAsync(Crear(clienteId: 999, lineas: (10, 1)), 99, default), TipoError.Validacion);

    [Fact]
    public async Task Registrar_ProductoInexistente_Validacion_YNoGuarda()
    {
        var res = await Registrar().EjecutarAsync(Crear(lineas: (999, 1)), 99, default);

        AssertError(res, TipoError.Validacion);
        Assert.Equal(0, _uow.Guardados);
    }

    [Fact]
    public async Task Registrar_ProductoInactivo_FallaPorInventario_SinTocarElStock()
    {
        var res = await Registrar().EjecutarAsync(Crear(lineas: (30, 1)), 99, default);

        AssertError(res, TipoError.Conflicto, CodigosError.StockInsuficiente);
        Assert.Contains("Descontinuado", res.Error!.Mensaje);
        Assert.Equal(10, _inactivo.CantidadStock);
    }

    [Fact]
    public async Task Registrar_ProductoAgotado_DiceAgotado()
    {
        _teclado.CantidadStock = 0;

        var res = await Registrar().EjecutarAsync(Crear(lineas: (10, 1)), 99, default);

        AssertError(res, TipoError.Conflicto, CodigosError.StockInsuficiente);
        Assert.Contains("agotado", res.Error!.Mensaje);
    }

    [Fact]
    public async Task Registrar_VariosProductosSinStock_ListaTodosEnElMensaje_Y_NoDescuentaNinguno()
    {
        var res = await Registrar().EjecutarAsync(Crear(lineas: new[] { (10, 50), (20, 50) }), 99, default);

        AssertError(res, TipoError.Conflicto, CodigosError.StockInsuficiente);
        Assert.Contains("Teclado", res.Error!.Mensaje);
        Assert.Contains("Mouse", res.Error.Mensaje);
        Assert.Equal(5, _teclado.CantidadStock);
        Assert.Equal(10, _mouse.CantidadStock);
        Assert.Empty(_repo.Pedidos);
    }

    [Fact]
    public async Task Registrar_LineasRepetidasDelMismoProducto_SeAgrupanEnUnaSola()
    {
        var res = await Registrar().EjecutarAsync(Crear(lineas: new[] { (10, 1), (10, 2) }), 99, default);

        Assert.True(res.Exito);
        var linea = Assert.Single(res.Valor!.Lineas);
        Assert.Equal(3, linea.Cantidad);
        Assert.Equal(2, _teclado.CantidadStock);
    }

    [Fact]
    public async Task Registrar_AgrupadasQueSuperanElStock_Falla()
    {
        var res = await Registrar().EjecutarAsync(Crear(lineas: new[] { (10, 3), (10, 3) }), 99, default);   // 6 > 5

        AssertError(res, TipoError.Conflicto, CodigosError.StockInsuficiente);
        Assert.Equal(5, _teclado.CantidadStock);
    }

    [Fact]
    public async Task Registrar_Exitoso_GuardaUnaVez_AsignaUsuario_CopiaPrecios_YPublicaUnEvento()
    {
        var res = await Registrar().EjecutarAsync(Crear(comentarios: " dejar con el vigilante ", lineas: new[] { (10, 2), (20, 4) }), 77, default);

        Assert.True(res.Exito);
        Assert.Equal(1, _uow.Guardados);
        Assert.Equal(77, res.Valor!.UsuarioId);
        Assert.Equal("dejar con el vigilante", res.Valor.Comentarios);
        Assert.Equal(EstadoPedido.Pendiente, res.Valor.Estado);
        Assert.Equal(200m, res.Valor.Total);   // 2*50 + 4*25
        Assert.All(res.Valor.Lineas, l => Assert.Equal(l.Cantidad * l.PrecioUnitario, l.Subtotal));
        var ev = Assert.Single(_publicados);
        Assert.Equal(TipoCambioServidor.Registrada, ev.Tipo);
        Assert.Equal(77, ev.UsuarioId);
        Assert.Contains("Carlos", ev.Detalle);
        Assert.Contains("200.00", ev.Detalle);
    }

    [Fact]
    public async Task Registrar_ConConflictoDeConcurrencia_DevuelveConflicto_YNoPublicaEvento()
    {
        var res = await Registrar(new UnidadDeTrabajoConConflicto()).EjecutarAsync(Crear(lineas: (10, 1)), 99, default);

        AssertError(res, TipoError.Conflicto, CodigosError.Conflicto);
        Assert.Empty(_publicados);
    }

    // ===================== ActualizarPedido =====================

    [Fact]
    public async Task Actualizar_Exitoso_AjustaElStockPorDiferencia_YPublicaUnEvento()
    {
        var p = PedidoExistente(100, (_teclado, 2), (_mouse, 1));   // teclado 3, mouse 9
        var eventosAntes = _publicados.Count;

        var res = await Actualizar().EjecutarAsync(100, Editar(comentarios: "nuevo", lineas: new[] { (10, 4), (20, 1) }), 99, default);

        Assert.True(res.Exito);
        Assert.Equal(1, _teclado.CantidadStock);   // faltaban 2 más
        Assert.Equal(9, _mouse.CantidadStock);     // sin cambio
        Assert.Equal(225m, res.Valor!.Total);
        Assert.Equal(1, _uow.Guardados);
        var ev = Assert.Single(_publicados.Skip(eventosAntes));
        Assert.Equal(TipoCambioServidor.Actualizada, ev.Tipo);
        Assert.Equal(p.Id, ev.EntidadId);
    }

    [Fact]
    public async Task Actualizar_QuitarUnProductoYAgregarOtro_ReponeYDescuenta()
    {
        PedidoExistente(100, (_teclado, 2));   // teclado 3

        var res = await Actualizar().EjecutarAsync(100, Editar(lineas: (20, 3)), 99, default);

        Assert.True(res.Exito);
        Assert.Equal(5, _teclado.CantidadStock);   // repuestas las 2
        Assert.Equal(7, _mouse.CantidadStock);
        Assert.Equal(75m, res.Valor!.Total);
    }

    [Fact]
    public async Task Actualizar_ConservaElPrecioHistoricoDeLasLineasExistentes()
    {
        PedidoExistente(100, (_teclado, 1));
        _teclado.PrecioVenta = 999m;

        var res = await Actualizar().EjecutarAsync(100, Editar(lineas: (10, 2)), 99, default);

        Assert.True(res.Exito);
        Assert.Equal(50m, res.Valor!.Lineas.Single().PrecioUnitario);
    }

    [Fact]
    public async Task Actualizar_PedidoInexistente_NoEncontrado() =>
        AssertError(await Actualizar().EjecutarAsync(999, Editar(lineas: (10, 1)), 99, default), TipoError.NoEncontrado);

    [Fact]
    public async Task Actualizar_PedidoEntregado_Regla_SinEvento()
    {
        var p = PedidoExistente(100, (_teclado, 1));
        p.MarcarEntregado();

        var res = await Actualizar().EjecutarAsync(100, Editar(lineas: (10, 1)), 99, default);

        AssertError(res, TipoError.Regla);
        Assert.Empty(_publicados);
    }

    [Fact]
    public async Task Actualizar_PedidoDadoDeBaja_Regla()
    {
        var p = PedidoExistente(100, (_teclado, 1));
        p.Desactivar(Ahora);

        AssertError(await Actualizar().EjecutarAsync(100, Editar(lineas: (10, 1)), 99, default), TipoError.Regla);
    }

    [Fact]
    public async Task Actualizar_SinLineas_Validacion() =>
        AssertError(await Actualizar().EjecutarAsync(100, Editar(), 99, default), TipoError.Validacion);

    [Fact]
    public async Task Actualizar_FechaAnteriorAHoy_Validacion()
    {
        PedidoExistente(100, (_teclado, 1));
        AssertError(await Actualizar().EjecutarAsync(100, Editar(entrega: Hoy.AddDays(-1), lineas: (10, 1)), 99, default), TipoError.Validacion);
    }

    [Fact]
    public async Task Actualizar_ProductoInexistente_Validacion()
    {
        PedidoExistente(100, (_teclado, 1));
        AssertError(await Actualizar().EjecutarAsync(100, Editar(lineas: (999, 1)), 99, default), TipoError.Validacion);
    }

    [Fact]
    public async Task Actualizar_IncrementoMayorAlStock_Conflicto_SinCambiarNada()
    {
        PedidoExistente(100, (_teclado, 2));   // quedan 3

        var res = await Actualizar().EjecutarAsync(100, Editar(lineas: (10, 6)), 99, default);   // faltan 4, hay 3

        AssertError(res, TipoError.Conflicto, CodigosError.StockInsuficiente);
        Assert.Equal(3, _teclado.CantidadStock);
        Assert.Equal(0, _uow.Guardados);
        Assert.Empty(_publicados);
    }

    [Fact]
    public async Task Actualizar_ConConflictoDeConcurrencia_Conflicto_SinEvento()
    {
        PedidoExistente(100, (_teclado, 1));

        var res = await Actualizar(new UnidadDeTrabajoConConflicto()).EjecutarAsync(100, Editar(lineas: (10, 2)), 99, default);

        AssertError(res, TipoError.Conflicto, CodigosError.Conflicto);
        Assert.Empty(_publicados);
    }

    // ---------- Pendientes de la revisión (rojas hasta corregirlas) ----------

    /// <summary>
    /// Revisión del PR #2 (<c>RegistrarPedido.cs:45</c>): pasadas las 6 p. m. de El Salvador la fecha UTC ya es la del día siguiente
    /// y el dominio rechazaba una entrega "hoy" con una excepción sin capturar (500). 19:00 del 9-nov = 01:00 UTC del 10-nov.
    /// </summary>
    [Fact]
    public async Task Registrar_EntregaHoyEnElSalvador_DespuesDeLas6pm_DebeRegistrar()
    {
        var reloj = new FakeTimeProvider(new DateTimeOffset(2026, 11, 10, 1, 0, 0, TimeSpan.Zero));

        var res = await Registrar(reloj: reloj).EjecutarAsync(Crear(entrega: new DateOnly(2026, 11, 9), lineas: (10, 1)), 99, default);

        Assert.True(res.Exito);
    }

    /// <summary>Revisión del PR #2 (<c>ActualizarPedido.cs:57</c>): la versión es obligatoria al editar.</summary>
    [Fact]
    public async Task Actualizar_SinRowVersion_DebeSerValidacion()
    {
        PedidoExistente(100, (_teclado, 1));

        var res = await Actualizar().EjecutarAsync(100, Editar(version: Array.Empty<byte>(), lineas: (10, 1)), 99, default);

        AssertError(res, TipoError.Validacion);
    }

    // ===================== CambiarEstadoPedido =====================

    [Fact]
    public async Task CambiarEstado_PedidoInexistente_NoEncontrado() =>
        AssertError(await CambiarEstado().EjecutarAsync(999, new CambiarEstadoPedidoSolicitud(EstadoPedido.Entregado, Version), 99, default), TipoError.NoEncontrado);

    [Fact]
    public async Task CambiarEstado_EstadoInvalido_Validacion_SinGuardar()
    {
        PedidoExistente(100, (_teclado, 1));

        var res = await CambiarEstado().EjecutarAsync(100, new CambiarEstadoPedidoSolicitud("Pendiente", Version), 99, default);

        AssertError(res, TipoError.Validacion);
        Assert.Equal(0, _uow.Guardados);
    }

    [Theory]
    [InlineData(EstadoPedido.Entregado)]
    [InlineData(EstadoPedido.Cancelado)]
    public async Task CambiarEstado_DesdeUnEstadoFinal_Regla_SinEvento_YSinTocarElStock(string nuevo)
    {
        var p = PedidoExistente(100, (_teclado, 2));
        p.MarcarEntregado();
        var eventosAntes = _publicados.Count;

        var res = await CambiarEstado().EjecutarAsync(100, new CambiarEstadoPedidoSolicitud(nuevo, Version), 99, default);

        AssertError(res, TipoError.Regla);
        Assert.Equal(3, _teclado.CantidadStock);
        Assert.Equal(eventosAntes, _publicados.Count);
    }

    [Fact]
    public async Task CambiarEstado_Entregado_NoToca_ElStock_YPublicaUnEvento()
    {
        PedidoExistente(100, (_teclado, 2));
        var eventosAntes = _publicados.Count;

        var res = await CambiarEstado().EjecutarAsync(100, new CambiarEstadoPedidoSolicitud(EstadoPedido.Entregado, Version), 99, default);

        Assert.True(res.Exito);
        Assert.Equal(3, _teclado.CantidadStock);
        var ev = Assert.Single(_publicados.Skip(eventosAntes));
        Assert.Equal(TipoCambioServidor.EstadoCambiado, ev.Tipo);
        Assert.Contains("entregado", ev.Detalle);
    }

    [Fact]
    public async Task CambiarEstado_Cancelado_PublicaEventoConStockRepuesto()
    {
        PedidoExistente(100, (_teclado, 2));
        var eventosAntes = _publicados.Count;

        var res = await CambiarEstado().EjecutarAsync(100, new CambiarEstadoPedidoSolicitud(EstadoPedido.Cancelado, Version), 99, default);

        Assert.True(res.Exito);
        var ev = Assert.Single(_publicados.Skip(eventosAntes));
        Assert.Contains("stock repuesto", ev.Detalle);
    }

    [Fact]
    public async Task CambiarEstado_ConConflictoDeConcurrencia_Conflicto_SinEvento()
    {
        PedidoExistente(100, (_teclado, 2));
        var eventosAntes = _publicados.Count;

        var res = await CambiarEstado(new UnidadDeTrabajoConConflicto()).EjecutarAsync(100, new CambiarEstadoPedidoSolicitud(EstadoPedido.Entregado, Version), 99, default);

        AssertError(res, TipoError.Conflicto, CodigosError.Conflicto);
        Assert.Equal(eventosAntes, _publicados.Count);
    }

    // ===================== DesactivarPedido =====================

    [Fact]
    public async Task Desactivar_PedidoInexistente_NoEncontrado() =>
        AssertError(await Desactivar().EjecutarAsync(999, 99, default), TipoError.NoEncontrado);

    [Fact]
    public async Task Desactivar_PedidoEntregado_SoloMarcaInactivo_SinReponerStock()
    {
        var p = PedidoExistente(100, (_teclado, 2));
        p.MarcarEntregado();

        var res = await Desactivar().EjecutarAsync(100, 99, default);

        Assert.True(res.Exito);
        Assert.False(p.Activo);
        Assert.Equal(EstadoPedido.Entregado, p.Estado);
        Assert.Equal(3, _teclado.CantidadStock);
    }

    [Fact]
    public async Task Desactivar_YaInactivo_EsOk_SinGuardarNiPublicar()
    {
        var p = PedidoExistente(100, (_teclado, 2));
        p.Desactivar(Ahora);
        var guardadosAntes = _uow.Guardados;
        var eventosAntes = _publicados.Count;

        var res = await Desactivar().EjecutarAsync(100, 99, default);

        Assert.True(res.Exito);
        Assert.Equal(guardadosAntes, _uow.Guardados);
        Assert.Equal(eventosAntes, _publicados.Count);
    }

    [Fact]
    public async Task Desactivar_Pendiente_PublicaUnEvento_ConElNumeroDePedido()
    {
        PedidoExistente(100, (_teclado, 2));
        var eventosAntes = _publicados.Count;

        await Desactivar().EjecutarAsync(100, 99, default);

        var ev = Assert.Single(_publicados.Skip(eventosAntes));
        Assert.Equal(100, ev.EntidadId);
        Assert.Contains("#100", ev.Detalle);
    }

    [Fact]
    public async Task Desactivar_ConConflictoDeConcurrencia_Conflicto_SinEvento()
    {
        PedidoExistente(100, (_teclado, 2));
        var eventosAntes = _publicados.Count;

        var res = await Desactivar(new UnidadDeTrabajoConConflicto()).EjecutarAsync(100, 99, default);

        AssertError(res, TipoError.Conflicto, CodigosError.Conflicto);
        Assert.Equal(eventosAntes, _publicados.Count);
    }

    // ===================== ObtenerPedido y BuscarPedidos =====================

    [Fact]
    public async Task Obtener_PedidoExistente_DevuelveCabeceraYLineas()
    {
        PedidoExistente(100, (_teclado, 2), (_mouse, 1));

        var res = await new ObtenerPedido(_repo).EjecutarAsync(100, default);

        Assert.True(res.Exito);
        Assert.Equal(100, res.Valor!.Id);
        Assert.Equal(2, res.Valor.Lineas.Count);
        Assert.Equal("Carlos López", res.Valor.Cliente);
    }

    [Fact]
    public async Task Obtener_PedidoInexistente_NoEncontrado() =>
        AssertError(await new ObtenerPedido(_repo).EjecutarAsync(999, default), TipoError.NoEncontrado);

    [Fact]
    public async Task Buscar_ExcluyeInactivosPorDefecto_Y_LosIncluyeSiSePide()
    {
        PedidoExistente(100, (_teclado, 1));
        var baja = PedidoExistente(101, (_mouse, 1));
        baja.Desactivar(Ahora);

        var buscar = new BuscarPedidos(_repo);
        var porDefecto = await buscar.EjecutarAsync(new FiltroPedidosSolicitud(), default);
        var todos = await buscar.EjecutarAsync(new FiltroPedidosSolicitud(IncluirInactivos: true), default);

        Assert.Equal(1, porDefecto.Valor!.Total);
        Assert.Equal(2, todos.Valor!.Total);
    }

    [Fact]
    public async Task Buscar_FiltraPorEstado()
    {
        var p = PedidoExistente(100, (_teclado, 1));
        p.MarcarEntregado();
        PedidoExistente(101, (_mouse, 1));

        var res = await new BuscarPedidos(_repo).EjecutarAsync(new FiltroPedidosSolicitud(Estado: EstadoPedido.Entregado), default);

        Assert.Equal(1, res.Valor!.Total);
        Assert.Equal(100, res.Valor.Elementos.Single().Id);
    }
}
