using System.Net.Mail;

namespace GestionPedidos.Application.Common;

/// <summary>Validaciones simples y reutilizables de los casos de uso.</summary>
public static class Validaciones
{
    /// <summary>Indica si el texto es un correo con formato válido.</summary>
    public static bool EsCorreo(string? correo)
    {
        if (string.IsNullOrWhiteSpace(correo) || correo.Length > 150) return false;
        return MailAddress.TryCreate(correo.Trim(), out var m) && m.Address == correo.Trim() && correo.Contains('.');
    }

    /// <summary>Deja solo los dígitos de un teléfono y quita el prefijo <c>503</c> si lo trae.</summary>
    public static string NormalizarTelefono(string? telefono)
    {
        var digitos = new string((telefono ?? "").Where(char.IsDigit).ToArray());
        return digitos.Length > 8 && digitos.StartsWith("503", StringComparison.Ordinal) ? digitos[3..] : digitos;
    }

    /// <summary>Devuelve un mensaje si el texto está vacío o supera el máximo; <c>null</c> si es válido.</summary>
    public static string? Texto(string? valor, string campo, int maximo, int minimo = 1)
    {
        var v = valor?.Trim() ?? "";
        if (v.Length < minimo) return minimo == 1 ? $"{campo} es obligatorio." : $"{campo} debe tener al menos {minimo} caracteres.";
        if (v.Length > maximo) return $"{campo} no puede superar {maximo} caracteres.";
        return null;
    }
}
