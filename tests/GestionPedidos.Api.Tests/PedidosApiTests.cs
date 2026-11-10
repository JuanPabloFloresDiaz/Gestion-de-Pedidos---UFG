using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Clientes;
using GestionPedidos.Domain.Productos;
using GestionPedidos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GestionPedidos.Api.Tests;

/// <summary>
/// Administración de pedidos (<c>/api/v1/pedidos</c>, [Pedidos.md §5]): permisos, reglas, stock, atomicidad, concurrencia,
/// baja lógica y búsquedas, contra SQL Server real.
/// </summary>
[Collection(ApiCollection.Nombre)]
public class PedidosApiTests
{
    private readonly ApiFixture _api;
    public PedidosApiTests(ApiFixture api) => _api = api;

    private static string Sufijo() => Guid.NewGuid().ToString("N")[..8];
    private static DateOnly Entrega(int dias = 3) => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(dias));

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    // ---------- Datos de apoyo (directo en la base, como hacen las demás pruebas de API) ----------

    private async Task<int> ClienteAsync(bool activo = true, string? apellidos = null)
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GestionPedidosDbContext>();
        var c = new Cliente { Nombres = "Cliente", Apellidos = apellidos ?? "Pedidos " + Sufijo(), Telefono = "78945612", Direccion = "Col. Escalón", CiudadId = 1, Activo = activo };
        db.Clientes.Add(c);
        await db.SaveChangesAsync();
        return c.Id;
    }

    private async Task<int> ProductoAsync(int stock, decimal precio = 10m, bool activo = true, string? nombre = null)
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GestionPedidosDbContext>();
        var p = new Producto { Nombre = nombre ?? "Prod " + Sufijo(), Descripcion = "d", CantidadStock = stock, PrecioVenta = precio, Activo = activo };
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

    private async Task<int> PedidosDelClienteAsync(int clienteId)
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GestionPedidosDbContext>();
        return await db.Pedidos.CountAsync(p => p.ClienteId == clienteId);
    }

    private static CrearPedidoSolicitud Pedido(int clienteId, DateOnly? entrega = null, string? comentarios = null, params (int Producto, int Cantidad)[] lineas) =>
        new(clienteId, entrega ?? Entrega(), comentarios, lineas.Select(l => new LineaPedidoSolicitud(l.Producto, l.Cantidad)).ToList());

    private static async Task<string?> Codigo(HttpResponseMessage r)
    {
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("codigo", out var c) ? c.GetString() : null;
    }

    private static async Task<PedidoDetalleDto> LeerAsync(HttpResponseMessage r) =>
        (await r.Content.ReadFromJsonAsync<PedidoDetalleDto>(Web))!;

    private async Task<PedidoDetalleDto> RegistrarAsync(HttpClient admin, int clienteId, params (int Producto, int Cantidad)[] lineas)
    {
        var r = await admin.PostAsJsonAsync("/api/v1/pedidos", Pedido(clienteId, lineas: lineas));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return await LeerAsync(r);
    }

    private static ActualizarPedidoSolicitud Editar(PedidoDetalleDto p, params (int Producto, int Cantidad)[] lineas) =>
        new(Entrega(5), "editado", lineas.Select(l => new LineaPedidoSolicitud(l.Producto, l.Cantidad)).ToList(), p.RowVersion);

    private async Task<HttpClient> OperadorAsync(HttpClient admin)
    {
        var n = "op" + Sufijo();
        await admin.PostAsJsonAsync("/api/v1/usuarios", new CrearUsuarioSolicitud(n, "Operador", $"{n}@e.com", 2, "ClaveTemporal_2026"));
        var sesion = await _api.LoginAsync(n, "ClaveTemporal_2026");
        var cambio = await _api.ClienteCon(sesion).PostAsJsonAsync("/api/v1/auth/cambiar-clave", new CambiarClaveSolicitud("ClaveTemporal_2026", "ClaveOperador_2026"));
        cambio.EnsureSuccessStatusCode();
        return _api.ClienteCon((await cambio.Content.ReadFromJsonAsync<SesionDto>())!);
    }

    // ===================== Permisos =====================

    [Fact]
    public async Task SinSesion_TodosLosEndpoints_Dan401()
    {
        var anonimo = _api.CreateClient();
        var json = JsonContent.Create(new { });

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/v1/pedidos")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/v1/pedidos/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/v1/pedidos/clientes-selector")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/v1/pedidos/productos-selector")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsync("/api/v1/pedidos", json)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PutAsync("/api/v1/pedidos/1", json)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PatchAsync("/api/v1/pedidos/1/estado", json)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PatchAsync("/api/v1/pedidos/1/desactivar", null)).StatusCode);
    }

    [Fact]
    public async Task Operador_PuedeVerCrearEditarYCambiarEstado_PeroNoDarDeBaja403()
    {
        var admin = await _api.AdminAsync();
        var operador = await OperadorAsync(admin);
        var cliente = await ClienteAsync();
        var producto = await ProductoAsync(stock: 10);

        Assert.Equal(HttpStatusCode.OK, (await operador.GetAsync("/api/v1/pedidos")).StatusCode);
        var creado = await operador.PostAsJsonAsync("/api/v1/pedidos", Pedido(cliente, lineas: (producto, 1)));
        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
        var pedido = await LeerAsync(creado);

        Assert.Equal(HttpStatusCode.OK, (await operador.PutAsJsonAsync($"/api/v1/pedidos/{pedido.Id}", Editar(pedido, (producto, 2)))).StatusCode);
        var actual = await LeerAsync(await operador.GetAsync($"/api/v1/pedidos/{pedido.Id}"));
        Assert.Equal(HttpStatusCode.OK, (await operador.PatchAsJsonAsync($"/api/v1/pedidos/{pedido.Id}/estado", new CambiarEstadoPedidoSolicitud(EstadosPedido.Entregado, actual.RowVersion))).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await operador.PatchAsync($"/api/v1/pedidos/{pedido.Id}/desactivar", null)).StatusCode);
    }

    [Fact]
    public async Task UnClienteDeLaWeb_NoPuedeUsarLaAdministracion403()
    {
        var correo = $"cli{Sufijo()}@ejemplo.com";
        var registro = await _api.CreateClient().PostAsJsonAsync("/api/v1/auth/registro",
            new RegistroClienteSolicitud("Cliente", "Web", "78945612", "Col. Escalón", correo, 1, "ClaveCliente_2026"));
        Assert.Equal(HttpStatusCode.Created, registro.StatusCode);
        var web = _api.ClienteCon(await _api.LoginAsync(correo, "ClaveCliente_2026"));

        Assert.Equal(HttpStatusCode.Forbidden, (await web.GetAsync("/api/v1/pedidos")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await web.GetAsync("/api/v1/pedidos/clientes-selector")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await web.PostAsJsonAsync("/api/v1/pedidos", Pedido(1, lineas: (1, 1)))).StatusCode);
    }

    // ===================== Registrar =====================

    [Fact]
    public async Task Registrar_Exitoso_201_CopiaPrecio_DescuentaStock_AsignaUsuario_YDevuelveLocation()
    {
        var admin = await _api.AdminAsync();
        var cliente = await ClienteAsync();
        var a = await ProductoAsync(stock: 10, precio: 12.50m);
        var b = await ProductoAsync(stock: 5, precio: 3m);

        var r = await admin.PostAsJsonAsync("/api/v1/pedidos", Pedido(cliente, comentarios: "Entregar por la tarde", lineas: new[] { (a, 2), (b, 1) }));

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var pedido = await LeerAsync(r);
        Assert.Equal($"/api/v1/pedidos/{pedido.Id}", r.Headers.Location!.OriginalString);
        Assert.Equal(EstadosPedido.Pendiente, pedido.Estado);
        Assert.Equal(28m, pedido.Total);   // 2*12.50 + 3
        Assert.Equal(2, pedido.Lineas.Count);
        Assert.Equal(8, await StockAsync(a));
        Assert.Equal(4, await StockAsync(b));
        Assert.True(pedido.UsuarioId > 0);   // lo pone el servidor
    }

    [Fact]
    public async Task Registrar_ElPrecioViajaDelServidor_NoDelCliente()
    {
        var admin = await _api.AdminAsync();
        var cliente = await ClienteAsync();
        var producto = await ProductoAsync(stock: 10, precio: 40m);

        // un cliente malicioso intenta mandar su propio precio y usuario: se ignoran
        var cuerpo = $$"""{"clienteId":{{cliente}},"fechaEntrega":"{{Entrega():yyyy-MM-dd}}","usuarioId":999,"lineas":[{"productoId":{{producto}},"cantidad":1,"precioUnitario":0.01}]}""";
        var r = await admin.PostAsync("/api/v1/pedidos", new StringContent(cuerpo, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var pedido = await LeerAsync(r);
        Assert.Equal(40m, pedido.Lineas.Single().PrecioUnitario);
        Assert.NotEqual(999, pedido.UsuarioId);
    }

    [Fact]
    public async Task Registrar_LineasRepetidas_SeAgrupan()
    {
        var admin = await _api.AdminAsync();
        var producto = await ProductoAsync(stock: 10);

        var pedido = await RegistrarAsync(admin, await ClienteAsync(), (producto, 1), (producto, 2));

        Assert.Equal(3, pedido.Lineas.Single().Cantidad);
        Assert.Equal(7, await StockAsync(producto));
    }

    [Fact]
    public async Task Registrar_DatosInvalidos_400_YNoTocaElStock()
    {
        var admin = await _api.AdminAsync();
        var cliente = await ClienteAsync();
        var producto = await ProductoAsync(stock: 10);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/pedidos", Pedido(cliente))).StatusCode);   // sin líneas
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/pedidos", Pedido(cliente, lineas: (producto, 0)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/pedidos", Pedido(cliente, lineas: (producto, -2)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/pedidos", Pedido(cliente, comentarios: new string('x', 501), lineas: (producto, 1)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/pedidos", Pedido(cliente, entrega: Entrega(-2), lineas: (producto, 1)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/pedidos", Pedido(999999, lineas: (producto, 1)))).StatusCode);   // cliente inexistente
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/pedidos", Pedido(cliente, lineas: (999999, 1)))).StatusCode);    // producto inexistente

        Assert.Equal(10, await StockAsync(producto));
        Assert.Equal(0, await PedidosDelClienteAsync(cliente));
    }

    [Fact]
    public async Task Registrar_JsonSinLineas_OMalformado_Da400_NoError500()
    {
        var admin = await _api.AdminAsync();
        var cliente = await ClienteAsync();

        var sinLineas = await admin.PostAsync("/api/v1/pedidos", new StringContent($$"""{"clienteId":{{cliente}},"fechaEntrega":"{{Entrega():yyyy-MM-dd}}"}""", Encoding.UTF8, "application/json"));
        var malformado = await admin.PostAsync("/api/v1/pedidos", new StringContent("{no es json", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, sinLineas.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, malformado.StatusCode);
    }

    [Fact]
    public async Task Registrar_ClienteInactivo_422()
    {
        var admin = await _api.AdminAsync();
        var inactivo = await ClienteAsync(activo: false);
        var producto = await ProductoAsync(stock: 10);

        var r = await admin.PostAsJsonAsync("/api/v1/pedidos", Pedido(inactivo, lineas: (producto, 1)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
        Assert.Equal(10, await StockAsync(producto));
    }

    [Fact]
    public async Task Registrar_StockInsuficiente_409_ConElNombreDelProducto_YNoDescuentaNada()
    {
        var admin = await _api.AdminAsync();
        var cliente = await ClienteAsync();
        var producto = await ProductoAsync(stock: 2, nombre: "Teclado raro " + Sufijo());

        var r = await admin.PostAsJsonAsync("/api/v1/pedidos", Pedido(cliente, lineas: (producto, 5)));

        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Equal(CodigosError.StockInsuficiente, await Codigo(r));
        Assert.Contains("Teclado raro", await r.Content.ReadAsStringAsync());
        Assert.Equal(2, await StockAsync(producto));
        Assert.Equal(0, await PedidosDelClienteAsync(cliente));
    }

    [Fact]
    public async Task Registrar_ProductoInactivo_NoSeVende()
    {
        var admin = await _api.AdminAsync();
        var cliente = await ClienteAsync();
        var inactivo = await ProductoAsync(stock: 10, activo: false);

        var r = await admin.PostAsJsonAsync("/api/v1/pedidos", Pedido(cliente, lineas: (inactivo, 1)));

        Assert.False(r.IsSuccessStatusCode);
        Assert.Equal(10, await StockAsync(inactivo));
    }

    [Fact]
    public async Task Registrar_Atomicidad_SiLaSegundaLineaNoTieneStock_NoQuedaNada()
    {
        var admin = await _api.AdminAsync();
        var cliente = await ClienteAsync();
        var conStock = await ProductoAsync(stock: 10);
        var sinStock = await ProductoAsync(stock: 1);

        var r = await admin.PostAsJsonAsync("/api/v1/pedidos", Pedido(cliente, lineas: new[] { (conStock, 3), (sinStock, 5) }));

        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Equal(10, await StockAsync(conStock));   // la primera línea no quedó descontada
        Assert.Equal(1, await StockAsync(sinStock));
        Assert.Equal(0, await PedidosDelClienteAsync(cliente));
    }

    [Fact]
    public async Task Registrar_DosPedidosPorLaUltimaUnidad_UnoGana_ElOtroRecibe409_YElStockNoQuedaNegativo()
    {
        var admin = await _api.AdminAsync();
        var c1 = await ClienteAsync();
        var c2 = await ClienteAsync();
        var producto = await ProductoAsync(stock: 1);

        var respuestas = await Task.WhenAll(
            admin.PostAsJsonAsync("/api/v1/pedidos", Pedido(c1, lineas: (producto, 1))),
            admin.PostAsJsonAsync("/api/v1/pedidos", Pedido(c2, lineas: (producto, 1))));

        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(0, await StockAsync(producto));
        Assert.Equal(1, await PedidosDelClienteAsync(c1) + await PedidosDelClienteAsync(c2));
    }

    // ===================== Consultar =====================

    [Fact]
    public async Task Obtener_Existente_200_ConLineas_Inexistente_404_YRutaNoNumerica_404()
    {
        var admin = await _api.AdminAsync();
        var producto = await ProductoAsync(stock: 10);
        var pedido = await RegistrarAsync(admin, await ClienteAsync(), (producto, 2));

        var r = await admin.GetAsync($"/api/v1/pedidos/{pedido.Id}");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Single((await LeerAsync(r)).Lineas);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/pedidos/99999999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/pedidos/abc")).StatusCode);
    }

    [Fact]
    public async Task Selectores_SoloActivos_ConStockYPrecio()
    {
        var admin = await _api.AdminAsync();
        var activo = await ClienteAsync(apellidos: "Selector" + Sufijo());
        var inactivo = await ClienteAsync(activo: false, apellidos: "Selector" + Sufijo());
        var prod = await ProductoAsync(stock: 7, precio: 4.5m, nombre: "SelProd " + Sufijo());
        var prodInactivo = await ProductoAsync(stock: 7, activo: false, nombre: "SelProd " + Sufijo());

        var clientes = await admin.GetFromJsonAsync<List<ClienteSelectorDto>>("/api/v1/pedidos/clientes-selector?q=Selector", Web);
        var productos = await admin.GetFromJsonAsync<List<ProductoSelectorDto>>("/api/v1/pedidos/productos-selector?q=SelProd", Web);

        Assert.Contains(clientes!, c => c.Id == activo);
        Assert.DoesNotContain(clientes!, c => c.Id == inactivo);
        Assert.Contains(productos!, p => p.Id == prod && p.CantidadStock == 7 && p.PrecioVenta == 4.5m);
        Assert.DoesNotContain(productos!, p => p.Id == prodInactivo);
    }

    // ===================== Editar =====================

    [Fact]
    public async Task Editar_AjustaElStockPorDiferencia_ConservaPrecios_Y_Devuelve200()
    {
        var admin = await _api.AdminAsync();
        var a = await ProductoAsync(stock: 10, precio: 10m);
        var b = await ProductoAsync(stock: 10, precio: 20m);
        var c = await ProductoAsync(stock: 10, precio: 30m);
        var pedido = await RegistrarAsync(admin, await ClienteAsync(), (a, 2), (b, 1));   // a:8 b:9

        // a sube a 5 (+3), se quita b (repone 1), se agrega c x2
        var r = await admin.PutAsJsonAsync($"/api/v1/pedidos/{pedido.Id}", Editar(pedido, (a, 5), (c, 2)));

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var editado = await LeerAsync(r);
        Assert.Equal(5, await StockAsync(a));
        Assert.Equal(10, await StockAsync(b));
        Assert.Equal(8, await StockAsync(c));
        Assert.Equal(110m, editado.Total);   // 5*10 + 2*30
        Assert.Equal(2, editado.Lineas.Count);
        Assert.Equal("editado", editado.Comentarios);
    }

    [Fact]
    public async Task Editar_ConRowVersionDesactualizado_409_SinCambiarElStock()
    {
        var admin = await _api.AdminAsync();
        var producto = await ProductoAsync(stock: 10);
        var pedido = await RegistrarAsync(admin, await ClienteAsync(), (producto, 1));
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/pedidos/{pedido.Id}", Editar(pedido, (producto, 2)))).StatusCode);

        var r = await admin.PutAsJsonAsync($"/api/v1/pedidos/{pedido.Id}", Editar(pedido, (producto, 4)));   // versión vieja

        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Equal(8, await StockAsync(producto));   // quedó como la primera edición (2 unidades)
    }

    [Fact]
    public async Task Editar_Inexistente_404_Entregado_422_YMasQueElStock_409()
    {
        var admin = await _api.AdminAsync();
        var producto = await ProductoAsync(stock: 5);
        var cliente = await ClienteAsync();
        var pedido = await RegistrarAsync(admin, cliente, (producto, 2));   // quedan 3

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync("/api/v1/pedidos/99999999", Editar(pedido, (producto, 1)))).StatusCode);

        var tropas = await admin.PutAsJsonAsync($"/api/v1/pedidos/{pedido.Id}", Editar(pedido, (producto, 9)));   // faltan 7, hay 3
        Assert.Equal(HttpStatusCode.Conflict, tropas.StatusCode);
        Assert.Equal(CodigosError.StockInsuficiente, await Codigo(tropas));
        Assert.Equal(3, await StockAsync(producto));

        var entregado = await LeerAsync(await admin.PatchAsJsonAsync($"/api/v1/pedidos/{pedido.Id}/estado", new CambiarEstadoPedidoSolicitud(EstadosPedido.Entregado, pedido.RowVersion)));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PutAsJsonAsync($"/api/v1/pedidos/{pedido.Id}", Editar(entregado, (producto, 1)))).StatusCode);
    }

    [Fact]
    public async Task Editar_DatosInvalidos_400()
    {
        var admin = await _api.AdminAsync();
        var producto = await ProductoAsync(stock: 10);
        var pedido = await RegistrarAsync(admin, await ClienteAsync(), (producto, 1));

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/pedidos/{pedido.Id}", Editar(pedido))).StatusCode);                  // sin líneas
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/pedidos/{pedido.Id}", Editar(pedido, (producto, 0)))).StatusCode);   // cantidad 0
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/pedidos/{pedido.Id}", Editar(pedido, (999999, 1)))).StatusCode);     // producto inexistente
    }

    // ---------- Pendiente de la revisión (roja hasta corregirla) ----------

    /// <summary>
    /// Revisión del PR #2 (<c>ActualizarPedido.cs:57</c> y <c>CambiarEstadoPedido.cs:42</c>): sin <c>RowVersion</c> hoy responde 409
    /// con el mensaje "modificado por otro usuario", que no es cierto; debe ser 400 ("falta la versión").
    /// </summary>
    [Fact]
    [Trait("Revision", "pendiente-de-correccion")]
    public async Task EditarYCambiarEstado_SinRowVersion_DebenResponder400()
    {
        var admin = await _api.AdminAsync();
        var producto = await ProductoAsync(stock: 10);
        var pedido = await RegistrarAsync(admin, await ClienteAsync(), (producto, 1));

        var sinVersionEditar = await admin.PutAsync($"/api/v1/pedidos/{pedido.Id}", new StringContent(
            $$"""{"fechaEntrega":"{{Entrega():yyyy-MM-dd}}","lineas":[{"productoId":{{producto}},"cantidad":2}]}""", Encoding.UTF8, "application/json"));
        var sinVersionEstado = await admin.PatchAsync($"/api/v1/pedidos/{pedido.Id}/estado", new StringContent(
            """{"estado":"Entregado"}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, sinVersionEditar.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, sinVersionEstado.StatusCode);
    }

    // ===================== Cambiar estado =====================

    [Fact]
    public async Task Estado_Entregado_NoCambiaElStock_YEsFinal()
    {
        var admin = await _api.AdminAsync();
        var producto = await ProductoAsync(stock: 10);
        var pedido = await RegistrarAsync(admin, await ClienteAsync(), (producto, 4));

        var r = await admin.PatchAsJsonAsync($"/api/v1/pedidos/{pedido.Id}/estado", new CambiarEstadoPedidoSolicitud(EstadosPedido.Entregado, pedido.RowVersion));

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var entregado = await LeerAsync(r);
        Assert.Equal(EstadosPedido.Entregado, entregado.Estado);
        Assert.Equal(6, await StockAsync(producto));

        // desde un estado final no se puede pasar a ninguno
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PatchAsJsonAsync($"/api/v1/pedidos/{pedido.Id}/estado", new CambiarEstadoPedidoSolicitud(EstadosPedido.Cancelado, entregado.RowVersion))).StatusCode);
        Assert.Equal(6, await StockAsync(producto));
    }

    [Fact]
    public async Task Estado_Cancelado_ReponeElStock()
    {
        var admin = await _api.AdminAsync();
        var a = await ProductoAsync(stock: 10);
        var b = await ProductoAsync(stock: 10);
        var pedido = await RegistrarAsync(admin, await ClienteAsync(), (a, 4), (b, 3));

        var r = await admin.PatchAsJsonAsync($"/api/v1/pedidos/{pedido.Id}/estado", new CambiarEstadoPedidoSolicitud(EstadosPedido.Cancelado, pedido.RowVersion));

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(10, await StockAsync(a));
        Assert.Equal(10, await StockAsync(b));
    }

    [Fact]
    public async Task Estado_Invalido_400_Inexistente_404_YVersionVieja_409()
    {
        var admin = await _api.AdminAsync();
        var producto = await ProductoAsync(stock: 10);
        var pedido = await RegistrarAsync(admin, await ClienteAsync(), (producto, 1));
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/pedidos/{pedido.Id}", Editar(pedido, (producto, 2)))).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PatchAsJsonAsync($"/api/v1/pedidos/{pedido.Id}/estado", new CambiarEstadoPedidoSolicitud("Pendiente", pedido.RowVersion))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PatchAsJsonAsync($"/api/v1/pedidos/{pedido.Id}/estado", new CambiarEstadoPedidoSolicitud("Inventado", pedido.RowVersion))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PatchAsJsonAsync("/api/v1/pedidos/99999999/estado", new CambiarEstadoPedidoSolicitud(EstadosPedido.Entregado, pedido.RowVersion))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PatchAsJsonAsync($"/api/v1/pedidos/{pedido.Id}/estado", new CambiarEstadoPedidoSolicitud(EstadosPedido.Cancelado, pedido.RowVersion))).StatusCode);   // versión vieja
    }

    [Fact]
    public async Task Estado_DobleCancelacionSimultanea_UnoGana_ElOtroFalla_YElStockSeRepoeUnaSolaVez()
    {
        var admin = await _api.AdminAsync();
        var producto = await ProductoAsync(stock: 10);
        var pedido = await RegistrarAsync(admin, await ClienteAsync(), (producto, 4));   // stock 6
        var solicitud = new CambiarEstadoPedidoSolicitud(EstadosPedido.Cancelado, pedido.RowVersion);

        var respuestas = await Task.WhenAll(
            admin.PatchAsJsonAsync($"/api/v1/pedidos/{pedido.Id}/estado", solicitud),
            admin.PatchAsJsonAsync($"/api/v1/pedidos/{pedido.Id}/estado", solicitud));

        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(respuestas, r => r.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity);
        Assert.Equal(10, await StockAsync(producto));   // 6 + 4, no 14
    }

    // ===================== Baja lógica =====================

    [Fact]
    public async Task Desactivar_Pendiente_CancelaRepone_YDejaDeSalirEnElListado()
    {
        var admin = await _api.AdminAsync();
        var cliente = await ClienteAsync();
        var producto = await ProductoAsync(stock: 10);
        var pedido = await RegistrarAsync(admin, cliente, (producto, 4));

        var r = await admin.PatchAsync($"/api/v1/pedidos/{pedido.Id}/desactivar", null);

        Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
        Assert.Equal(10, await StockAsync(producto));
        var detalle = await LeerAsync(await admin.GetAsync($"/api/v1/pedidos/{pedido.Id}"));
        Assert.False(detalle.Activo);
        Assert.Equal(EstadosPedido.Cancelado, detalle.Estado);

        var porDefecto = await admin.GetFromJsonAsync<PaginaDto<PedidoResumenDto>>($"/api/v1/pedidos?clienteId={cliente}", Web);
        var conBajas = await admin.GetFromJsonAsync<PaginaDto<PedidoResumenDto>>($"/api/v1/pedidos?clienteId={cliente}&incluirInactivos=true", Web);
        Assert.Empty(porDefecto!.Elementos);
        Assert.Single(conBajas!.Elementos);
    }

    [Fact]
    public async Task Desactivar_Entregado_SoloMarcaInactivo_SinTocarElStock_YUnInactivoNoSeEdita()
    {
        var admin = await _api.AdminAsync();
        var producto = await ProductoAsync(stock: 10);
        var pedido = await RegistrarAsync(admin, await ClienteAsync(), (producto, 4));
        var entregado = await LeerAsync(await admin.PatchAsJsonAsync($"/api/v1/pedidos/{pedido.Id}/estado", new CambiarEstadoPedidoSolicitud(EstadosPedido.Entregado, pedido.RowVersion)));

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PatchAsync($"/api/v1/pedidos/{pedido.Id}/desactivar", null)).StatusCode);

        Assert.Equal(6, await StockAsync(producto));
        var inactivo = await LeerAsync(await admin.GetAsync($"/api/v1/pedidos/{pedido.Id}"));
        Assert.Equal(EstadosPedido.Entregado, inactivo.Estado);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PutAsJsonAsync($"/api/v1/pedidos/{pedido.Id}", Editar(inactivo, (producto, 1)))).StatusCode);
    }

    [Fact]
    public async Task Desactivar_Inexistente_404_YRepetido_EsIdempotente()
    {
        var admin = await _api.AdminAsync();
        var producto = await ProductoAsync(stock: 10);
        var pedido = await RegistrarAsync(admin, await ClienteAsync(), (producto, 2));

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PatchAsync("/api/v1/pedidos/99999999/desactivar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PatchAsync($"/api/v1/pedidos/{pedido.Id}/desactivar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PatchAsync($"/api/v1/pedidos/{pedido.Id}/desactivar", null)).StatusCode);
        Assert.Equal(10, await StockAsync(producto));   // no se repone dos veces
    }

    // ===================== Búsquedas =====================

    [Fact]
    public async Task Buscar_PorCliente_Producto_Estado_Precio_Fechas_YUsuario()
    {
        var admin = await _api.AdminAsync();
        var apellido = "Buscable" + Sufijo();
        var cliente = await ClienteAsync(apellidos: apellido);
        var otroCliente = await ClienteAsync();
        var caro = await ProductoAsync(stock: 10, precio: 500m, nombre: "Caro " + Sufijo());
        var barato = await ProductoAsync(stock: 10, precio: 2m, nombre: "Barato " + Sufijo());
        var delCaro = await RegistrarAsync(admin, cliente, (caro, 1));
        var delBarato = await RegistrarAsync(admin, cliente, (barato, 1));
        var ajeno = await RegistrarAsync(admin, otroCliente, (barato, 1));
        await admin.PatchAsJsonAsync($"/api/v1/pedidos/{delBarato.Id}/estado", new CambiarEstadoPedidoSolicitud(EstadosPedido.Entregado, delBarato.RowVersion));

        async Task<List<int>> Ids(string consulta) =>
            (await admin.GetFromJsonAsync<PaginaDto<PedidoResumenDto>>("/api/v1/pedidos?" + consulta, Web))!.Elementos.Select(p => p.Id).ToList();

        Assert.Equal(new[] { delCaro.Id, delBarato.Id }.OrderBy(i => i), (await Ids($"cliente={apellido}")).OrderBy(i => i));   // por apellidos
        Assert.Equal(new[] { delCaro.Id }, await Ids($"cliente={apellido}&productoId={caro}"));
        Assert.Contains(delBarato.Id, await Ids($"producto={Uri.EscapeDataString("Barato")}&clienteId={cliente}"));
        Assert.Equal(new[] { delBarato.Id }, await Ids($"clienteId={cliente}&estado=Entregado"));
        Assert.Equal(new[] { delCaro.Id }, await Ids($"clienteId={cliente}&precioMin=100"));
        Assert.Equal(new[] { delBarato.Id }, await Ids($"clienteId={cliente}&precioMax=10"));
        Assert.Equal(2, (await Ids($"clienteId={cliente}&entregaDesde={Entrega(0):yyyy-MM-dd}&entregaHasta={Entrega(10):yyyy-MM-dd}")).Count);
        Assert.Empty(await Ids($"clienteId={cliente}&entregaDesde={Entrega(30):yyyy-MM-dd}"));
        Assert.Contains(ajeno.Id, await Ids($"usuarioId={ajeno.UsuarioId}&clienteId={otroCliente}"));
    }

    [Fact]
    public async Task Buscar_ConInyeccionSql_NoRompeNiDevuelveTodo_YLasTablasSiguenIntactas()
    {
        var admin = await _api.AdminAsync();
        var cliente = await ClienteAsync();
        var producto = await ProductoAsync(stock: 10);
        await RegistrarAsync(admin, cliente, (producto, 1));

        foreach (var texto in new[] { "' OR 1=1 --", "'; DROP TABLE Pedidos; --", "x' UNION SELECT 1,2,3 --", "\" OR \"1\"=\"1" })
        {
            var r = await admin.GetAsync($"/api/v1/pedidos?cliente={Uri.EscapeDataString(texto)}&producto={Uri.EscapeDataString(texto)}");
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            var pagina = await r.Content.ReadFromJsonAsync<PaginaDto<PedidoResumenDto>>(Web);
            Assert.Empty(pagina!.Elementos);
        }

        Assert.Equal(1, await PedidosDelClienteAsync(cliente));   // la tabla sigue ahí
    }

    [Fact]
    public async Task Buscar_Paginacion_NormalizaValoresRaros_YNoDa500()
    {
        var admin = await _api.AdminAsync();
        var cliente = await ClienteAsync();
        var producto = await ProductoAsync(stock: 50);
        for (var i = 0; i < 3; i++) await RegistrarAsync(admin, cliente, (producto, 1));

        var p1 = await admin.GetFromJsonAsync<PaginaDto<PedidoResumenDto>>($"/api/v1/pedidos?clienteId={cliente}&pagina=1&tamanoPagina=2", Web);
        var p2 = await admin.GetFromJsonAsync<PaginaDto<PedidoResumenDto>>($"/api/v1/pedidos?clienteId={cliente}&pagina=2&tamanoPagina=2", Web);

        Assert.Equal(3, p1!.Total);
        Assert.Equal(2, p1.Elementos.Count);
        Assert.Single(p2!.Elementos);
        Assert.Empty(p1.Elementos.Select(e => e.Id).Intersect(p2.Elementos.Select(e => e.Id)));
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/pedidos?pagina=0&tamanoPagina=0")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/pedidos?pagina=-5&tamanoPagina=99999")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/pedidos?pagina=abc")).StatusCode);
    }
}
