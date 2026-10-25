namespace GestionPedidos.Ui.Comun;

/// <summary>Regla de validación: devuelve el mensaje de error o <c>null</c> si es válida.</summary>
/// <typeparam name="T">Tipo del valor validado.</typeparam>
/// <param name="valor">Valor a validar.</param>
public delegate string? ReglaDeValidacion<in T>(T valor);

/// <summary>Reglas componibles de validación (usan delegados como parámetro, según el material de la materia).</summary>
public static class Validacion
{
    /// <summary>Aplica todas las reglas y devuelve el primer error, o <c>null</c> si todas pasan.</summary>
    public static string? Primero<T>(T valor, params ReglaDeValidacion<T>[] reglas) =>
        reglas.Select(regla => regla(valor)).OfType<string>().FirstOrDefault();

    /// <summary>Regla: texto obligatorio con un largo máximo.</summary>
    public static ReglaDeValidacion<string?> Texto(string campo, int maximo, int minimo = 1) => v =>
    {
        var t = v?.Trim() ?? "";
        if (t.Length < minimo) return minimo == 1 ? $"{campo} es obligatorio." : $"{campo} debe tener al menos {minimo} caracteres.";
        return t.Length > maximo ? $"{campo} no puede superar {maximo} caracteres." : null;
    };

    /// <summary>Regla: formato básico de correo.</summary>
    public static ReglaDeValidacion<string?> Correo() => v =>
        v is { Length: > 3 } c && c.Contains('@') && c.Contains('.') && !c.Contains(' ') ? null : "El correo no es válido.";

    /// <summary>Regla: contraseña de al menos 10 caracteres.</summary>
    public static ReglaDeValidacion<string?> Clave() => v =>
        v is { Length: >= 10 } ? null : "La contraseña debe tener al menos 10 caracteres.";
}
