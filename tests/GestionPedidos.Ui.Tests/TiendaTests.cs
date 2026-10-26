using System.Net;
using GestionPedidos.Contracts;
using GestionPedidos.Ui.Tienda;
using Microsoft.JSInterop;

namespace GestionPedidos.Ui.Tests;

/// <summary>JavaScript falso: guarda lo que el carrito escribiría en el almacenamiento local del navegador.</summary>
internal sealed class JsFalso : IJSRuntime
{
    public string Guardado { get; set; } = "[]";
    public bool Falla { get; set; }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        if (Falla) throw new JSException("sin almacenamiento");
        if (identifier == "gpTienda.guardar") { Guardado = (string)args![0]!; return default!; }
        return new ValueTask<TValue>((TValue)(object)Guardado);
    }
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => InvokeAsync<TValue>(identifier, args);
}

public class TiendaTests
{
    private static ProductoTiendaDto Producto(int id, string nombre = "Camisa", decimal precio = 20m, bool disponible = true, int maximo = 10, int? sub = 5) =>
        new(id, nombre, "d", precio, 1, "Ropa", sub, "Camisas", disponible, null, maximo, new DateOnly(2026, 10, 1), Array.Empty<DetalleProductoTiendaDto>());

    // ---------- Filtros en la dirección ----------
    [Fact]
    public void Filtros_IdaYVuelta_PorLaDireccion()
    {
        var f = FiltrosCatalogo.Desde("camisa roja", "1", "5", "10,12", "5.50", "40", "1", "precio-asc", "3");
        Assert.Equal(("camisa roja", 1, 5), (f.Q, f.CategoriaId, f.SubcategoriaId));
        Assert.Equal(new[] { 10, 12 }, f.Opciones);
        Assert.Equal((5.5m, 40m, true, 3), (f.PrecioMin, f.PrecioMax, f.SoloDisponibles, f.Pagina));

        var url = f.Url();
        Assert.StartsWith("/tienda?", url);
        Assert.Contains("q=camisa%20roja", url);
        Assert.Contains("op=10%2C12", url);
        Assert.Contains("pag=3", url);
    }

    [Fact]
    public void Filtros_LoInvalidoSeIgnora_YSinFiltrosLaUrlEsLimpia()
    {
        var f = FiltrosCatalogo.Desde("  ", "abc", null, "1,x,,1", "-3", "no", "0", null, "-9");
        Assert.Null(f.Q); Assert.Null(f.CategoriaId);
        Assert.Equal(new[] { 1 }, f.Opciones);   // sin repetidos ni basura
        Assert.Null(f.PrecioMin); Assert.Null(f.PrecioMax);
        Assert.False(f.SoloDisponibles);
        Assert.Equal(1, f.Pagina);
        Assert.Equal("/tienda", (f with { OpcionIds = null }).Url());
    }

    [Fact]
    public void Filtros_ParaLaApiUsanSusNombres_YMarcarOpcionVuelveALaPrimeraPagina()
    {
        var f = new FiltrosCatalogo(SubcategoriaId: 5, PrecioMax: 30, SoloDisponibles: true, Pagina: 4);
        var api = f.ToQuery(paraApi: true);
        Assert.Contains("subcategoriaId=5", api);
        Assert.Contains("precioMax=30", api);
        Assert.Contains("soloDisponibles=true", api);
        Assert.Contains("tamanoPagina=12", api);

        var con = f.ConOpcion(7, true);
        Assert.Equal(new[] { 7 }, con.Opciones);
        Assert.Equal(1, con.Pagina);
        Assert.Empty(con.ConOpcion(7, false).Opciones);
    }

