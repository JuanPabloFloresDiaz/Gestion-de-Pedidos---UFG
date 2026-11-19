namespace GestionPedidos.Application.Reportes;

/// <summary>
/// Colores de acento de los reportes. Son los mismos siete que ofrece la pantalla de apariencia (los valores salen de
/// <c>tema.css</c>, variante clara): el reporte toma el color que la persona tiene elegido en la aplicación.
/// </summary>
public static class AcentosDeReporte
{
    /// <summary>Acento por defecto (el mismo de la aplicación).</summary>
    public const string PorDefecto = "azul";

    private static readonly IReadOnlyDictionary<string, string> Colores = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["azul"] = "#2F5BEA",
        ["turquesa"] = "#0F766E",
        ["verde"] = "#15803D",
        ["ambar"] = "#B45309",
        ["rosa"] = "#BE185D",
        ["violeta"] = "#6D28D9",
        ["grafito"] = "#374151",
    };

    /// <summary>Identificadores válidos, para el mensaje de error.</summary>
    public static IEnumerable<string> Ids => Colores.Keys;

    /// <summary>Indica si el acento existe (vacío o <c>null</c> se acepta: se usa el de por defecto).</summary>
    public static bool EsValido(string? id) => string.IsNullOrWhiteSpace(id) || Colores.ContainsKey(id.Trim().ToLowerInvariant());

    /// <summary>Color en hexadecimal (<c>#RRGGBB</c>) del acento; si no existe o viene vacío, el de por defecto.</summary>
    public static string Color(string? id) =>
        id is not null && Colores.TryGetValue(id.Trim().ToLowerInvariant(), out var c) ? c : Colores[PorDefecto];

    /// <summary>Aclara un color mezclándolo con blanco (<paramref name="cantidad"/> entre 0 y 1; 1 = blanco): sirve para fondos suaves.</summary>
    public static string Aclarar(string hex, double cantidad)
    {
        var r = Convert.ToInt32(hex.Substring(1, 2), 16);
        var g = Convert.ToInt32(hex.Substring(3, 2), 16);
        var b = Convert.ToInt32(hex.Substring(5, 2), 16);
        int Mezcla(int x) => (int)Math.Round(x + (255 - x) * cantidad);
        return $"#{Mezcla(r):X2}{Mezcla(g):X2}{Mezcla(b):X2}";
    }
}
