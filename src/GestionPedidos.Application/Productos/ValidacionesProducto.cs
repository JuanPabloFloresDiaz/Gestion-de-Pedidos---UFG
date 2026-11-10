using GestionPedidos.Application.Common;

namespace GestionPedidos.Application.Productos;

/// <summary>Reglas de campos del producto compartidas por el alta y la edición (Pautas §11).</summary>
internal static class ValidacionesProducto
{
    /// <summary>Devuelve el mensaje del primer error, o <c>null</c> si todo es válido.</summary>
    public static string? Validar(string nombre, string descripcion, decimal precio, int stock, DateOnly fechaIngreso, DateOnly hoy)
    {
        var error = Validaciones.Texto(nombre, "El nombre del producto", 150)
                 ?? Validaciones.Texto(descripcion, "La descripción", 500);

        if (error is not null) return error;

        if (precio < 0)
            return "El precio de venta no puede ser negativo.";

        if (precio > 9_999_999_999.99m || decimal.Round(precio, 2) != precio)
            return "El precio debe tener como máximo 2 decimales y caber en el sistema.";

        if (stock < 0)
            return "El stock no puede ser negativo.";

        if (fechaIngreso > hoy)
            return "La fecha de ingreso no puede ser futura.";

        return null;
    }

    /// <summary>
    /// Valida la clasificación pedida (subcategoría y opciones de filtro) y devuelve los pares (filtro, opción) a guardar.
    /// Rechaza con un mensaje claro lo que antes terminaba en error 500 o se descartaba en silencio: subcategoría inexistente
    /// o inactiva, opciones que no existen o son de otra subcategoría, dos opciones del mismo filtro y opciones sin subcategoría.
    /// </summary>
    public static async Task<(string? Error, IReadOnlyList<(int FiltroId, int OpcionId)> Opciones)> ResolverClasificacionAsync(
        IProductoRepository productos, int? subcategoriaId, IReadOnlyList<int>? opcionIds, CancellationToken ct)
    {
        var pedidas = (opcionIds ?? Array.Empty<int>()).Distinct().ToList();

        if (subcategoriaId is null)
            return pedidas.Count > 0
                ? ("No se pueden elegir opciones de filtro sin una subcategoría.", Array.Empty<(int, int)>())
                : (null, Array.Empty<(int, int)>());

        if (!await productos.ExisteSubcategoriaActivaAsync(subcategoriaId.Value, ct))
            return ("La subcategoría seleccionada no existe o está inactiva.", Array.Empty<(int, int)>());

        if (pedidas.Count == 0)
            return (null, Array.Empty<(int, int)>());

        var validas = await productos.ObtenerOpcionesValidasAsync(subcategoriaId.Value, pedidas, ct);
        if (validas.Count != pedidas.Count)
            return ("Una o más opciones de filtro no existen o no pertenecen a la subcategoría elegida.", Array.Empty<(int, int)>());

        if (validas.GroupBy(v => v.FiltroId).Any(g => g.Count() > 1))
            return ("Solo se puede elegir una opción por filtro.", Array.Empty<(int, int)>());

        return (null, validas);
    }

    /// <summary>
    /// Interpreta la versión (base64) que manda el cliente para la concurrencia optimista. Es obligatoria al editar.
    /// </summary>
    public static (string? Error, byte[] Version) LeerVersion(string? rowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersion))
            return ("Falta la versión del producto (RowVersion). Recargue los datos e intente de nuevo.", Array.Empty<byte>());

        var buffer = new byte[rowVersion.Length];
        return Convert.TryFromBase64String(rowVersion, buffer, out var escritos) && escritos > 0
            ? (null, buffer[..escritos])
            : ("La versión del producto (RowVersion) no es válida. Recargue los datos e intente de nuevo.", Array.Empty<byte>());
    }
}