    [Theory]
    [InlineData("/carrito", true)]
    [InlineData("/pedido?x=1", true)]
    [InlineData("//malo.com", false)]
    [InlineData("https://malo.com", false)]
    [InlineData("/\\malo.com", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Retorno_SoloAceptaRutasInternas(string? ruta, bool esperado) => Assert.Equal(esperado, Retorno.EsRutaInterna(ruta));

    // ---------- Carrito ----------
    private static LineaCarrito Linea(int id, int cantidad = 1, int maximo = 5, decimal precio = 10m) => new(id, $"P{id}", precio, cantidad, maximo, "🛍️", 200);

    [Fact]
    public async Task Carrito_AgregaSumaYNoPasaDelMaximo_Total_YPersiste()
    {
        var js = new JsFalso();
        var carrito = new CarritoService(js);
        var cambios = 0;
        carrito.Cambiado += (_, _) => cambios++;

        Assert.Equal(2, await carrito.AgregarAsync(Linea(1, 2)));
        Assert.Equal(3, await carrito.AgregarAsync(Linea(1, 4)));   // solo caben 3 más (máximo 5)
        Assert.Equal(0, await carrito.AgregarAsync(Linea(1, 1)));   // ya está al máximo
        await carrito.AgregarAsync(Linea(2, 1, precio: 5m));

        Assert.Equal(6, carrito.Cantidad);
        Assert.Equal(55m, carrito.Total);
        Assert.True(cambios >= 3);
        Assert.Contains("\"ProductoId\":2", js.Guardado);   // se guardó en el almacenamiento
    }

    [Fact]
    public async Task Carrito_CambiaCantidadEntre1YElMaximo_QuitaYVacia()
    {
        var carrito = new CarritoService(new JsFalso());
        await carrito.AgregarAsync(Linea(1, 2));
        await carrito.CambiarCantidadAsync(1, 99);
        Assert.Equal(5, carrito.Lineas.Single().Cantidad);
        await carrito.CambiarCantidadAsync(1, 0);
        Assert.Equal(1, carrito.Lineas.Single().Cantidad);
        await carrito.QuitarAsync(1);
        Assert.Empty(carrito.Lineas);
        await carrito.AgregarAsync(Linea(3));
        await carrito.VaciarAsync();
        Assert.Equal(0, carrito.Cantidad);
    }

    [Fact]
    public async Task Carrito_SeRestauraDelAlmacenamiento_YToleraDatosDañadosOSinAlmacenamiento()
    {
        var js = new JsFalso();
        var primero = new CarritoService(js);
        await primero.AgregarAsync(Linea(1, 3));

        var segundo = new CarritoService(js);   // otra visita: lee lo guardado
        await segundo.InicializarAsync();
        Assert.Equal(3, segundo.Cantidad);

        var dañado = new CarritoService(new JsFalso { Guardado = "esto no es json" });
        await dañado.InicializarAsync();
        Assert.Empty(dañado.Lineas);

        var sinAlmacen = new CarritoService(new JsFalso { Falla = true });
        await sinAlmacen.InicializarAsync();
        await sinAlmacen.AgregarAsync(Linea(1));   // sigue funcionando en memoria
        Assert.Equal(1, sinAlmacen.Cantidad);
    }

    // ---------- Catálogo y producto ----------
    private static (Entorno, TiendaService) Crear(Func<HttpRequestMessage, (HttpStatusCode, object?)> responder)
    {
        var e = new Entorno();
        e.Manejador.Responder = responder;
        return (e, new TiendaService(e.Api));
    }

    [Fact]
    public async Task Catalogo_CargaMenuYResultados_ArmaChipsQuitables_YGuardaElMenuEnMemoria()
    {
        var arbol = new[] { new CategoriaTiendaDto(1, "Ropa", null, 3, new[]
        {
            new SubcategoriaTiendaDto(5, "Camisas", 3, new[] { new FiltroTiendaDto(50, "Color", new[] { new OpcionTiendaDto(500, "Negro") }) }),
        }) };
        var (e, tienda) = Crear(r => r.RequestUri!.AbsolutePath.EndsWith("/tienda/categorias")
            ? (HttpStatusCode.OK, arbol)
            : (HttpStatusCode.OK, new PaginaDto<ProductoTiendaDto>(new[] { Producto(1) }, 1, 1, 12)));
        var vm = new CatalogoViewModel(tienda);

        await vm.CargarAsync(new FiltrosCatalogo(SubcategoriaId: 5, OpcionIds: new[] { 500 }, PrecioMin: 10, SoloDisponibles: true));
        Assert.Equal("Camisas", vm.Titulo);
        Assert.Equal(1, vm.Pagina!.Total);
        var textos = vm.Aplicados.Select(a => a.Texto).ToList();
        Assert.Contains("Camisas", textos);
        Assert.Contains("Color: Negro", textos);
        Assert.Contains("Solo disponibles", textos);

        // Cada chip trae "los filtros sin él": quitar el de opción deja el resto
        var sinNegro = vm.Aplicados.Single(a => a.Texto == "Color: Negro").Sin;
        Assert.Empty(sinNegro.Opciones);
        Assert.Equal(5, sinNegro.SubcategoriaId);

        await vm.CargarAsync(new FiltrosCatalogo(Q: "x"));
        Assert.Equal(1, e.Manejador.Peticiones.Count(p => p.RequestUri!.AbsolutePath.EndsWith("/tienda/categorias")));   // el menú se pidió una sola vez
        Assert.Equal("Resultados para «x»", vm.Titulo);
    }

    [Fact]
    public async Task Catalogo_LasOpcionesSinSubcategoria_SeIgnoran()
    {
        var (_, tienda) = Crear(r => r.RequestUri!.AbsolutePath.EndsWith("/tienda/categorias")
            ? (HttpStatusCode.OK, Array.Empty<CategoriaTiendaDto>())
            : (HttpStatusCode.OK, new PaginaDto<ProductoTiendaDto>(Array.Empty<ProductoTiendaDto>(), 0, 1, 12)));
        var vm = new CatalogoViewModel(tienda);
        await vm.CargarAsync(new FiltrosCatalogo(OpcionIds: new[] { 1, 2 }));
        Assert.Empty(vm.Filtros.Opciones);
        Assert.Equal(0, vm.TotalPaginas);
    }

    [Fact]
    public async Task Producto_Existente_CargaRelacionados_ControlaLaCantidad_YAgregaAlCarrito()
    {
        var (_, tienda) = Crear(r => r.RequestUri!.AbsolutePath.EndsWith("/catalogo/1")
            ? (HttpStatusCode.OK, Producto(1, maximo: 3))
            : (HttpStatusCode.OK, new PaginaDto<ProductoTiendaDto>(new[] { Producto(1), Producto(2, "Otra"), Producto(3, "Agotada", disponible: false) }, 3, 1, 12)));
        var carrito = new CarritoService(new JsFalso());
        var vm = new ProductoViewModel(tienda, carrito);

        await vm.CargarAsync(1);
        Assert.Equal("Camisa", vm.Producto!.Nombre);
        Assert.Equal(new[] { 2 }, vm.Relacionados.Select(p => p.Id));   // sin él mismo ni agotados

        vm.CambiarCantidad(5);
        Assert.Equal(3, vm.Cantidad);   // tope por pedido
        vm.CambiarCantidad(-9);
        Assert.Equal(1, vm.Cantidad);

        vm.CambiarCantidad(2);
        await vm.AgregarAlCarritoAsync();
        Assert.Equal(3, carrito.Cantidad);
        Assert.Contains("Se agregó", vm.Aviso);
        await vm.AgregarAlCarritoAsync();   // ya está al máximo
        Assert.Contains("máximo", vm.Aviso);
    }

    [Fact]
    public async Task Producto_Inexistente_Da404_YSeMuestraComoNoDisponible_SinError()
    {
        var (_, tienda) = Crear(_ => (HttpStatusCode.NotFound, new { title = "El producto no existe.", codigo = "NO_ENCONTRADO" }));
        var vm = new ProductoViewModel(tienda, new CarritoService(new JsFalso()));
        await vm.CargarAsync(999);
        Assert.True(vm.NoEncontrado);
        Assert.Null(vm.Error);
        Assert.Null(vm.Producto);
    }

    // ---------- Registro ----------
    [Fact]
    public async Task Registro_ValidaAntesDeLlamarALaApi()
    {
        var e = new Entorno();
        e.Manejador.Responder = _ => (HttpStatusCode.OK, Array.Empty<DepartamentoDto>());
        var vm = new RegistroViewModel(e.Api, e.Sesion);
        await vm.IniciarAsync();
        var antes = e.Manejador.Peticiones.Count;

        Assert.Null(await vm.RegistrarAsync());
        Assert.Equal("Los nombres es obligatorio.", vm.ErrorDe(nameof(vm.Nombres)));
        Assert.NotNull(vm.ErrorDe(nameof(vm.CiudadId)));
        Assert.NotNull(vm.ErrorDe(nameof(vm.Clave)));
        Assert.Equal(antes, e.Manejador.Peticiones.Count);   // no se llamó a la API
    }

    [Fact]
    public async Task Registro_ClienteExistente_PideConfirmarTelefono_YLuegoCreaLaCuentaYEntra()
    {
        var e = new Entorno();
        var intento = 0;
        var usuario = new UsuarioActualDto(9, "ana@e.com", "Ana Pérez", "Cliente", new[] { "catalogo.ver" });
        e.Manejador.Responder = r =>
        {
            var ruta = r.RequestUri!.AbsolutePath;
            if (ruta.EndsWith("/auth/registro"))
                return ++intento == 1
                    ? (HttpStatusCode.Conflict, new { title = "Encontramos un cliente registrado con este correo.", codigo = CodigosError.ClienteExistente })
                    : (HttpStatusCode.Created, null);
            if (ruta.EndsWith("/auth/login")) return (HttpStatusCode.OK, new SesionDto("tok", e.Reloj.GetUtcNow().AddMinutes(60), false, usuario));
            return (HttpStatusCode.OK, Array.Empty<object>());
        };
        var vm = new RegistroViewModel(e.Api, e.Sesion)
        {
            Nombres = "Ana", Apellidos = "Pérez", Telefono = "78945612", Correo = "ana@e.com", Direccion = "Colonia Escalón",
            Clave = "ClaveSegura_2026", RepetirClave = "ClaveSegura_2026", CiudadId = 1,
        };

        Assert.Null(await vm.RegistrarAsync());
        Assert.True(vm.PedirConfirmacion);
        Assert.Contains("cliente registrado", vm.Error);

        vm.Clave = vm.RepetirClave = "ClaveSegura_2026";   // la pantalla vacía las claves tras cada intento
        vm.TelefonoConfirmacion = "7894-5612";
        var sesion = await vm.RegistrarAsync();
        Assert.NotNull(sesion);
        Assert.True(e.Sesion.Autenticada);
        var cuerpo = e.Manejador.Cuerpos[e.Manejador.Peticiones.FindLastIndex(p => p.RequestUri!.AbsolutePath.EndsWith("/auth/registro"))];
        Assert.Contains("7894-5612", cuerpo);   // el segundo envío lleva el teléfono de confirmación
    }
}

public class PedidoClienteTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 26, 18, 0, 0, TimeSpan.Zero);   // 12:00 en El Salvador

