using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Productos;
using GestionPedidos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GestionPedidos.Api.Tests;

/// <summary>Pedidos del cliente en la tienda: reglas, stock, concurrencia y acceso solo a lo propio.</summary>
[Collection(ApiCollection.Nombre)]
public class MisPedidosApiTests
{
    private readonly ApiFixture _api;
    public MisPedidosApiTests(ApiFixture api) => _api = api;

    private static string Sufijo() => Guid.NewGuid().ToString("N")[..8];
    private static DateOnly Manana() => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));

    private static async Task<string?> Codigo(HttpResponseMessage r)
    {
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("codigo", out var c) ? c.GetString() : null;
    }

    /// <summary>Un cliente de la tienda con su sesión (se registra por la API y entra).</summary>
    private async Task<HttpClient> ClienteAsync()
    {
        var correo = $"cli{Sufijo()}@ejemplo.com";
        var registro = await _api.CreateClient().PostAsJsonAsync("/api/v1/auth/registro",
            new RegistroClienteSolicitud("Cliente", "De Prueba", "78945612", "Colonia Escalón, casa 1", correo, 1, "ClaveCliente_2026"));
        Assert.Equal(HttpStatusCode.Created, registro.StatusCode);
        return _api.ClienteCon(await _api.LoginAsync(correo, "ClaveCliente_2026"));
    }

    private async Task<int> ProductoAsync(int stock, decimal precio = 10m, bool activo = true)
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GestionPedidosDbContext>();
        var p = new Producto { Nombre = "Prod " + Sufijo(), Descripcion = "d", CantidadStock = stock, PrecioVenta = precio, Activo = activo };
        db.Productos.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<int> StockAsync(int productoId)
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GestionPedidosDbContext>();
        return await db.Productos.AsNoTracking().Where(p => p.Id == productoId).Select(p => p.CantidadStock).SingleAsync();
    }

    private static CrearPedidoClienteSolicitud Pedido(params (int Producto, int Cantidad)[] lineas) =>
        new(lineas.Select(l => new LineaPedidoSolicitud(l.Producto, l.Cantidad)).ToList(), Manana(), "Entregar por la tarde");

    [Fact]
    public async Task Permisos_SinSesion401_YElPersonalNoPuedeHacerPedidosDeCliente403()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.CreateClient().GetAsync("/api/v1/mis-pedidos")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.CreateClient().PostAsJsonAsync("/api/v1/mis-pedidos", Pedido((1, 1)))).StatusCode);

        var admin = await _api.AdminAsync();   // el personal no tiene los permisos mispedidos.*
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/v1/mis-pedidos")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/v1/mis-pedidos", Pedido((1, 1)))).StatusCode);
    }

    [Fact]
    public async Task HacerUnPedido_CopiaElPrecioVigente_DescuentaElStock_YApareceEnMisPedidos()
    {
        var cliente = await ClienteAsync();
        var a = await ProductoAsync(stock: 10, precio: 12.50m);
        var b = await ProductoAsync(stock: 4, precio: 3m);

        var entrega = (await cliente.GetFromJsonAsync<DatosEntregaDto>("/api/v1/mis-pedidos/entrega"))!;
        Assert.Equal("Cliente De Prueba", entrega.Nombre);
        Assert.Equal("Colonia Escalón, casa 1", entrega.Direccion);

        var r = await cliente.PostAsJsonAsync("/api/v1/mis-pedidos", Pedido((a, 2), (b, 4), (a, 1)));   // el producto repetido se suma
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var pedido = (await r.Content.ReadFromJsonAsync<PedidoClienteDto>())!;
        Assert.Equal("Pendiente", pedido.Estado);
        Assert.True(pedido.PuedeCancelar);
        Assert.Equal(2, pedido.Lineas.Count);
        Assert.Equal(3, pedido.Lineas.Single(l => l.ProductoId == a).Cantidad);
        Assert.Equal(12.50m * 3 + 3m * 4, pedido.Total);
        Assert.Equal(7, await StockAsync(a));
        Assert.Equal(0, await StockAsync(b));

        var lista = (await cliente.GetFromJsonAsync<PaginaDto<PedidoClienteDto>>("/api/v1/mis-pedidos"))!;
        Assert.Contains(lista.Elementos, p => p.Id == pedido.Id);
        Assert.Equal(pedido.Total, (await cliente.GetFromJsonAsync<PedidoClienteDto>($"/api/v1/mis-pedidos/{pedido.Id}"))!.Total);
    }

    [Fact]
    public async Task Validaciones_FechaLineasStockProductosNoDisponibles()
    {
        var cliente = await ClienteAsync();
        var p = await ProductoAsync(stock: 2);
        var inactivo = await ProductoAsync(stock: 5, activo: false);

        async Task<HttpResponseMessage> Enviar(CrearPedidoClienteSolicitud s) => await cliente.PostAsJsonAsync("/api/v1/mis-pedidos", s);

        Assert.Equal(HttpStatusCode.BadRequest, (await Enviar(Pedido())).StatusCode);                                   // sin líneas
        Assert.Equal(HttpStatusCode.BadRequest, (await Enviar(Pedido((p, 0)))).StatusCode);                            // cantidad cero
        Assert.Equal(HttpStatusCode.BadRequest, (await Enviar(Pedido((p, 1)) with { FechaEntrega = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Enviar(Pedido((p, 1)) with { FechaEntrega = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(400)) })).StatusCode);

        var sinStock = await Enviar(Pedido((p, 3)));                      // pide 3 y hay 2
        Assert.Equal(HttpStatusCode.Conflict, sinStock.StatusCode);
        Assert.Equal("STOCK_INSUFICIENTE", await Codigo(sinStock));
        Assert.Equal(2, await StockAsync(p));                              // nada cambió

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Enviar(Pedido((inactivo, 1)))).StatusCode);   // producto dado de baja
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Enviar(Pedido((99999999, 1)))).StatusCode);   // inexistente
        var demasiadas = await ProductoAsync(stock: 50);
        Assert.Equal(HttpStatusCode.Conflict, (await Enviar(Pedido((demasiadas, 11)))).StatusCode);            // más de 10 por pedido
    }

    [Fact]
    public async Task DosClientesCompranLaUltimaUnidad_UnoGana_ElOtroRecibe409_YElStockNuncaQuedaNegativo()
    {
        var uno = await ClienteAsync();
        var dos = await ClienteAsync();
        var ultima = await ProductoAsync(stock: 1);

        var respuestas = await Task.WhenAll(
            uno.PostAsJsonAsync("/api/v1/mis-pedidos", Pedido((ultima, 1))),
            dos.PostAsJsonAsync("/api/v1/mis-pedidos", Pedido((ultima, 1))));

        Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(0, await StockAsync(ultima));
    }

    [Fact]
    public async Task Cancelar_SoloLosPendientesYPropios_ReponeElStock_YNoSePuedeCancelarDosVeces()
    {
        var cliente = await ClienteAsync();
        var otro = await ClienteAsync();
        var p = await ProductoAsync(stock: 5);
        var pedido = (await (await cliente.PostAsJsonAsync("/api/v1/mis-pedidos", Pedido((p, 3)))).Content.ReadFromJsonAsync<PedidoClienteDto>())!;
        Assert.Equal(2, await StockAsync(p));

        // Otro cliente no ve ni cancela pedidos ajenos: responde como si no existieran
        Assert.Equal(HttpStatusCode.NotFound, (await otro.GetAsync($"/api/v1/mis-pedidos/{pedido.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otro.PatchAsync($"/api/v1/mis-pedidos/{pedido.Id}/cancelar", null)).StatusCode);
        Assert.DoesNotContain((await otro.GetFromJsonAsync<PaginaDto<PedidoClienteDto>>("/api/v1/mis-pedidos"))!.Elementos, x => x.Id == pedido.Id);
        Assert.Equal(2, await StockAsync(p));

        var cancelado = await cliente.PatchAsync($"/api/v1/mis-pedidos/{pedido.Id}/cancelar", null);
        Assert.Equal(HttpStatusCode.OK, cancelado.StatusCode);
        var dto = (await cancelado.Content.ReadFromJsonAsync<PedidoClienteDto>())!;
        Assert.Equal("Cancelado", dto.Estado);
        Assert.False(dto.PuedeCancelar);
        Assert.Equal(5, await StockAsync(p));   // el stock volvió

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await cliente.PatchAsync($"/api/v1/mis-pedidos/{pedido.Id}/cancelar", null)).StatusCode);
        Assert.Equal(5, await StockAsync(p));   // y no se repone dos veces
    }

    [Fact]
    public async Task ElPedidoQuedaEnElHistorial()
    {
        var cliente = await ClienteAsync();
        var p = await ProductoAsync(stock: 3);
        var pedido = (await (await cliente.PostAsJsonAsync("/api/v1/mis-pedidos", Pedido((p, 1)))).Content.ReadFromJsonAsync<PedidoClienteDto>())!;

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GestionPedidosDbContext>();
        for (var i = 0; i < 40 && !await db.Actividades.AnyAsync(a => a.Entidad == "Pedido" && a.EntidadId == pedido.Id); i++) await Task.Delay(250);
        Assert.True(await db.Actividades.AnyAsync(a => a.Entidad == "Pedido" && a.EntidadId == pedido.Id && a.Accion == "Registrar"));
    }
}
