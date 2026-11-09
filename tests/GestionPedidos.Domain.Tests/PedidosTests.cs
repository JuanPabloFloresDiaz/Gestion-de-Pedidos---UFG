using GestionPedidos.Domain.Pedidos;
using GestionPedidos.Domain.Productos;

namespace GestionPedidos.Domain.Tests;

public class PedidosTests
{
    private static readonly DateTimeOffset FechaPedido = new(2026, 11, 8, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly FechaEntrega = new(2026, 11, 10);

    private static Producto Producto(int id, string nombre, decimal precio, int stock) => new()
    {
        Id = id,
        Codigo = $"PRD-{id:000000}",
        Nombre = nombre,
        PrecioVenta = precio,
        CantidadStock = stock,
        Activo = true
    };

    [Fact]
    public void Crear_InicializaEnPendienteYActivo()
    {
        var p = Pedido.Crear(1, 2, FechaPedido, FechaEntrega, "Comentario de prueba");

        Assert.Equal(1, p.ClienteId);
        Assert.Equal(2, p.UsuarioId);
        Assert.Equal(FechaPedido, p.FechaPedido);
        Assert.Equal(FechaEntrega, p.FechaEntrega);
        Assert.Equal("Comentario de prueba", p.Comentarios);
        Assert.Equal(EstadoPedido.Pendiente, p.Estado);
        Assert.True(p.Activo);
        Assert.Null(p.EliminadoEn);
        Assert.Empty(p.Detalles);
        Assert.Equal(0m, p.Total);
    }

    [Fact]
    public void Crear_FechaEntregaAnterior_LanzaExcepcion()
    {
        var anterior = new DateOnly(2026, 11, 7);
        Assert.Throws<ArgumentException>(() => Pedido.Crear(1, 2, FechaPedido, anterior, null));
    }

    [Fact]
    public void AgregarLinea_DescuentaStockYCalculaTotal()
    {
        var p = Pedido.Crear(1, 2, FechaPedido, FechaEntrega, null);
        var prod1 = Producto(1, "Camisa", 25.00m, 10);
        var prod2 = Producto(2, "Pantalón", 40.00m, 5);

        p.AgregarLinea(prod1, 2);
        p.AgregarLinea(prod2, 1);

        Assert.Equal(8, prod1.CantidadStock);
        Assert.Equal(4, prod2.CantidadStock);
        Assert.Equal(2, p.Detalles.Count);
        Assert.Equal(90.00m, p.Total); // (2 * 25) + (1 * 40)
    }

    [Fact]
    public void AgregarLinea_ProductoDuplicado_LanzaExcepcion()
    {
        var p = Pedido.Crear(1, 2, FechaPedido, FechaEntrega, null);
        var prod = Producto(1, "Camisa", 25.00m, 10);

        p.AgregarLinea(prod, 2);
        Assert.Throws<InvalidOperationException>(() => p.AgregarLinea(prod, 1));
    }

    [Fact]
    public void AgregarLinea_StockInsuficiente_LanzaExcepcionYNoModificaStock()
    {
        var p = Pedido.Crear(1, 2, FechaPedido, FechaEntrega, null);
        var prod = Producto(1, "Camisa", 25.00m, 2);

        var ex = Assert.Throws<InvalidOperationException>(() => p.AgregarLinea(prod, 5));
        Assert.Contains("Solo quedan 2 unidades", ex.Message);
        Assert.Equal(2, prod.CantidadStock);
        Assert.Empty(p.Detalles);
        Assert.Equal(0m, p.Total);
    }

    [Fact]
    public void MarcarEntregado_CambiaEstadoAFinal()
    {
        var p = Pedido.Crear(1, 2, FechaPedido, FechaEntrega, null);
        var prod = Producto(1, "Camisa", 20.00m, 5);
        p.AgregarLinea(prod, 2);

        p.MarcarEntregado();

        Assert.Equal(EstadoPedido.Entregado, p.Estado);
        Assert.Equal(3, prod.CantidadStock); // stock no cambia al entregar
    }

    [Fact]
    public void Cancelar_ReponeStockDeTodasLasLineas()
    {
        var p = Pedido.Crear(1, 2, FechaPedido, FechaEntrega, null);
        var prod1 = Producto(1, "Camisa", 20.00m, 10);
        var prod2 = Producto(2, "Zapatos", 50.00m, 5);

        p.AgregarLinea(prod1, 3);
        p.AgregarLinea(prod2, 2);

        Assert.Equal(7, prod1.CantidadStock);
        Assert.Equal(3, prod2.CantidadStock);

        p.Cancelar();

        Assert.Equal(EstadoPedido.Cancelado, p.Estado);
        Assert.Equal(10, prod1.CantidadStock); // repuesto
        Assert.Equal(5, prod2.CantidadStock);  // repuesto
    }

    [Fact]
    public void TransicionesDesdeEstadosFinales_LanzanExcepcion()
    {
        var p1 = Pedido.Crear(1, 2, FechaPedido, FechaEntrega, null);
        p1.MarcarEntregado();
        Assert.Throws<InvalidOperationException>(() => p1.MarcarEntregado());
        Assert.Throws<InvalidOperationException>(() => p1.Cancelar());

        var p2 = Pedido.Crear(1, 2, FechaPedido, FechaEntrega, null);
        p2.Cancelar();
        Assert.Throws<InvalidOperationException>(() => p2.MarcarEntregado());
        Assert.Throws<InvalidOperationException>(() => p2.Cancelar());
    }

    [Fact]
    public void Desactivar_PedidoPendiente_CancelaYReponeStock()
    {
        var p = Pedido.Crear(1, 2, FechaPedido, FechaEntrega, null);
        var prod = Producto(1, "Camisa", 20.00m, 5);
        p.AgregarLinea(prod, 2);

        var ahoraBaja = FechaPedido.AddHours(2);
        p.Desactivar(ahoraBaja);

        Assert.False(p.Activo);
        Assert.Equal(ahoraBaja, p.EliminadoEn);
        Assert.Equal(EstadoPedido.Cancelado, p.Estado);
        Assert.Equal(5, prod.CantidadStock); // stock repuesto
    }

    [Fact]
    public void Desactivar_PedidoEntregado_SoloMarcaInactivoSinAlterarStock()
    {
        var p = Pedido.Crear(1, 2, FechaPedido, FechaEntrega, null);
        var prod = Producto(1, "Camisa", 20.00m, 5);
        p.AgregarLinea(prod, 2);
        p.MarcarEntregado();

        var ahoraBaja = FechaPedido.AddHours(3);
        p.Desactivar(ahoraBaja);

        Assert.False(p.Activo);
        Assert.Equal(ahoraBaja, p.EliminadoEn);
        Assert.Equal(EstadoPedido.Entregado, p.Estado);
        Assert.Equal(3, prod.CantidadStock); // no se altera
    }

    [Fact]
    public void ActualizarLineas_AjustaStockPorDiferenciaYConservaPreciosHistoricos()
    {
        var p = Pedido.Crear(1, 2, FechaPedido, FechaEntrega, null);
        var prod1 = Producto(1, "Camisa", 20.00m, 10);
        var prod2 = Producto(2, "Pantalón", 40.00m, 10);
        var prod3 = Producto(3, "Gorra", 15.00m, 10);

        p.AgregarLinea(prod1, 3); // stock queda en 7
        p.AgregarLinea(prod2, 2); // stock queda en 8

        // Simulamos que el precio de prod1 subió en el catálogo a 35.00m
        prod1.PrecioVenta = 35.00m;

        // Actualizamos líneas:
        // prod1 aumenta a 5 (diferencia +2, debe restar 2 a stock y conservar precio 20.00)
        // prod2 se elimina (debe reponer sus 2 unidades)
        // prod3 es nuevo con cantidad 4 (debe restar 4 de stock con precio 15.00)
        var nuevas = new[]
        {
            (prod1, 5),
            (prod3, 4)
        };

        p.ActualizarLineas(nuevas);

        Assert.Equal(5, prod1.CantidadStock);  // 7 - 2 = 5
        Assert.Equal(10, prod2.CantidadStock); // 8 + 2 = 10 (repuesto)
        Assert.Equal(6, prod3.CantidadStock);  // 10 - 4 = 6

        Assert.Equal(2, p.Detalles.Count);
        var linea1 = p.Detalles.First(d => d.ProductoId == 1);
        Assert.Equal(5, linea1.Cantidad);
        Assert.Equal(20.00m, linea1.PrecioUnitario); // CONSERVA el precio histórico
        Assert.Equal(100.00m, linea1.Subtotal);

        var linea3 = p.Detalles.First(d => d.ProductoId == 3);
        Assert.Equal(4, linea3.Cantidad);
        Assert.Equal(15.00m, linea3.PrecioUnitario); // precio nuevo
        Assert.Equal(60.00m, linea3.Subtotal);

        Assert.Equal(160.00m, p.Total);
    }
}