    private static PedidoClienteDto Pedido(int id, string estado = "Pendiente", bool puedeCancelar = true) =>
        new(id, Ahora, new DateOnly(2026, 10, 30), estado, 49.98m, null, puedeCancelar,
            new[] { new LineaPedidoClienteDto(1, "Camisa", 2, 24.99m, 49.98m) });

    private static async Task<(Entorno, CarritoService, PedidoViewModel)> CrearPedidoVm(Func<HttpRequestMessage, (HttpStatusCode, object?)> responder)
    {
        var e = new Entorno();
        e.Manejador.Responder = responder;
        var carrito = new CarritoService(new JsFalso());
        await carrito.AgregarAsync(new LineaCarrito(1, "Camisa", 24.99m, 2, 10, "👕", 200));
        return (e, carrito, new PedidoViewModel(new PedidosClienteService(e.Api), carrito, new Microsoft.Extensions.Time.Testing.FakeTimeProvider(Ahora)));
    }

    [Fact]
    public async Task Pedido_ValidaLaFechaAntesDeLlamarALaApi()
    {
        var (e, _, vm) = await CrearPedidoVm(_ => (HttpStatusCode.OK, Pedido(1)));
        Assert.Equal(new DateOnly(2026, 10, 29), vm.FechaEntrega);   // por defecto: hoy + 3 días (hora de El Salvador)

        vm.FechaEntrega = null;
        Assert.Null(await vm.ConfirmarAsync());
        Assert.Equal("Elija la fecha de entrega.", vm.ErrorFecha);

        vm.FechaEntrega = new DateOnly(2026, 10, 26);   // hoy no vale: debe ser posterior
        Assert.Null(await vm.ConfirmarAsync());
        Assert.Contains("posterior a hoy", vm.ErrorFecha);

        vm.FechaEntrega = new DateOnly(2027, 3, 1);
        Assert.Null(await vm.ConfirmarAsync());
        Assert.Contains("60 días", vm.ErrorFecha);
        Assert.Empty(e.Manejador.Peticiones);   // la API no se tocó
    }

