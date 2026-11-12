using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Application.Pedidos;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Clientes;
using GestionPedidos.Domain.Pedidos;
using GestionPedidos.Domain.Productos;
using GestionPedidos.Domain.Seguridad;
using GestionPedidos.Infrastructure.Persistence;
using GestionPedidos.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GestionPedidos.Infrastructure.Tests;

/// <summary>
/// Pedidos sobre SQL Server real ([Pedidos.md §3 y §8], [Testing.md §5]): transacción única, atomicidad, concurrencia optimista,
/// líneas huérfanas y búsquedas del repositorio. Complementa a las pruebas de esquema de <see cref="EsquemaTests"/>.
/// </summary>
[Collection(BaseDeDatosCollection.Nombre)]
public class PedidosPersistenciaTests
{
    private readonly BaseDeDatosFixture _bd;
    public PedidosPersistenciaTests(BaseDeDatosFixture bd) => _bd = bd;

    private static string Unico(string prefijo) => $"{prefijo}-{Guid.NewGuid():N}"[..20];
    private static DateOnly Entrega(int dias = 3) => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(dias));

    private async Task<(int Cliente, int Usuario)> ClienteYUsuarioAsync(string? apellidos = null)
    {
        await using var db = _bd.Contexto();
        var n = Unico("pp");
        var u = Usuario.Crear(n, "Operador", $"{n}@e.com", "h", 2, false);
        var c = new Cliente { Nombres = "Ana", Apellidos = apellidos ?? Unico("Ape"), Telefono = "7777-0000", Direccion = "X", CiudadId = 1 };
        db.AddRange(u, c);
        await db.SaveChangesAsync();
        return (c.Id, u.Id);
    }

    private async Task<int> ProductoAsync(int stock, decimal precio = 10m, string? nombre = null)
    {
        await using var db = _bd.Contexto();
        var p = new Producto { Nombre = nombre ?? Unico("Prod"), Descripcion = "d", CantidadStock = stock, PrecioVenta = precio, FechaIngreso = DateOnly.FromDateTime(DateTime.UtcNow) };
        db.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    private static RegistrarPedido Registrar(GestionPedidosDbContext db) =>
        new(new PedidoRepository(db), new UnitOfWork(db), new EventosAplicacion(), TimeProvider.System);

    private async Task<int> RegistrarAsync(int cliente, int usuario, params (int Producto, int Cantidad)[] lineas)
    {
        await using var db = _bd.Contexto();
        var res = await Registrar(db).EjecutarAsync(
            new CrearPedidoSolicitud(cliente, Entrega(), null, lineas.Select(l => new LineaPedidoSolicitud(l.Producto, l.Cantidad)).ToList()), usuario, default);
        Assert.True(res.Exito, res.Error?.Mensaje);
        return res.Valor!.Id;
    }

    private async Task<int> StockAsync(int producto)
    {
        await using var db = _bd.Contexto();
        return await db.Productos.AsNoTracking().Where(p => p.Id == producto).Select(p => p.CantidadStock).SingleAsync();
    }

    // ===================== Transacción y atomicidad =====================

    [Fact]
    public async Task Registrar_GuardaPedidoLineasYStockEnUnaSolaTransaccion_ConElSubtotalCalculadoPorLaBase()
    {
        var (cliente, usuario) = await ClienteYUsuarioAsync();
        var a = await ProductoAsync(10, 12.50m);
        var b = await ProductoAsync(5, 3m);

        var id = await RegistrarAsync(cliente, usuario, (a, 2), (b, 1));

        await using var db = _bd.Contexto();
        var pedido = await db.Pedidos.Include(p => p.Detalles).AsNoTracking().SingleAsync(p => p.Id == id);
        Assert.Equal(28m, pedido.Total);
        Assert.Equal(new[] { 3m, 25m }, pedido.Detalles.Select(d => d.Subtotal).OrderBy(x => x));   // columna calculada
        Assert.Equal(8, await StockAsync(a));
        Assert.Equal(4, await StockAsync(b));
    }

    [Fact]
    public async Task UnaFallaAlGuardar_RevierteTodo_NoQuedaPedidoNiStockDescontado()
    {
        var (cliente, usuario) = await ClienteYUsuarioAsync();
        var producto = await ProductoAsync(10);

        await using (var db = _bd.Contexto())
        {
            var p = await db.Productos.SingleAsync(x => x.Id == producto);
            var pedido = Pedido.Crear(cliente, usuario, DateTimeOffset.UtcNow, Entrega(), null);
            pedido.AgregarLinea(p, 4);          // descuenta 4 en memoria
            pedido.Estado = "Inventado";        // viola CK_Pedidos_Estado: la base rechaza el guardado
            db.Pedidos.Add(pedido);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        Assert.Equal(10, await StockAsync(producto));   // el descuento del producto también se revirtió
        await using var lectura = _bd.Contexto();
        Assert.False(await lectura.Pedidos.AnyAsync(p => p.ClienteId == cliente));
    }

    // ===================== Concurrencia =====================

    [Fact]
    public async Task DosPedidosPorLaUltimaUnidad_UnoGana_ElOtroRecibeConflicto_YElStockNuncaQuedaNegativo()
    {
        var (c1, u1) = await ClienteYUsuarioAsync();
        var (c2, u2) = await ClienteYUsuarioAsync();
        var producto = await ProductoAsync(1);

        // ambos leen el producto con stock 1 antes de que cualquiera guarde
        await using var dbA = _bd.Contexto();
        await using var dbB = _bd.Contexto();
        var repoA = new PedidoRepository(dbA);
        var repoB = new PedidoRepository(dbB);
        var pA = (await repoA.ObtenerProductosAsync(new[] { producto }, default)).Single();
        var pB = (await repoB.ObtenerProductosAsync(new[] { producto }, default)).Single();
        var pedidoA = Pedido.Crear(c1, u1, DateTimeOffset.UtcNow, Entrega(), null); pedidoA.AgregarLinea(pA, 1);
        var pedidoB = Pedido.Crear(c2, u2, DateTimeOffset.UtcNow, Entrega(), null); pedidoB.AgregarLinea(pB, 1);
        repoA.Agregar(pedidoA);
        repoB.Agregar(pedidoB);

        await new UnitOfWork(dbA).GuardarAsync(default);                                          // gana
        await Assert.ThrowsAsync<ConcurrenciaException>(() => new UnitOfWork(dbB).GuardarAsync(default));   // pierde

        Assert.Equal(0, await StockAsync(producto));
        await using var lectura = _bd.Contexto();
        Assert.False(await lectura.Pedidos.AnyAsync(p => p.ClienteId == c2));   // el perdedor no dejó nada
    }

    [Fact]
    public async Task DosCambiosDeEstadoSimultaneos_ElSegundoFalla_YElStockSeRepoeUnaVez()
    {
        var (cliente, usuario) = await ClienteYUsuarioAsync();
        var producto = await ProductoAsync(10);
        var id = await RegistrarAsync(cliente, usuario, (producto, 4));   // stock 6

        await using var dbA = _bd.Contexto();
        await using var dbB = _bd.Contexto();
        var pedidoA = await new PedidoRepository(dbA).ObtenerPorIdAsync(id, default);
        var pedidoB = await new PedidoRepository(dbB).ObtenerPorIdAsync(id, default);
        pedidoA!.Cancelar();
        pedidoB!.Cancelar();

        await new UnitOfWork(dbA).GuardarAsync(default);
        await Assert.ThrowsAsync<ConcurrenciaException>(() => new UnitOfWork(dbB).GuardarAsync(default));

        Assert.Equal(10, await StockAsync(producto));   // 6 + 4, no 14
    }

    [Fact]
    public async Task EditarConUnaVersionVieja_LanzaConcurrencia()
    {
        var (cliente, usuario) = await ClienteYUsuarioAsync();
        var producto = await ProductoAsync(10);
        var id = await RegistrarAsync(cliente, usuario, (producto, 1));

        await using var lectura = _bd.Contexto();
        var versionVieja = (await lectura.Pedidos.AsNoTracking().SingleAsync(p => p.Id == id)).RowVersion;
        await using (var db = _bd.Contexto())   // otra persona edita y guarda primero
        {
            var otro = await new PedidoRepository(db).ObtenerPorIdAsync(id, default);
            otro!.ActualizarCabecera(Entrega(6), "cambio de otra persona");
            await new UnitOfWork(db).GuardarAsync(default);
        }

        await using var mio = _bd.Contexto();
        var repo = new PedidoRepository(mio);
        var pedido = await repo.ObtenerPorIdAsync(id, default);
        repo.FijarVersionOriginal(pedido!, versionVieja);
        pedido!.ActualizarCabecera(Entrega(7), "mi cambio");

        await Assert.ThrowsAsync<ConcurrenciaException>(() => new UnitOfWork(mio).GuardarAsync(default));
    }

    // ===================== Edición y baja =====================

    [Fact]
    public async Task Editar_QuitarUnaLinea_LaBorraDeLaBase_YReponeElStock_ConsistenteConElTotal()
    {
        var (cliente, usuario) = await ClienteYUsuarioAsync();
        var a = await ProductoAsync(10, 10m);
        var b = await ProductoAsync(10, 20m);
        var id = await RegistrarAsync(cliente, usuario, (a, 2), (b, 3));   // a:8 b:7

        await using (var db = _bd.Contexto())
        {
            var repo = new PedidoRepository(db);
            var pedido = (await repo.ObtenerPorIdAsync(id, default))!;
            var productos = (await repo.ObtenerProductosAsync(new[] { a, b }, default)).ToDictionary(p => p.Id);
            pedido.ActualizarLineas(new[] { (productos[a], 2) });   // se quita b
            await new UnitOfWork(db).GuardarAsync(default);
        }

        await using var lectura = _bd.Contexto();
        var guardado = await lectura.Pedidos.Include(p => p.Detalles).AsNoTracking().SingleAsync(p => p.Id == id);
        Assert.Single(guardado.Detalles);                    // la línea quitada ya no existe (sin huérfanas)
        Assert.Equal(20m, guardado.Total);
        Assert.Equal(1, await lectura.DetallesPedido.CountAsync(d => d.PedidoId == id));
        Assert.Equal(10, await StockAsync(b));
    }

    [Fact]
    public async Task Desactivar_PedidoPendiente_GuardaInactivoCanceladoYStockRepuesto_ConFechaDeBaja()
    {
        var (cliente, usuario) = await ClienteYUsuarioAsync();
        var producto = await ProductoAsync(10);
        var id = await RegistrarAsync(cliente, usuario, (producto, 4));

        await using (var db = _bd.Contexto())
        {
            var res = await new DesactivarPedido(new PedidoRepository(db), new UnitOfWork(db), new EventosAplicacion(), TimeProvider.System).EjecutarAsync(id, usuario, default);
            Assert.True(res.Exito);
        }

        await using var lectura = _bd.Contexto();
        var p = await lectura.Pedidos.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.False(p.Activo);
        Assert.Equal(EstadoPedido.Cancelado, p.Estado);
        Assert.NotNull(p.EliminadoEn);
        Assert.Equal(10, await StockAsync(producto));
    }

    // ===================== Búsquedas del repositorio (usp_Pedidos_Buscar) =====================

    [Fact]
    public async Task Buscar_FiltraPorFechasUsuarioEstadoEInactivos_YPagina_ConElTotalCorrecto()
    {
        var apellido = Unico("Bus");
        var (cliente, usuario) = await ClienteYUsuarioAsync(apellido);
        var (_, otroUsuario) = await ClienteYUsuarioAsync();
        var producto = await ProductoAsync(100, 10m);
        var ids = new List<int>();
        for (var i = 0; i < 5; i++) ids.Add(await RegistrarAsync(cliente, i == 0 ? otroUsuario : usuario, (producto, 1)));

        await using (var db = _bd.Contexto())   // uno entregado y uno dado de baja
        {
            var repo = new PedidoRepository(db);
            (await repo.ObtenerPorIdAsync(ids[1], default))!.MarcarEntregado();
            (await repo.ObtenerPorIdAsync(ids[2], default))!.Desactivar(DateTimeOffset.UtcNow);
            await new UnitOfWork(db).GuardarAsync(default);
        }

        await using var q = _bd.Contexto();
        var repoQ = new PedidoRepository(q);
        Task<(IReadOnlyList<PedidoResumenDto> Elementos, int Total)> Buscar(FiltroPedidosSolicitud f) => repoQ.BuscarAsync(f, default);
        var baseFiltro = new FiltroPedidosSolicitud(ClienteId: cliente, Pagina: 1, TamanoPagina: 20);

        Assert.Equal(4, (await Buscar(baseFiltro)).Total);                                                 // sin el dado de baja
        Assert.Equal(5, (await Buscar(baseFiltro with { IncluirInactivos = true })).Total);
        Assert.Equal(new[] { ids[1] }, (await Buscar(baseFiltro with { Estado = EstadoPedido.Entregado })).Elementos.Select(e => e.Id));
        Assert.Equal(new[] { ids[0] }, (await Buscar(baseFiltro with { UsuarioId = otroUsuario })).Elementos.Select(e => e.Id));
        Assert.Equal(4, (await Buscar(baseFiltro with { EntregaDesde = Entrega(0), EntregaHasta = Entrega(10) })).Total);
        Assert.Equal(0, (await Buscar(baseFiltro with { EntregaDesde = Entrega(30) })).Total);
        Assert.Equal(4, (await Buscar(baseFiltro with { PedidoDesde = DateTimeOffset.UtcNow.AddHours(-1), PedidoHasta = DateTimeOffset.UtcNow.AddHours(1) })).Total);
        Assert.Equal(4, (await Buscar(new FiltroPedidosSolicitud(Cliente: apellido, Pagina: 1, TamanoPagina: 20))).Total);   // por apellidos

        var p1 = await Buscar(baseFiltro with { TamanoPagina = 3 });
        var p2 = await Buscar(baseFiltro with { TamanoPagina = 3, Pagina = 2 });
        Assert.Equal(4, p1.Total);                                          // el total no depende de la página
        Assert.Equal(3, p1.Elementos.Count);
        Assert.Single(p2.Elementos);
        Assert.Empty(p1.Elementos.Select(e => e.Id).Intersect(p2.Elementos.Select(e => e.Id)));
        Assert.True(p1.Elementos.Select(e => e.FechaPedido).SequenceEqual(p1.Elementos.Select(e => e.FechaPedido).OrderByDescending(f => f)));   // más recientes primero
    }

    [Fact]
    public async Task Buscar_PorPrecioUnitarioDeAlgunaLinea_YPorProducto_DevuelveLasLineasContadas()
    {
        var (cliente, usuario) = await ClienteYUsuarioAsync();
        var caro = await ProductoAsync(10, 300m, "Caro " + Unico("x"));
        var barato = await ProductoAsync(10, 2m, "Barato " + Unico("x"));
        var mixto = await RegistrarAsync(cliente, usuario, (caro, 1), (barato, 2));
        var soloBarato = await RegistrarAsync(cliente, usuario, (barato, 1));

        await using var db = _bd.Contexto();
        var repo = new PedidoRepository(db);
        var f = new FiltroPedidosSolicitud(ClienteId: cliente, Pagina: 1, TamanoPagina: 20);

        var conCaro = await repo.BuscarAsync(f with { PrecioMin = 100 }, default);
        Assert.Equal(new[] { mixto }, conCaro.Elementos.Select(e => e.Id));
        Assert.Equal(2, conCaro.Elementos.Single().Lineas);

        var porProducto = await repo.BuscarAsync(f with { ProductoId = barato }, default);
        Assert.Equal(2, porProducto.Total);
        Assert.Contains(soloBarato, porProducto.Elementos.Select(e => e.Id));
        Assert.Equal(1, (await repo.BuscarAsync(f with { Producto = "Caro" }, default)).Total);
    }

    [Fact]
    public async Task Selectores_DevuelvenSoloActivos_Ordenados_ConLimite()
    {
        var (activo, _) = await ClienteYUsuarioAsync("Selec" + Unico("a"));
        await using (var db = _bd.Contexto())
        {
            db.Clientes.Add(new Cliente { Nombres = "Baja", Apellidos = "Selec" + Unico("b"), Telefono = "1", Direccion = "x", CiudadId = 1, Activo = false });
            db.Productos.Add(new Producto { Nombre = "SelProd Inactivo " + Unico("z"), Descripcion = "d", CantidadStock = 3, PrecioVenta = 1m, Activo = false, FechaIngreso = DateOnly.FromDateTime(DateTime.UtcNow) });
            await db.SaveChangesAsync();
        }

        await using var lectura = _bd.Contexto();
        var repo = new PedidoRepository(lectura);
        var clientes = await repo.ListarClientesSelectorAsync("Selec", default);
        var productos = await repo.ListarProductosSelectorAsync("SelProd", default);

        Assert.Contains(clientes, c => c.Id == activo);
        Assert.DoesNotContain(clientes, c => c.Nombre.StartsWith("Baja"));
        Assert.DoesNotContain(productos, p => p.Nombre.Contains("Inactivo"));
        Assert.True((await repo.ListarClientesSelectorAsync(null, default)).Count <= 50);
        Assert.True((await repo.ListarProductosSelectorAsync(null, default)).Count <= 100);
    }
}
