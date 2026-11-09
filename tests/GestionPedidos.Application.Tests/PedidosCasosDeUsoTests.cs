using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Application.Pedidos;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Clientes;
using GestionPedidos.Domain.Pedidos;
using GestionPedidos.Domain.Productos;
using Microsoft.Extensions.Time.Testing;

namespace GestionPedidos.Application.Tests;

internal sealed class PedidoRepositoryFake : IPedidoRepository
{
    public List<Pedido> Pedidos { get; } = new();
    public List<Cliente> Clientes { get; } = new();
    public List<Producto> Productos { get; } = new();
    private int _siguienteId = 1;

    public Task<Pedido?> ObtenerPorIdAsync(int id, CancellationToken ct) =>
        Task.FromResult(Pedidos.FirstOrDefault(p => p.Id == id));

    public Task<Cliente?> ObtenerClienteAsync(int clienteId, CancellationToken ct) =>
        Task.FromResult(Clientes.FirstOrDefault(c => c.Id == clienteId));

    public Task<IReadOnlyList<Producto>> ObtenerProductosAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Producto>>(Productos.Where(p => ids.Contains(p.Id)).ToList());

    public void Agregar(Pedido pedido)
    {
        if (pedido.Id == 0) pedido.Id = _siguienteId++;
        Pedidos.Add(pedido);
    }

    public void FijarVersionOriginal(Pedido pedido, byte[] rowVersion) { }

    public Task<(IReadOnlyList<PedidoResumenDto> Elementos, int Total)> BuscarAsync(FiltroPedidosSolicitud filtro, CancellationToken ct)
    {
        var query = Pedidos.AsEnumerable();
        if (filtro.ClienteId.HasValue) query = query.Where(p => p.ClienteId == filtro.ClienteId.Value);
        if (filtro.Estado is not null) query = query.Where(p => p.Estado == filtro.Estado);
        if (filtro.IncluirInactivos != true) query = query.Where(p => p.Activo);

        var lista = query.Select(p => p.AResumenDto()).ToList();
        return Task.FromResult(((IReadOnlyList<PedidoResumenDto>)lista, lista.Count));
    }