    [Fact]
    public async Task Pedido_Correcto_EnviaSoloProductosYCantidades_YVaciaElCarrito()
    {
        var (e, carrito, vm) = await CrearPedidoVm(_ => (HttpStatusCode.Created, Pedido(7)));
        vm.Comentarios = "  Por la tarde  ";
        var pedido = await vm.ConfirmarAsync();

        Assert.Equal(7, pedido!.Id);
        Assert.Empty(carrito.Lineas);
        var cuerpo = e.Manejador.Cuerpos.Single();
        Assert.Contains("\"productoId\":1", cuerpo);
        Assert.Contains("\"cantidad\":2", cuerpo);
        Assert.Contains("Por la tarde", cuerpo);
        Assert.DoesNotContain("precio", cuerpo, StringComparison.OrdinalIgnoreCase);   // el precio nunca viaja: lo pone el servidor
    }

    [Fact]
    public async Task Pedido_SinStock_MuestraElMotivo_YConservaElCarrito()
    {
        var (_, carrito, vm) = await CrearPedidoVm(_ => (HttpStatusCode.Conflict,
            new { title = "Camisa: solo quedan 1.", codigo = CodigosError.StockInsuficiente }));
        Assert.Null(await vm.ConfirmarAsync());
        Assert.Equal("Camisa: solo quedan 1.", vm.Error);
        Assert.Single(carrito.Lineas);   // no se pierde lo que había elegido
    }

