using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun;

namespace GestionPedidos.Ui.Tienda;

/// <summary>Un filtro aplicado, para mostrarlo como chip que se puede quitar.</summary>
/// <param name="Texto">Texto del chip.</param>
/// <param name="Sin">Los filtros sin este.</param>
public sealed record FiltroAplicado(string Texto, FiltrosCatalogo Sin);

/// <summary>Estado y lógica del catálogo público: menú de categorías, resultados y filtros aplicados.</summary>
public sealed class CatalogoViewModel : ViewModelBase
{
    private readonly TiendaService _tienda;

    /// <summary>Crea el ViewModel.</summary>
    public CatalogoViewModel(TiendaService tienda) => _tienda = tienda;

    /// <summary>Categorías del menú.</summary>
    public IReadOnlyList<CategoriaTiendaDto> Categorias { get; private set; } = Array.Empty<CategoriaTiendaDto>();
    /// <summary>Filtros que se están aplicando.</summary>
    public FiltrosCatalogo Filtros { get; private set; } = new();
    /// <summary>Página de resultados.</summary>
    public PaginaDto<ProductoTiendaDto>? Pagina { get; private set; }
    /// <summary>Indica si ya hubo una primera carga (para no mostrar "sin resultados" antes de tiempo).</summary>
    public bool Cargado { get; private set; }

    /// <summary>Total de páginas.</summary>
    public int TotalPaginas => Pagina is null ? 0 : (int)Math.Ceiling(Pagina.Total / (double)Pagina.TamanoPagina);

    /// <summary>Subcategoría elegida (si la hay).</summary>
    public SubcategoriaTiendaDto? SubcategoriaElegida => Categorias.SelectMany(c => c.Subcategorias).FirstOrDefault(s => s.Id == Filtros.SubcategoriaId);
    /// <summary>Categoría elegida, directa o por su subcategoría.</summary>
    public CategoriaTiendaDto? CategoriaElegida => Categorias.FirstOrDefault(c => c.Id == Filtros.CategoriaId
        || c.Subcategorias.Any(s => s.Id == Filtros.SubcategoriaId));

    /// <summary>Título de la página según lo elegido.</summary>
    public string Titulo => !string.IsNullOrWhiteSpace(Filtros.Q) ? $"Resultados para «{Filtros.Q}»"
        : SubcategoriaElegida?.Nombre ?? CategoriaElegida?.Nombre ?? "Todos los productos";

    /// <summary>Filtros aplicados como chips que se pueden quitar uno a uno.</summary>
    public IReadOnlyList<FiltroAplicado> Aplicados
    {
        get
        {
            var f = Filtros;
            var lista = new List<FiltroAplicado>();
            if (!string.IsNullOrWhiteSpace(f.Q)) lista.Add(new($"Búsqueda: {f.Q}", f with { Q = null, Pagina = 1 }));
            if (CategoriaElegida is { } c && f.SubcategoriaId is null) lista.Add(new(c.Nombre, f with { CategoriaId = null, Pagina = 1 }));
            if (SubcategoriaElegida is { } s) lista.Add(new(s.Nombre, f with { SubcategoriaId = null, CategoriaId = null, OpcionIds = null, Pagina = 1 }));
            foreach (var o in OpcionesElegidas) lista.Add(new($"{o.Filtro}: {o.Valor}", f.ConOpcion(o.Id, false)));
            if (f.PrecioMin is not null || f.PrecioMax is not null)
                lista.Add(new($"Precio: {(f.PrecioMin is { } a ? TiendaVisual.Dinero(a) : "—")} a {(f.PrecioMax is { } b ? TiendaVisual.Dinero(b) : "—")}",
                    f with { PrecioMin = null, PrecioMax = null, Pagina = 1 }));
            if (f.SoloDisponibles) lista.Add(new("Solo disponibles", f with { SoloDisponibles = false, Pagina = 1 }));
            return lista;
        }
    }

    private IEnumerable<(int Id, string Filtro, string Valor)> OpcionesElegidas =>
        (SubcategoriaElegida?.Filtros ?? Array.Empty<FiltroTiendaDto>())
        .SelectMany(fl => fl.Opciones.Where(o => Filtros.Opciones.Contains(o.Id)).Select(o => (o.Id, fl.Nombre, o.Valor)));

    /// <summary>Carga el menú (la primera vez) y los resultados de los filtros indicados.</summary>
    public async Task CargarAsync(FiltrosCatalogo filtros)
    {
        // Las opciones solo tienen sentido dentro de una subcategoría; si no hay, se ignoran
        Filtros = filtros.SubcategoriaId is null && filtros.Opciones.Count > 0 ? filtros with { OpcionIds = null } : filtros;
        await EjecutarAsync(async ct =>
        {
            Categorias = await _tienda.CategoriasAsync(ct);
            Pagina = await _tienda.BuscarAsync(Filtros, ct);
        });
        Cargado = true;
        OnCambiado();
    }
}
