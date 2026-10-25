namespace GestionPedidos.Application.Autenticacion;

/// <summary>Política de contraseñas ([AutenticacionYPermisos.md §2]): 10 a 128 caracteres y distinta del usuario y del correo.</summary>
public static class PoliticaClave
{
    /// <summary>Largo mínimo.</summary>
    public const int Minimo = 10;
    /// <summary>Largo máximo.</summary>
    public const int Maximo = 128;

    /// <summary>Valida una contraseña.</summary>
    /// <param name="clave">Contraseña propuesta.</param>
    /// <param name="nombreUsuario">Nombre de usuario, que no puede usarse como clave.</param>
    /// <param name="correo">Correo, que no puede usarse como clave.</param>
    /// <returns>Mensaje de error, o <c>null</c> si cumple la política.</returns>
    public static string? Validar(string? clave, string? nombreUsuario = null, string? correo = null)
    {
        if (string.IsNullOrEmpty(clave) || clave.Length < Minimo)
            return $"La contraseña debe tener al menos {Minimo} caracteres.";
        if (clave.Length > Maximo)
            return $"La contraseña no puede superar {Maximo} caracteres.";
        if (Igual(clave, nombreUsuario) || Igual(clave, correo))
            return "La contraseña no puede ser igual al usuario ni al correo.";
        return null;
    }

    private static bool Igual(string clave, string? otro) =>
        !string.IsNullOrWhiteSpace(otro) && string.Equals(clave, otro.Trim(), StringComparison.OrdinalIgnoreCase);
}
