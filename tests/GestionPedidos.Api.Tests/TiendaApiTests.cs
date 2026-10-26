using System.Net;
using System.Net.Http.Json;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Catalogo;
using GestionPedidos.Domain.Productos;
using GestionPedidos.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace GestionPedidos.Api.Tests;

/// <summary>Catálogo público: anónimo, solo ve lo activo, no expone datos internos y filtra con Y/O.</summary>
[Collection(ApiCollection.Nombre)]
public class TiendaApiTests
{
    private readonly ApiFixture _api;
    public TiendaApiTests(ApiFixture api) => _api = api;

    private sealed record Mundo(int CategoriaId, int SubId, int ColorId, int TallaId, int Negro, int Rojo, int M, int L,
        int Visible, int Agotado, int ConStock, int Inactivo, int SinClasificar, string Marca);

    /// <summary>Crea una categoría con una subcategoría, dos filtros (Color y Talla) y cuatro productos de ejemplo.</summary>
    private async Task<Mundo> SembrarAsync()
    {
        var marca = "Tst" + Guid.NewGuid().ToString("N")[..8];   // distingue estos productos de los de otras pruebas
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GestionPedidosDbContext>();
        var cat = new Categoria { Nombre = marca };
        var sub = new Subcategoria { Categoria = cat, Nombre = "Camisas" };
        var color = new Filtro { Subcategoria = sub, Nombre = "Color" };
        var talla = new Filtro { Subcategoria = sub, Nombre = "Talla" };
        var negro = new OpcionFiltro { Filtro = color, Valor = "Negro" };
        var rojo = new OpcionFiltro { Filtro = color, Valor = "Rojo" };
        var m = new OpcionFiltro { Filtro = talla, Valor = "M" };
        var l = new OpcionFiltro { Filtro = talla, Valor = "L" };
        db.AddRange(cat, sub, color, talla, negro, rojo, m, l);
        await db.SaveChangesAsync();

        Producto P(string nombre, decimal precio, int stock, bool mostrar, params (Filtro, OpcionFiltro)[] detalle)
        {
            var p = new Producto { Nombre = $"{marca} {nombre}", Descripcion = "Descripción " + nombre, PrecioVenta = precio, CantidadStock = stock,
                MostrarStock = mostrar, FechaIngreso = DateOnly.FromDateTime(DateTime.UtcNow) };
            p.EstablecerClasificacion(sub.Id, detalle.Select(d => (d.Item1.Id, d.Item2.Id)));
            return p;
        }
        var visible = P("Negra M", 20, 50, false, (color, negro), (talla, m));   // stock oculto
        var agotado = P("Roja L", 30, 0, false, (color, rojo), (talla, l));
        var conStock = P("Negra L", 40, 7, true, (color, negro), (talla, l));    // muestra existencias
        var inactivo = P("Inactiva", 10, 5, false, (color, rojo));
        inactivo.Activo = false;
        var sinClasificar = new Producto { Nombre = $"{marca} Suelto", Descripcion = "Sin clasificar", PrecioVenta = 5, CantidadStock = 3 };
        db.Productos.AddRange(visible, agotado, conStock, inactivo, sinClasificar);
        await db.SaveChangesAsync();
        return new(cat.Id, sub.Id, color.Id, talla.Id, negro.Id, rojo.Id, m.Id, l.Id, visible.Id, agotado.Id, conStock.Id, inactivo.Id, sinClasificar.Id, marca);
    }

    private static async Task<PaginaDto<ProductoTiendaDto>> Buscar(HttpClient c, string consulta) =>
        (await c.GetFromJsonAsync<PaginaDto<ProductoTiendaDto>>($"/api/v1/catalogo?{consulta}"))!;

    [Fact]
    public async Task Catalogo_EsAnonimo_YNoMuestraInactivos_NiExponeStockOculto()
    {
        var w = await SembrarAsync();
        var anonimo = _api.CreateClient();   // sin token

        var pagina = await Buscar(anonimo, $"q={w.Marca}&tamanoPagina=48");
        Assert.DoesNotContain(pagina.Elementos, p => p.Id == w.Inactivo);
        Assert.Equal(4, pagina.Total);   // visible, agotado, con stock y sin clasificar

        var oculto = pagina.Elementos.Single(p => p.Id == w.Visible);
        Assert.True(oculto.Disponible);
        Assert.Null(oculto.Existencias);               // el administrador no activó "mostrar existencias"
        Assert.Equal(10, oculto.MaximoPorPedido);      // tope por pedido, no el stock real (50)
        var expuesto = pagina.Elementos.Single(p => p.Id == w.ConStock);
        Assert.Equal(7, expuesto.Existencias);
        Assert.Equal(7, expuesto.MaximoPorPedido);
        Assert.False(pagina.Elementos.Single(p => p.Id == w.Agotado).Disponible);
        Assert.Contains(oculto.Detalles, d => d is { Filtro: "Color", Valor: "Negro" });
    }