    public Task<IReadOnlyList<ClienteSelectorDto>> ListarClientesSelectorAsync(string? q, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ClienteSelectorDto>>(Clientes.Where(c => c.Activo)
            .Select(c => new ClienteSelectorDto(c.Id, $"{c.Nombres} {c.Apellidos}", c.Telefono, c.Direccion)).ToList());

    public Task<IReadOnlyList<ProductoSelectorDto>> ListarProductosSelectorAsync(string? q, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ProductoSelectorDto>>(Productos.Where(p => p.Activo)
            .Select(p => new ProductoSelectorDto(p.Id, p.Codigo, p.Nombre, p.PrecioVenta, p.CantidadStock)).ToList());
}

public class PedidosCasosDeUsoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly FechaEntrega = new(2026, 11, 15);

    private readonly FakeTimeProvider _reloj = new(Ahora);
    private readonly UnitOfWorkFake _uow = new();
    private readonly EventosAplicacion _eventos = new();
    private readonly PedidoRepositoryFake _repo = new();

    public PedidosCasosDeUsoTests()
    {
        _repo.Clientes.Add(new Cliente
        {
            Id = 1,
            Nombres = "Carlos",
            Apellidos = "López",
            Telefono = "7777-8888",
            Direccion = "San Salvador",
            Activo = true
        });

        _repo.Clientes.Add(new Cliente
        {
            Id = 2,
            Nombres = "Inactivo",
            Apellidos = "Pérez",
            Telefono = "7000-0000",
            Direccion = "Santa Ana",
            Activo = false
        });

        _repo.Productos.Add(new Producto
        {
            Id = 10,
            Codigo = "PRD-000010",
            Nombre = "Teclado Mecánico",
            PrecioVenta = 50.00m,
            CantidadStock = 5,
            Activo = true
        });

        _repo.Productos.Add(new Producto
        {
            Id = 20,
            Codigo = "PRD-000020",
            Nombre = "Mouse Inalámbrico",
            PrecioVenta = 25.00m,
            CantidadStock = 10,
            Activo = true
        });
    }

    [Fact]
    public async Task RegistrarPedido_Exitoso_DescuentaStockYPublicaEvento()
    {
        var eventosOcurridos = new List<EntidadCambiadaServidorEventArgs>();
        _eventos.EntidadCambiada += (_, e) => eventosOcurridos.Add(e);

        var caso = new RegistrarPedido(_repo, _uow, _eventos, _reloj);
        var sol = new CrearPedidoSolicitud(
            ClienteId: 1,
            FechaEntrega: FechaEntrega,
            Comentarios: "Entregar por la tarde",
            Lineas: new[]
            {
                new LineaPedidoSolicitud(10, 2),
                new LineaPedidoSolicitud(20, 1)
            });

        var res = await caso.EjecutarAsync(sol, usuarioId: 99, CancellationToken.None);

        Assert.True(res.Exito);
        Assert.NotNull(res.Valor);
        Assert.Equal(125.00m, res.Valor.Total); // (2*50) + (1*25)
        Assert.Equal(3, _repo.Productos.First(p => p.Id == 10).CantidadStock); // 5 - 2 = 3
        Assert.Equal(9, _repo.Productos.First(p => p.Id == 20).CantidadStock); // 10 - 1 = 9
        Assert.Equal(1, _uow.Guardados);

        var ev = Assert.Single(eventosOcurridos);
        Assert.Equal("Pedido", ev.Entidad);
        Assert.Equal(TipoCambioServidor.Registrada, ev.Tipo);
    }

    [Fact]
    public async Task RegistrarPedido_StockInsuficiente_FallaYNoGuarda()
    {
        var caso = new RegistrarPedido(_repo, _uow, _eventos, _reloj);
        var sol = new CrearPedidoSolicitud(
            ClienteId: 1,
            FechaEntrega: FechaEntrega,
            Comentarios: null,
            Lineas: new[] { new LineaPedidoSolicitud(10, 10) }); // solo hay 5

        var res = await caso.EjecutarAsync(sol, usuarioId: 99, CancellationToken.None);

        Assert.False(res.Exito);
        Assert.Equal(TipoError.Conflicto, res.Error!.Tipo);
        Assert.Equal(CodigosError.StockInsuficiente, res.Error!.Codigo);
        Assert.Equal(5, _repo.Productos.First(p => p.Id == 10).CantidadStock); // intacto
        Assert.Equal(0, _uow.Guardados);
    }

    [Fact]
    public async Task RegistrarPedido_ClienteInactivo_Falla()
    {
        var caso = new RegistrarPedido(_repo, _uow, _eventos, _reloj);
        var sol = new CrearPedidoSolicitud(
            ClienteId: 2, // cliente inactivo
            FechaEntrega: FechaEntrega,
            Comentarios: null,
            Lineas: new[] { new LineaPedidoSolicitud(10, 1) });

        var res = await caso.EjecutarAsync(sol, usuarioId: 99, CancellationToken.None);

        Assert.False(res.Exito);
        Assert.Equal(TipoError.Regla, res.Error!.Tipo);
    }

    [Fact]
    public async Task CambiarEstado_Entregado_Exitoso()
    {
        var p = Pedido.Crear(1, 99, Ahora, FechaEntrega, null);
        p.Id = 100;
        p.AgregarLinea(_repo.Productos.First(p => p.Id == 10), 1);
        _repo.Pedidos.Add(p);

        var caso = new CambiarEstadoPedido(_repo, _uow, _eventos);
        var res = await caso.EjecutarAsync(100, new CambiarEstadoPedidoSolicitud(EstadoPedido.Entregado, p.RowVersion), 99, CancellationToken.None);

        Assert.True(res.Exito);
        Assert.Equal(EstadoPedido.Entregado, res.Valor!.Estado);
        Assert.Equal(1, _uow.Guardados);
    }

    [Fact]
    public async Task CambiarEstado_Cancelado_ReponeStock()
    {
        var prod = _repo.Productos.First(p => p.Id == 10);
        var p = Pedido.Crear(1, 99, Ahora, FechaEntrega, null);
        p.Id = 101;
        p.AgregarLinea(prod, 2); // stock pasa de 5 a 3
        _repo.Pedidos.Add(p);

        Assert.Equal(3, prod.CantidadStock);

        var caso = new CambiarEstadoPedido(_repo, _uow, _eventos);
        var res = await caso.EjecutarAsync(101, new CambiarEstadoPedidoSolicitud(EstadoPedido.Cancelado, p.RowVersion), 99, CancellationToken.None);

        Assert.True(res.Exito);
        Assert.Equal(EstadoPedido.Cancelado, res.Valor!.Estado);
        Assert.Equal(5, prod.CantidadStock); // repuesto a 5
    }

    [Fact]
    public async Task DesactivarPedido_Pendiente_CancelaYReponeStock()
    {
        var prod = _repo.Productos.First(p => p.Id == 10);
        var p = Pedido.Crear(1, 99, Ahora, FechaEntrega, null);
        p.Id = 102;
        p.AgregarLinea(prod, 3); // stock pasa a 2
        _repo.Pedidos.Add(p);

        var caso = new DesactivarPedido(_repo, _uow, _eventos, _reloj);
        var res = await caso.EjecutarAsync(102, 99, CancellationToken.None);

        Assert.True(res.Exito);
        Assert.False(p.Activo);
        Assert.Equal(EstadoPedido.Cancelado, p.Estado);
        Assert.Equal(5, prod.CantidadStock); // repuesto
    }
}
