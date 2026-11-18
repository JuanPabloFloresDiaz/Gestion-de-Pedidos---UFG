using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Application.Tienda;
using GestionPedidos.Domain.Clientes;
using GestionPedidos.Domain.Pedidos;
using GestionPedidos.Domain.Productos;
using Microsoft.Extensions.Time.Testing;

namespace GestionPedidos.Application.Tests;

/// <summary>Confirmar la recepción de un pedido desde la tienda: solo pedidos propios, entregados y una sola vez.</summary>
public class ConfirmarRecepcionTests
{
    private sealed class RepoFake : IPedidosClienteRepository
    {
        public Cliente Cliente { get; } = new() { Id = 1, Nombres = "Carlos", Apellidos = "López", Telefono = "7777-8888", Direccion = "San Salvador", Activo = true };
        public List<Pedido> Pedidos { get; } = new();
        public Task<Cliente?> ObtenerClienteDeUsuarioAsync(int usuarioId, CancellationToken ct) => Task.FromResult<Cliente?>(usuarioId == 7 ? Cliente : null);
        public Task<IReadOnlyList<Producto>> ObtenerProductosVendiblesAsync(IReadOnlyCollection<int> ids, CancellationToken ct) => throw new NotSupportedException();
        public void Agregar(Pedido pedido) => throw new NotSupportedException();
        public Task<(IReadOnlyList<Pedido> Elementos, int Total)> ListarAsync(int clienteId, int pagina, int tamano, CancellationToken ct) => throw new NotSupportedException();
        public Task<Pedido?> ObtenerAsync(int clienteId, int pedidoId, CancellationToken ct) =>
            Task.FromResult(Pedidos.FirstOrDefault(p => p.Id == pedidoId && p.ClienteId == clienteId));
    }

    private static readonly DateTimeOffset Ahora = new(2026, 11, 20, 15, 0, 0, TimeSpan.Zero);
    private readonly RepoFake _repo = new();
    private readonly UnitOfWorkFake _uow = new();
    private readonly EventosAplicacion _eventos = new();
    private readonly List<EntidadCambiadaServidorEventArgs> _cambios = new();

    public ConfirmarRecepcionTests() => _eventos.EntidadCambiada += (_, e) => _cambios.Add(e);

    private ConfirmarRecepcionDePedido Caso(IUnitOfWork? uow = null) => new(_repo, uow ?? _uow, _eventos, new FakeTimeProvider(Ahora));

    private Pedido PedidoEn(string estado, int id = 100)
    {
        var producto = new Producto { Id = 10, Codigo = "PRD-000010", Nombre = "Teclado", PrecioVenta = 50m, CantidadStock = 10, Activo = true };
        var p = Pedido.Crear(1, 7, Ahora.AddDays(-3), new DateOnly(2026, 11, 20), null);
        p.Id = id;
        p.AgregarLinea(producto, 2);
        if (estado == EstadoPedido.Entregado) p.MarcarEntregado();
        if (estado == EstadoPedido.Cancelado) p.Cancelar();
        _repo.Pedidos.Add(p);
        return p;
    }

    [Fact]
    public async Task PedidoEntregado_SeConfirma_GuardaLaFecha_PublicaElEvento_YYaNoSePuedeConfirmar()
    {
        var pedido = PedidoEn(EstadoPedido.Entregado);

        var r = await Caso().EjecutarAsync(7, 100, default);

        Assert.True(r.Exito);
        Assert.Equal(Ahora, pedido.ConfirmadoPorClienteEn);
        Assert.Equal(Ahora, r.Valor!.ConfirmadoEn);
        Assert.False(r.Valor.PuedeConfirmar);
        var e = Assert.Single(_cambios);
        Assert.Equal(TipoCambioServidor.Confirmada, e.Tipo);
        Assert.Equal(7, e.UsuarioId);
    }

    [Fact]
    public async Task ConfirmarDosVeces_EsUnaReglaDeNegocio_YNoCambiaLaFecha()
    {
        var pedido = PedidoEn(EstadoPedido.Entregado);
        await Caso().EjecutarAsync(7, 100, default);
        _cambios.Clear();

        var r = await Caso().EjecutarAsync(7, 100, default);

        Assert.Equal(TipoError.Regla, r.Error!.Tipo);
        Assert.Equal(Ahora, pedido.ConfirmadoPorClienteEn);
        Assert.Empty(_cambios);
    }

    [Theory]
    [InlineData("Pendiente")]
    [InlineData("Cancelado")]
    public async Task PedidoQueNoEstaEntregado_NoSePuedeConfirmar(string estado)
    {
        PedidoEn(estado);

        var r = await Caso().EjecutarAsync(7, 100, default);

        Assert.Equal(TipoError.Regla, r.Error!.Tipo);
        Assert.Empty(_cambios);
    }

    [Fact]
    public async Task PedidoAjeno_OInexistente_NoEncontrado()
    {
        PedidoEn(EstadoPedido.Entregado);

        Assert.Equal(TipoError.NoEncontrado, (await Caso().EjecutarAsync(7, 999, default)).Error!.Tipo);   // no existe
        Assert.Equal(TipoError.NoEncontrado, (await Caso().EjecutarAsync(8, 100, default)).Error!.Tipo);   // otro usuario (sin cliente)
    }

    [Fact]
    public async Task ConConflictoDeConcurrencia_DevuelveConflicto()
    {
        PedidoEn(EstadoPedido.Entregado);

        var r = await Caso(new UnidadDeTrabajoConConflicto()).EjecutarAsync(7, 100, default);

        Assert.Equal(TipoError.Conflicto, r.Error!.Tipo);
        Assert.Empty(_cambios);
    }
}
