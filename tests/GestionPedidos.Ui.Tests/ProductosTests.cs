using System.Net;
using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun.Eventos;
using GestionPedidos.Ui.Modulos.Productos;

namespace GestionPedidos.Ui.Tests;

/// <summary>Pruebas del servicio y del ViewModel de Productos (interfaz y eventos).</summary>
public class ProductosTests
{
    private static ProductoDto Producto(int id, bool activo = true, int stock = 10) =>
        new(id, $"PRD-{id:D6}", $"Producto {id}", "Descripción de prueba", 25.50m, stock,
            DateOnly.FromDateTime(DateTime.Today), 1, "Tecnología", 1, "Laptops", false, activo, null, null, "AAAA");

    private static (Entorno entorno, ProductosService servicio) Crear()
    {
        var e = new Entorno();
        e.Manejador.Responder = r =>
        {
            var ruta = r.RequestUri!.AbsolutePath;
            if (ruta.EndsWith("/categorias"))
                return (HttpStatusCode.OK, new[] { new CategoriaDto(1, "Tecnología", null, true, null, "AAAA", new[] { new SubcategoriaDto(1, 1, "Laptops", null, true, null, "AAAA", 0, 10) }) });
            if (ruta.Contains("/subcategorias/") && ruta.EndsWith("/filtros"))
                return (HttpStatusCode.OK, new[] { new FiltroDto(1, 1, "Marca", true, null, "AAAA", new[] { new OpcionFiltroDto(1, 1, "HP", true, null, "AAAA") }) });
            if (r.Method == HttpMethod.Get)
                return (HttpStatusCode.OK, new PaginaDto<ProductoDto>(new[] { Producto(1), Producto(2, stock: 2) }, 2, 1, 10));

            return (HttpStatusCode.OK, Producto(3));
        };
        return (e, new ProductosService(e.Api));
    }

    // ---- Pruebas de ProductosService (publicadora de eventos) ----

    [Fact]
    public async Task Servicio_PublicaEntidadCambiada_SoloTrasConfirmarLaApi()
    {
        var (e, servicio) = Crear();
        var eventos = new List<EntidadCambiadaEventArgs>();
        servicio.EntidadCambiada += (s, a) => { Assert.Same(servicio, s); eventos.Add(a); };

        var solicitud = new CrearProductoSolicitud("Laptop Asus", "Core i7 16GB", 799.99m, 5,
            DateOnly.FromDateTime(DateTime.Today), 1, false);

        await servicio.CrearAsync(solicitud, default);

        var ev = Assert.Single(eventos);
        Assert.Equal(TipoEntidad.Producto, ev.Entidad);
        Assert.Equal(TipoCambio.Registrada, ev.Tipo);

        // Si la API falla, no se debe publicar ningún evento
        e.Manejador.Responder = _ => (HttpStatusCode.UnprocessableEntity, new { title = "Regla violada", codigo = "REGLA" });
        await Assert.ThrowsAsync<GestionPedidos.Ui.Comun.Api.ApiException>(() => servicio.CambiarEstadoAsync(1, false, default));
        Assert.Single(eventos); // Se mantiene solo el evento anterior
    }

    [Fact]
    public async Task Servicio_SinSuscriptores_NoLanzaExcepcion()
    {
        var (_, servicio) = Crear();
        await servicio.CambiarEstadoAsync(1, false, default);
    }

    // ---- Pruebas de ProductosViewModel (patrón MVVM y eventos de UI) ----

    [Fact]
    public async Task ViewModel_IniciarAsync_CargaCategoriasYResultados()
    {
        var (_, servicio) = Crear();
        using var vm = new ProductosViewModel(servicio);

        var cambios = 0;
        vm.Cambiado += (_, _) => cambios++;

        await vm.IniciarAsync();

        Assert.NotEmpty(vm.Categorias);
        Assert.NotNull(vm.Pagina);
        Assert.Equal(2, vm.Pagina.Elementos.Count);
        Assert.True(cambios > 0);
    }

    [Fact]
    public async Task ViewModel_Nuevo_PreparaFormularioYNotificaCambio()
    {
        var (_, servicio) = Crear();
        using var vm = new ProductosViewModel(servicio);

        var cambioNotificado = false;
        vm.Cambiado += (_, _) => cambioNotificado = true;

        vm.Nuevo();

        Assert.True(vm.Editando);
        Assert.Null(vm.Original);
        Assert.Empty(vm.Nombre);
        Assert.True(cambioNotificado);
    }

    [Fact]
    public async Task ViewModel_GuardarAsync_ConCamposInvalidos_NoLlamaALaApi()
    {
        var (_, servicio) = Crear();
        using var vm = new ProductosViewModel(servicio);

        vm.Nuevo();
        vm.Nombre = ""; // inválido
        vm.PrecioTexto = "-50"; // inválido

        await vm.GuardarAsync();

        Assert.True(vm.Editando);
        Assert.NotNull(vm.ErrorDe(nameof(vm.Nombre)));
        Assert.NotNull(vm.ErrorDe(nameof(vm.PrecioTexto)));
    }

    [Fact]
    public async Task ViewModel_SolicitarCambioEstado_Y_Confirmar_CambiaElEstado()
    {
        var (_, servicio) = Crear();
        using var vm = new ProductosViewModel(servicio);

        var producto = Producto(1, activo: true);
        vm.SolicitarCambioEstado(producto);

        Assert.True(vm.HayConfirmacion);
        Assert.Equal(AccionProducto.DarDeBaja, vm.Accion);
        Assert.True(vm.EsPeligro);

        await vm.ConfirmarAsync();

        Assert.False(vm.HayConfirmacion);
        Assert.Null(vm.Pendiente);
    }

    [Fact]
    public async Task ViewModel_CambioDeSubcategoria_CargaFiltrosDinamicos()
    {
        var (_, servicio) = Crear();
        using var vm = new ProductosViewModel(servicio);

        await vm.AlCambiarSubcategoriaFormularioAsync(1);

        Assert.NotEmpty(vm.FiltrosSubcategoria);
        Assert.Equal("Marca", vm.FiltrosSubcategoria[0].Nombre);
    }
}
