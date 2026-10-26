namespace GestionPedidos.Ui.Tienda;

/// <summary>
/// Apariencia de los productos sin imágenes: cada categoría tiene un ícono y un tono pastel (como las tarjetas de las
/// tiendas modernas). Es solo presentación; si más adelante hubiera fotos, bastaría reemplazar este componente.
/// </summary>
public static class TiendaVisual
{
    private static readonly (string Palabra, string Icono)[] Iconos =
    {
        ("camisa", "👕"), ("pantalon", "👖"), ("calzado", "👟"), ("chaqueta", "🧥"), ("complemento", "🧢"), ("ropa de cama", "🛏️"), ("ropa", "👗"),
        ("celular", "📱"), ("laptop", "💻"), ("audio", "🎧"), ("accesorio", "🔌"), ("tecnolog", "📱"),
        ("electrodom", "🍳"), ("utensilio", "🥘"), ("decoraci", "🪴"), ("hogar", "🏠"),
        ("fútbol", "⚽"), ("futbol", "⚽"), ("fitness", "🏋️"), ("ciclismo", "🚲"), ("running", "🏃"), ("deporte", "🏅"),
        ("piel", "🧴"), ("cabello", "💇"), ("fragancia", "🌸"), ("belleza", "💄"),
        ("cuaderno", "📓"), ("escritura", "✏️"), ("mochila", "🎒"), ("papeler", "📚"),
        ("mascota", "🐾"), ("alimento", "🥫"),
    };

    /// <summary>Ícono para una subcategoría o categoría (busca por palabras clave en el nombre).</summary>
    public static string Icono(string? subcategoria, string? categoria)
    {
        foreach (var texto in new[] { subcategoria, categoria })
        {
            if (string.IsNullOrEmpty(texto)) continue;
            var t = texto.ToLowerInvariant();
            foreach (var (palabra, icono) in Iconos) if (t.Contains(palabra)) return icono;
        }
        return "🛍️";
    }

    /// <summary>Tono (0–359) de la categoría; el mismo id siempre da el mismo color.</summary>
    public static int Tono(int? categoriaId) => categoriaId is null ? 215 : (categoriaId.Value * 47 + 20) % 360;

    /// <summary>Precio con formato monetario ($ y dos decimales).</summary>
    public static string Dinero(decimal valor) => valor.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
}
