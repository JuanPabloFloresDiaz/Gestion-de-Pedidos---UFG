using GestionPedidos.Domain.Pedidos;
using GestionPedidos.Domain.Productos;

namespace GestionPedidos.Domain.Tests;

/// <summary>
/// Reglas del agregado <see cref="Pedido"/> que complementan a <see cref="PedidosTests"/>:
/// validaciones de cabecera, de líneas, edición, reposición de stock y estados finales ([Pedidos.md §3]).
/// </summary>
public class PedidosReglasTests
{
    private static readonly DateTimeOffset FechaPedido = new(2026, 11, 8, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly FechaEntrega = new(2026, 11, 10);

    private static Producto Producto(int id, decimal precio, int stock, bool activo = true) => new()
    {
        Id = id,
        Codigo = $"PRD-{id:000000}",
        Nombre = $"Producto {id}",
        PrecioVenta = precio,
        CantidadStock = stock,
        Activo = activo
    };

    private static Pedido PedidoConLineas(params (Producto Producto, int Cantidad)[] lineas)
    {
        var pedido = Pedido.Crear(1, 2, FechaPedido, FechaEntrega, null);
        foreach (var (producto, cantidad) in lineas) pedido.AgregarLinea(producto, cantidad);
        return pedido;
    }

    // ---------- Crear ----------

    [Theory]
    [InlineData(0, 2)]
    [InlineData(-1, 2)]
    public void Crear_ClienteInvalido_LanzaFueraDeRango(int clienteId, int usuarioId) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Pedido.Crear(clienteId, usuarioId, FechaPedido, FechaEntrega, null));

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, -5)]
    public void Crear_UsuarioInvalido_LanzaFueraDeRango(int clienteId, int usuarioId) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Pedido.Crear(clienteId, usuarioId, FechaPedido, FechaEntrega, null));

    [Fact]
    public void Crear_EntregaElMismoDiaDelPedido_EsValida()
    {
        var p = Pedido.Crear(1, 2, FechaPedido, new DateOnly(2026, 11, 8), null);
        Assert.Equal(new DateOnly(2026, 11, 8), p.FechaEntrega);
    }

    [Fact]
    public void Crear_Comentarios500Caracteres_EsValido_Y501Lanza()
    {
        Assert.NotNull(Pedido.Crear(1, 2, FechaPedido, FechaEntrega, new string('x', 500)));
        Assert.Throws<ArgumentException>(() => Pedido.Crear(1, 2, FechaPedido, FechaEntrega, new string('x', 501)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    public void Crear_ComentariosVacios_QuedanNulos(string? comentarios) =>
        Assert.Null(Pedido.Crear(1, 2, FechaPedido, FechaEntrega, comentarios).Comentarios);

    [Fact]
    public void Crear_ComentariosConEspacios_SeRecortan() =>
        Assert.Equal("hola", Pedido.Crear(1, 2, FechaPedido, FechaEntrega, "  hola  ").Comentarios);

    // ---------- AgregarLinea ----------

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void AgregarLinea_CantidadNoPositiva_Lanza(int cantidad)
    {
        var pedido = Pedido.Crear(1, 2, FechaPedido, FechaEntrega, null);
        var producto = Producto(10, 5m, 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => pedido.AgregarLinea(producto, cantidad));
        Assert.Equal(10, producto.CantidadStock);
        Assert.Empty(pedido.Detalles);
    }

    [Fact]
    public void AgregarLinea_ProductoInactivo_LanzaYNoTocaElStock()
    {
        var pedido = Pedido.Crear(1, 2, FechaPedido, FechaEntrega, null);
        var inactivo = Producto(10, 5m, 10, activo: false);

        Assert.Throws<InvalidOperationException>(() => pedido.AgregarLinea(inactivo, 1));
        Assert.Equal(10, inactivo.CantidadStock);
    }

    [Fact]
    public void AgregarLinea_ProductoNulo_Lanza() =>
        Assert.Throws<ArgumentNullException>(() => Pedido.Crear(1, 2, FechaPedido, FechaEntrega, null).AgregarLinea(null!, 1));

    [Fact]
    public void AgregarLinea_PedidoEntregado_Lanza()
    {
        var pedido = PedidoConLineas((Producto(10, 5m, 10), 1));
        pedido.MarcarEntregado();

        Assert.Throws<InvalidOperationException>(() => pedido.AgregarLinea(Producto(20, 5m, 10), 1));
    }

    [Fact]
    public void AgregarLinea_CopiaElPrecioVigente_YCambiosPosterioresDelProductoNoAfectanLaLinea()
    {
        var producto = Producto(10, 50m, 10);
        var pedido = PedidoConLineas((producto, 2));

        producto.PrecioVenta = 80m;
        pedido.RecalcularTotal();

        Assert.Equal(50m, pedido.Detalles.Single().PrecioUnitario);
        Assert.Equal(100m, pedido.Total);
    }

    // ---------- ActualizarCabecera ----------

    [Fact]
    public void ActualizarCabecera_CambiaFechaYComentarios()
    {
        var pedido = PedidoConLineas((Producto(10, 5m, 10), 1));

        pedido.ActualizarCabecera(new DateOnly(2026, 11, 20), "  nuevo  ");

        Assert.Equal(new DateOnly(2026, 11, 20), pedido.FechaEntrega);
        Assert.Equal("nuevo", pedido.Comentarios);
    }

    [Fact]
    public void ActualizarCabecera_FechaAnteriorAlPedido_Lanza_YNoCambiaNada()
    {
        var pedido = PedidoConLineas((Producto(10, 5m, 10), 1));

        Assert.Throws<ArgumentException>(() => pedido.ActualizarCabecera(new DateOnly(2026, 11, 1), "x"));
        Assert.Equal(FechaEntrega, pedido.FechaEntrega);
    }

    [Fact]
    public void ActualizarCabecera_Comentarios501_Lanza() =>
        Assert.Throws<ArgumentException>(() => PedidoConLineas((Producto(10, 5m, 10), 1)).ActualizarCabecera(FechaEntrega, new string('x', 501)));

    [Fact]
    public void ActualizarCabecera_PedidoCancelado_Lanza()
    {
        var pedido = PedidoConLineas((Producto(10, 5m, 10), 1));
        pedido.Cancelar();

        Assert.Throws<InvalidOperationException>(() => pedido.ActualizarCabecera(FechaEntrega, "x"));
    }

    // ---------- ActualizarLineas ----------

    [Fact]
    public void ActualizarLineas_QuitarUnaLinea_ReponeSuStock()
    {
        var a = Producto(10, 10m, 10);
        var b = Producto(20, 20m, 10);
        var pedido = PedidoConLineas((a, 3), (b, 4));   // a: 7, b: 6

        pedido.ActualizarLineas(new[] { (a, 3) });

        Assert.Equal(7, a.CantidadStock);
        Assert.Equal(10, b.CantidadStock);   // se repusieron las 4 unidades de b
        Assert.Single(pedido.Detalles);
        Assert.Equal(30m, pedido.Total);
    }

    [Fact]
    public void ActualizarLineas_AgregarProductoNuevo_DescuentaStockYTomaElPrecioVigente()
    {
        var a = Producto(10, 10m, 10);
        var b = Producto(20, 20m, 10);
        var pedido = PedidoConLineas((a, 1));

        pedido.ActualizarLineas(new[] { (a, 1), (b, 2) });

        Assert.Equal(8, b.CantidadStock);
        Assert.Equal(20m, pedido.Detalles.Single(d => d.ProductoId == 20).PrecioUnitario);
        Assert.Equal(50m, pedido.Total);
    }

    [Fact]
    public void ActualizarLineas_BajarCantidad_ReponeLaDiferencia()
    {
        var a = Producto(10, 10m, 10);
        var pedido = PedidoConLineas((a, 5));   // stock 5

        pedido.ActualizarLineas(new[] { (a, 2) });

        Assert.Equal(8, a.CantidadStock);
        Assert.Equal(20m, pedido.Total);
    }

    [Fact]
    public void ActualizarLineas_ConservaElPrecioHistoricoAunqueElProductoCambieDePrecio()
    {
        var a = Producto(10, 10m, 10);
        var pedido = PedidoConLineas((a, 1));
        a.PrecioVenta = 99m;

        pedido.ActualizarLineas(new[] { (a, 3) });

        Assert.Equal(10m, pedido.Detalles.Single().PrecioUnitario);
        Assert.Equal(30m, pedido.Total);
    }

    [Fact]
    public void ActualizarLineas_SinLineas_Lanza()
    {
        var pedido = PedidoConLineas((Producto(10, 10m, 10), 1));
        Assert.Throws<ArgumentException>(() => pedido.ActualizarLineas(Array.Empty<(Producto, int)>()));
    }

    [Fact]
    public void ActualizarLineas_CantidadNoPositiva_Lanza()
    {
        var a = Producto(10, 10m, 10);
        var pedido = PedidoConLineas((a, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => pedido.ActualizarLineas(new[] { (a, 0) }));
    }

    [Fact]
    public void ActualizarLineas_ProductoRepetido_Lanza()
    {
        var a = Producto(10, 10m, 10);
        var pedido = PedidoConLineas((a, 1));
        Assert.Throws<ArgumentException>(() => pedido.ActualizarLineas(new[] { (a, 1), (a, 2) }));
    }

    [Fact]
    public void ActualizarLineas_ProductoInactivo_Lanza()
    {
        var a = Producto(10, 10m, 10);
        var inactivo = Producto(20, 5m, 10, activo: false);
        var pedido = PedidoConLineas((a, 1));
        Assert.Throws<InvalidOperationException>(() => pedido.ActualizarLineas(new[] { (a, 1), (inactivo, 1) }));
    }

    [Fact]
    public void ActualizarLineas_SubirMasDeLoDisponible_Lanza()
    {
        var a = Producto(10, 10m, 5);
        var pedido = PedidoConLineas((a, 2));   // quedan 3

        Assert.Throws<InvalidOperationException>(() => pedido.ActualizarLineas(new[] { (a, 6) }));   // faltan 4, solo hay 3
    }

    // ---------- Estados ----------

    [Fact]
    public void MarcarEntregado_NoCambiaElStock()
    {
        var a = Producto(10, 10m, 10);
        var pedido = PedidoConLineas((a, 4));

        pedido.MarcarEntregado();

        Assert.Equal(6, a.CantidadStock);
        Assert.Equal(EstadoPedido.Entregado, pedido.Estado);
    }

    [Fact]
    public void Cancelar_PedidoDadoDeBaja_Lanza()
    {
        var pedido = PedidoConLineas((Producto(10, 10m, 10), 1));
        pedido.Desactivar(FechaPedido);

        Assert.Throws<InvalidOperationException>(pedido.Cancelar);
        Assert.Throws<InvalidOperationException>(pedido.MarcarEntregado);
    }

    [Fact]
    public void Cancelar_DosVeces_Lanza_YNoReponeElStockDosVeces()
    {
        var a = Producto(10, 10m, 10);
        var pedido = PedidoConLineas((a, 4));
        pedido.Cancelar();

        Assert.Throws<InvalidOperationException>(pedido.Cancelar);
        Assert.Equal(10, a.CantidadStock);
    }

    [Fact]
    public void Desactivar_YaInactivo_NoHaceNadaNiCambiaLaFechaDeBaja()
    {
        var pedido = PedidoConLineas((Producto(10, 10m, 10), 1));
        var primera = FechaPedido.AddDays(1);
        pedido.Desactivar(primera);

        pedido.Desactivar(FechaPedido.AddDays(9));

        Assert.False(pedido.Activo);
        Assert.Equal(primera, pedido.EliminadoEn);
    }

    [Fact]
    public void Desactivar_PedidoCancelado_SoloMarcaInactivoSinReponerOtraVez()
    {
        var a = Producto(10, 10m, 10);
        var pedido = PedidoConLineas((a, 4));
        pedido.Cancelar();

        pedido.Desactivar(FechaPedido);

        Assert.Equal(10, a.CantidadStock);
        Assert.False(pedido.Activo);
        Assert.Equal(EstadoPedido.Cancelado, pedido.Estado);
    }

    [Fact]
    public void Desactivar_GuardaLaFechaDeBaja()
    {
        var pedido = PedidoConLineas((Producto(10, 10m, 10), 1));
        pedido.Desactivar(FechaPedido.AddHours(3));
        Assert.Equal(FechaPedido.AddHours(3), pedido.EliminadoEn);
    }

    // ---------- Pendientes de la revisión (rojas hasta corregirlas) ----------

    /// <summary>
    /// Revisión del PR #2 (<c>Pedido.cs:220</c>): cancelar con líneas cuyo producto no está cargado no debe pasar el pedido a
    /// <c>Cancelado</c> sin reponer el stock. Debe fallar (o el dominio exigir el producto cargado).
    /// </summary>
    [Fact]
    public void Cancelar_ConLineaSinProductoCargado_NoDebeCancelarEnSilencio()
    {
        var pedido = Pedido.Crear(1, 2, FechaPedido, FechaEntrega, null);
        pedido.Detalles.Add(new DetallePedido { ProductoId = 10, Producto = null, Cantidad = 3, PrecioUnitario = 5m });

        Assert.Throws<InvalidOperationException>(pedido.Cancelar);
        Assert.Equal(EstadoPedido.Pendiente, pedido.Estado);
    }
}
