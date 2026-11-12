using GestionPedidos.Application.Common;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Clientes;

namespace GestionPedidos.Application.Clientes;

/// <summary>Validaciones de campos compartidas por registrar y actualizar un cliente ([Clientes.md §2]).</summary>
internal static class ValidacionesCliente
{
    /// <summary>
    /// Valida nombres, apellidos, dirección, teléfono y correo. Devuelve el primer error (400) o <c>null</c> si todo es válido.
    /// </summary>
    public static Resultado<ClienteDto>? Campos(string? nombres, string? apellidos, string? telefono, string? direccion, string? correo)
    {
        var error = Validaciones.Texto(nombres, "Los nombres", 100)
            ?? Validaciones.Texto(apellidos, "Los apellidos", 100)
            ?? Validaciones.Texto(direccion, "La dirección", 250);

        if (error is null && !Cliente.EsTelefonoValido(telefono))
            error = "El teléfono debe tener entre 8 y 20 caracteres, al menos 8 números, y solo números, espacios, + o -.";

        if (error is null && !string.IsNullOrWhiteSpace(correo) && !Validaciones.EsCorreo(correo))
            error = "El correo no tiene un formato válido.";

        return error is null ? null : Resultado<ClienteDto>.Falla(TipoError.Validacion, CodigosError.Validacion, error);
    }

    /// <summary>
    /// Valida y decodifica la versión (base64) para la concurrencia optimista. Es obligatoria: sin ella no hay forma de
    /// detectar que otra persona modificó el cliente.
    /// </summary>
    /// <param name="version">Versión recibida del cliente.</param>
    /// <param name="bytes">Versión decodificada cuando es válida.</param>
    /// <returns>Un error 400 si falta o no es base64; <c>null</c> si es válida.</returns>
    public static Resultado<ClienteDto>? Version(string? version, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(version))
            return Resultado<ClienteDto>.Falla(TipoError.Validacion, CodigosError.Validacion,
                "Falta la versión del cliente. Recargue la pantalla e intente de nuevo.");

        var buffer = new byte[version.Length];
        if (!Convert.TryFromBase64String(version, buffer, out var escritos))
            return Resultado<ClienteDto>.Falla(TipoError.Validacion, CodigosError.Validacion,
                "La versión del cliente no es válida. Recargue la pantalla e intente de nuevo.");

        bytes = buffer[..escritos];
        return null;
    }
}
