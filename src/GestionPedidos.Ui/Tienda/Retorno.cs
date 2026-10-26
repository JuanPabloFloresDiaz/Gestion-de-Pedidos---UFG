namespace GestionPedidos.Ui.Tienda;

/// <summary>Lee el parámetro <c>retorno</c> de una dirección (a dónde volver tras ingresar), aceptando solo rutas internas.</summary>
public static class Retorno
{
    /// <summary>Ruta interna pedida en <c>?retorno=</c>, o <c>null</c>. Rechaza direcciones externas para evitar redirecciones abiertas.</summary>
    public static string? Desde(string uri)
    {
        var valor = System.Web.HttpUtility.ParseQueryString(new Uri(uri).Query)["retorno"];
        return EsRutaInterna(valor) ? valor : null;
    }

    /// <summary>Indica si es una ruta de este mismo sitio (empieza con una sola barra).</summary>
    public static bool EsRutaInterna(string? ruta) =>
        !string.IsNullOrEmpty(ruta) && ruta[0] == '/' && !ruta.StartsWith("//", StringComparison.Ordinal) && !ruta.StartsWith("/\\", StringComparison.Ordinal);
}