    [Fact]
    public async Task MisPedidos_CargaYCancelaConConfirmacion_YRefrescaElEstado()
    {
        var cancelado = false;
        var e = new Entorno();
        e.Manejador.Responder = r =>
        {
            if (r.Method == HttpMethod.Patch) { cancelado = true; return (HttpStatusCode.OK, Pedido(5, "Cancelado", false)); }
            return (HttpStatusCode.OK, new PaginaDto<PedidoClienteDto>(new[] { cancelado ? Pedido(5, "Cancelado", false) : Pedido(5) }, 1, 1, 5));
        };
        var vm = new MisPedidosViewModel(new PedidosClienteService(e.Api));
        await vm.IniciarAsync();
        Assert.True(vm.Pagina!.Elementos.Single().PuedeCancelar);

        vm.SolicitarCancelacion(vm.Pagina.Elementos[0]);
        vm.DescartarCancelacion();
        Assert.DoesNotContain(e.Manejador.Peticiones, p => p.Method == HttpMethod.Patch);   // descartar no llama a la API

        vm.SolicitarCancelacion(vm.Pagina.Elementos[0]);
        await vm.ConfirmarCancelacionAsync();
        Assert.Null(vm.PorCancelar);
        Assert.Equal("Cancelado", vm.Pagina!.Elementos.Single().Estado);
    }

    [Fact]
    public async Task MisPedidos_SiYaNoEstabaPendiente_ElErrorSeMuestra_YLaListaSeActualiza()
    {
        var e = new Entorno();
        e.Manejador.Responder = r => r.Method == HttpMethod.Patch
            ? (HttpStatusCode.UnprocessableEntity, new { title = "Solo se pueden cancelar los pedidos pendientes: este está entregado.", codigo = "REGLA_DE_NEGOCIO" })
            : (HttpStatusCode.OK, new PaginaDto<PedidoClienteDto>(new[] { Pedido(5, "Entregado", false) }, 1, 1, 5));
        var vm = new MisPedidosViewModel(new PedidosClienteService(e.Api));
        await vm.IniciarAsync();
        vm.SolicitarCancelacion(Pedido(5));
        await vm.ConfirmarCancelacionAsync();
        Assert.Contains("entregado", vm.Error);
        Assert.Equal("Entregado", vm.Pagina!.Elementos.Single().Estado);
    }
}
