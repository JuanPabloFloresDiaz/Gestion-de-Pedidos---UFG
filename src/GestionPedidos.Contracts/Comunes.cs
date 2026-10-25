namespace GestionPedidos.Contracts;

/// <summary>Página de resultados de un listado.</summary>
/// <typeparam name="T">Tipo de cada elemento.</typeparam>
/// <param name="Elementos">Elementos de la página.</param>
/// <param name="Total">Total de elementos que cumplen el filtro.</param>
/// <param name="Pagina">Número de página (desde 1).</param>
/// <param name="TamanoPagina">Elementos por página.</param>
public sealed record PaginaDto<T>(IReadOnlyList<T> Elementos, int Total, int Pagina, int TamanoPagina)
{
    /// <summary>Total de páginas.</summary>
    public int TotalPaginas => TamanoPagina <= 0 ? 0 : (int)Math.Ceiling(Total / (double)TamanoPagina);
}

/// <summary>Límites de paginación comunes a todos los listados.</summary>
public static class Paginacion
{
    /// <summary>Tamaño de página por defecto.</summary>
    public const int TamanoPorDefecto = 20;
    /// <summary>Tamaño máximo permitido.</summary>
    public const int TamanoMaximo = 100;

    /// <summary>Normaliza página y tamaño a valores válidos.</summary>
    public static (int Pagina, int Tamano) Normalizar(int? pagina, int? tamano) =>
        (Math.Max(pagina ?? 1, 1), Math.Clamp(tamano ?? TamanoPorDefecto, 1, TamanoMaximo));
}