    [Fact]
    public async Task Catalogo_FiltraPorOpciones_YEntreFiltros_OEnLasDelMismoFiltro()
    {
        var w = await SembrarAsync();
        var c = _api.CreateClient();
        async Task<List<int>> Ids(string extra) => (await Buscar(c, $"q={w.Marca}&tamanoPagina=48&{extra}")).Elementos.Select(p => p.Id).ToList();

        // Mismo filtro (Color): Negro O Rojo → los tres con color (el inactivo no se ve)
        var color = await Ids($"opcionIds={w.Negro},{w.Rojo}");
        Assert.Equal(new[] { w.Visible, w.Agotado, w.ConStock }.OrderBy(x => x), color.OrderBy(x => x));

        // Filtros distintos: Negro Y L → solo la negra L
        Assert.Equal(new[] { w.ConStock }, await Ids($"opcionIds={w.Negro},{w.L}"));
        // Solo disponibles + Rojo → nada (la roja está agotada)
        Assert.Empty(await Ids($"opcionIds={w.Rojo}&soloDisponibles=true"));
        // Subcategoría y rango de precio
        Assert.Equal(new[] { w.ConStock }, await Ids($"subcategoriaId={w.SubId}&precioMin=35&precioMax=45"));
        // Opciones inexistentes o mal escritas no rompen nada
        Assert.NotNull(await Buscar(c, "opcionIds=abc,,999999"));
    }

    [Fact]
    public async Task Catalogo_Ordena_Pagina_YValidaPrecios()
    {
        var w = await SembrarAsync();
        var c = _api.CreateClient();
        var asc = (await Buscar(c, $"q={w.Marca}&orden=precio-asc&tamanoPagina=48")).Elementos.Select(p => p.Precio).ToList();
        Assert.Equal(asc.OrderBy(x => x), asc);
        var desc = (await Buscar(c, $"q={w.Marca}&orden=precio-desc&tamanoPagina=48")).Elementos.Select(p => p.Precio).ToList();
        Assert.Equal(desc.OrderByDescending(x => x), desc);

        var primera = await Buscar(c, $"q={w.Marca}&tamanoPagina=2&pagina=1");
        var segunda = await Buscar(c, $"q={w.Marca}&tamanoPagina=2&pagina=2");
        Assert.Equal(2, primera.Elementos.Count);
        Assert.Empty(primera.Elementos.Select(p => p.Id).Intersect(segunda.Elementos.Select(p => p.Id)));
        Assert.Equal(48, (await Buscar(c, "tamanoPagina=5000")).TamanoPagina);   // tope del tamaño de página

        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/v1/catalogo?precioMin=50&precioMax=10")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/v1/catalogo?precioMin=-1")).StatusCode);
    }

    [Fact]
    public async Task Detalle_DeUnoVisible_Da200_DeUnoInactivoOInexistente_Da404()
    {
        var w = await SembrarAsync();
        var c = _api.CreateClient();
        var p = (await c.GetFromJsonAsync<ProductoTiendaDto>($"/api/v1/catalogo/{w.Visible}"))!;
        Assert.Equal(w.SubId, p.SubcategoriaId);
        Assert.Equal(2, p.Detalles.Count);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/v1/catalogo/{w.Inactivo}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/v1/catalogo/99999999")).StatusCode);
    }

    [Fact]
    public async Task Categorias_SoloActivas_ConFiltrosYConteosDeProductosDisponibles()
    {
        var w = await SembrarAsync();
        var c = _api.CreateClient();
        var arbol = (await c.GetFromJsonAsync<List<CategoriaTiendaDto>>("/api/v1/tienda/categorias"))!;
        var mia = arbol.Single(x => x.Id == w.CategoriaId);
        var sub = mia.Subcategorias.Single();
        Assert.Equal(2, sub.TotalProductos);   // con stock y visibles: la negra M y la negra L (la roja está agotada)
        Assert.Equal(new[] { "Color", "Talla" }, sub.Filtros.Select(f => f.Nombre));
        Assert.Equal(new[] { "Negro", "Rojo" }, sub.Filtros.Single(f => f.Nombre == "Color").Opciones.Select(o => o.Valor));

        // Una categoría dada de baja desaparece del menú y sus productos del catálogo
        // La subcategoría tiene productos activos (la regla de la API impediría la baja): se da de baja directo en la base
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GestionPedidosDbContext>();
        var s = await db.Subcategorias.FindAsync(w.SubId);
        s!.CambiarEstado(false, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
        var despues = (await c.GetFromJsonAsync<List<CategoriaTiendaDto>>("/api/v1/tienda/categorias"))!;
        Assert.Empty(despues.Single(x => x.Id == w.CategoriaId).Subcategorias);
        Assert.DoesNotContain((await Buscar(c, $"q={w.Marca}&tamanoPagina=48")).Elementos, p => p.Id == w.Visible);
    }
}
