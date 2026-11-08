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
}