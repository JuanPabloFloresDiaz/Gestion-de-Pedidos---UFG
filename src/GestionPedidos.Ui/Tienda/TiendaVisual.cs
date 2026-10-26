using Blazicons;

namespace GestionPedidos.Ui.Tienda;

/// <summary>
/// Apariencia de los productos cuando no tienen foto: cada categoría tiene un ícono y un tono pastel (como las tarjetas de las
/// tiendas modernas). Con foto (almacén de imágenes) se muestra la foto y esto queda como respaldo.
/// </summary>
public static class TiendaVisual
{
    private static readonly (string Palabra, SvgIcon Icono)[] Iconos =
    {
        ("camisa", Lucide.Shirt), ("pantalon", Lucide.Tag), ("calzado", Lucide.Footprints), ("chaqueta", Lucide.Wind), ("complemento", Lucide.Glasses),
        ("ropa de cama", Lucide.BedDouble), ("ropa", Lucide.Shirt),
        ("celular", Lucide.Smartphone), ("laptop", Lucide.Laptop), ("audio", Lucide.Headphones), ("accesorio", Lucide.Plug), ("tecnolog", Lucide.Cpu),
        ("electrodom", Lucide.Blend), ("utensilio", Lucide.CookingPot), ("decoraci", Lucide.Lamp), ("hogar", Lucide.Sofa),
        ("fútbol", Lucide.Goal), ("futbol", Lucide.Goal), ("fitness", Lucide.Dumbbell), ("ciclismo", Lucide.Bike), ("running", Lucide.Mountain), ("deporte", Lucide.Trophy),
        ("piel", Lucide.Droplets), ("cabello", Lucide.Scissors), ("fragancia", Lucide.SprayCan), ("belleza", Lucide.Sparkles),
        ("cuaderno", Lucide.Notebook), ("escritura", Lucide.PenLine), ("mochila", Lucide.Backpack), ("papeler", Lucide.NotebookPen),
        ("mascota", Lucide.PawPrint), ("alimento", Lucide.ShoppingBasket),
    };

    /// <summary>Ícono para una subcategoría o categoría (busca por palabras clave en el nombre; la subcategoría manda).</summary>
    public static SvgIcon Icono(string? subcategoria, string? categoria)
    {
        foreach (var texto in new[] { subcategoria, categoria })
        {
            if (string.IsNullOrEmpty(texto)) continue;
            var t = texto.ToLowerInvariant();
            foreach (var (palabra, icono) in Iconos) if (t.Contains(palabra)) return icono;
        }
        return Lucide.ShoppingBag;
    }

    /// <summary>Tono (0–359) de la categoría; el mismo id siempre da el mismo color.</summary>
    public static int Tono(int? categoriaId) => categoriaId is null ? 215 : (categoriaId.Value * 47 + 20) % 360;

    /// <summary>Precio con formato monetario ($ y dos decimales).</summary>
    public static string Dinero(decimal valor) => valor.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
}
