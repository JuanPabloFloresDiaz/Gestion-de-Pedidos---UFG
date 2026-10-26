using System.Globalization;

namespace GestionPedidos.Ui.Tienda;

/// <summary>
/// Filtros del catálogo. La dirección de la página (<c>/tienda?...</c>) es la fuente de verdad: así los filtros se pueden
/// compartir, el botón "atrás" los conserva y recargar no los pierde.
/// </summary>
/// <param name="Q">Texto buscado.</param>
/// <param name="CategoriaId">Categoría.</param>
/// <param name="SubcategoriaId">Subcategoría.</param>
/// <param name="OpcionIds">Opciones de filtro marcadas.</param>
/// <param name="PrecioMin">Precio mínimo.</param>
/// <param name="PrecioMax">Precio máximo.</param>
/// <param name="SoloDisponibles">Oculta los agotados.</param>
/// <param name="Orden">Orden elegido.</param>
/// <param name="Pagina">Página.</param>
public sealed record FiltrosCatalogo(string? Q = null, int? CategoriaId = null, int? SubcategoriaId = null, IReadOnlyList<int>? OpcionIds = null,
    decimal? PrecioMin = null, decimal? PrecioMax = null, bool SoloDisponibles = false, string? Orden = null, int Pagina = 1)
{
    /// <summary>Tamaño de página del catálogo.</summary>
    public const int Tamano = 12;

    /// <summary>Opciones marcadas (nunca nulo).</summary>
    public IReadOnlyList<int> Opciones => OpcionIds ?? Array.Empty<int>();

    /// <summary>Indica si hay algún filtro aplicado además del orden y la página.</summary>
    public bool HayFiltros => !string.IsNullOrWhiteSpace(Q) || CategoriaId is not null || SubcategoriaId is not null || Opciones.Count > 0
        || PrecioMin is not null || PrecioMax is not null || SoloDisponibles;

    /// <summary>Lee los filtros de los parámetros de la dirección (texto); lo inválido se ignora.</summary>
    public static FiltrosCatalogo Desde(string? q, string? categoria, string? sub, string? opciones, string? min, string? max, string? disponibles,
        string? orden, string? pagina) => new(
        string.IsNullOrWhiteSpace(q) ? null : q.Trim(), Entero(categoria), Entero(sub),
        (opciones ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Entero).OfType<int>().Distinct().ToList(),
        Decimal(min), Decimal(max), disponibles is "1" or "true", string.IsNullOrWhiteSpace(orden) ? null : orden, Math.Max(Entero(pagina) ?? 1, 1));

    private static int? Entero(string? t) => int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
    private static decimal? Decimal(string? t) => decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) && n >= 0 ? n : null;

    /// <summary>Cadena de consulta. Para la dirección de la página usa nombres cortos; para la API, los nombres de sus parámetros.</summary>
    public string ToQuery(bool paraApi = false)
    {
        var p = new List<string>();
        void A(string k, object? v) { if (v is not null) p.Add($"{k}={Uri.EscapeDataString(Convert.ToString(v, CultureInfo.InvariantCulture)!)}"); }
        A("q", string.IsNullOrWhiteSpace(Q) ? null : Q);
        A(paraApi ? "categoriaId" : "categoria", CategoriaId);
        A(paraApi ? "subcategoriaId" : "sub", SubcategoriaId);
        if (Opciones.Count > 0) A(paraApi ? "opcionIds" : "op", string.Join(',', Opciones));
        A(paraApi ? "precioMin" : "min", PrecioMin);
        A(paraApi ? "precioMax" : "max", PrecioMax);
        if (SoloDisponibles) A(paraApi ? "soloDisponibles" : "disp", paraApi ? "true" : "1");
        A("orden", Orden);
        if (Pagina > 1 || paraApi) A(paraApi ? "pagina" : "pag", Pagina);
        if (paraApi) A("tamanoPagina", Tamano);
        return string.Join('&', p);
    }

    /// <summary>Dirección de la página con estos filtros.</summary>
    public string Url() => ToQuery() is { Length: > 0 } q ? $"/tienda?{q}" : "/tienda";

    /// <summary>Marca o desmarca una opción y vuelve a la primera página.</summary>
    public FiltrosCatalogo ConOpcion(int opcionId, bool marcada) => this with
    {
        OpcionIds = marcada ? Opciones.Append(opcionId).Distinct().ToList() : Opciones.Where(o => o != opcionId).ToList(), Pagina = 1,
    };
}
