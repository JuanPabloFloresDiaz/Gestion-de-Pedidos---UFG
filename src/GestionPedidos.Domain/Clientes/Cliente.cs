using GestionPedidos.Domain.Geografia;
using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Domain.Clientes;

/// <summary>
/// Cliente dueño de pedidos ([Clientes.md]). Protege sus reglas de negocio:
/// formato de teléfono salvadoreño, baja lógica suave y enlace único con usuario web.
/// </summary>
public class Cliente : IAuditable
{
    /// <summary>Identificador.</summary>
    public int Id { get; set; }

    /// <summary>Nombres (1–100 caracteres).</summary>
    public string Nombres { get; set; } = "";

    /// <summary>Apellidos (1–100 caracteres).</summary>
    public string Apellidos { get; set; } = "";

    /// <summary>Teléfono (8–20 caracteres, dígitos, espacios, + y -).</summary>
    public string Telefono { get; set; } = "";

    /// <summary>Dirección de entrega (1–250 caracteres).</summary>
    public string Direccion { get; set; } = "";

    /// <summary>Correo opcional y único; lo usa la web para enlazar la cuenta.</summary>
    public string? Correo { get; set; }

    /// <summary>Ciudad (el departamento se deduce de ella).</summary>
    public int CiudadId { get; set; }

    /// <summary>Navegación a la ciudad.</summary>
    public Ciudad? Ciudad { get; set; }

    /// <summary>Cuenta web enlazada, si existe.</summary>
    public int? UsuarioId { get; set; }

    /// <summary>Navegación al usuario enlazado.</summary>
    public Usuario? Usuario { get; set; }

    /// <summary>Baja lógica: <c>false</c> = inactivo.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Cuándo se dio de baja (eliminado suave); <c>null</c> mientras esté activo.</summary>
    public DateTimeOffset? EliminadoEn { get; set; }

    /// <summary>Creación (UTC).</summary>
    public DateTimeOffset CreadoEn { get; set; }

    /// <summary>Última modificación (UTC).</summary>
    public DateTimeOffset ActualizadoEn { get; set; }

    /// <summary>Concurrencia optimista.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    /// <summary>Nombre y apellidos concatenados para mostrar.</summary>
    public string NombreCompleto => $"{Nombres} {Apellidos}".Trim();

    /// <summary>Crea un cliente nuevo y activo.</summary>
    public static Cliente Crear(string nombres, string apellidos, string telefono, string direccion,
        string? correo, int ciudadId) => new()
    {
        Nombres = nombres.Trim(),
        Apellidos = apellidos.Trim(),
        Telefono = telefono.Trim(),
        Direccion = direccion.Trim(),
        Correo = string.IsNullOrWhiteSpace(correo) ? null : correo.Trim().ToLowerInvariant(),
        CiudadId = ciudadId,
        Activo = true,
    };

    /// <summary>Actualiza los datos editables del cliente.</summary>
    public void Actualizar(string nombres, string apellidos, string telefono, string direccion,
        string? correo, int ciudadId)
    {
        Nombres = nombres.Trim();
        Apellidos = apellidos.Trim();
        Telefono = telefono.Trim();
        Direccion = direccion.Trim();
        Correo = string.IsNullOrWhiteSpace(correo) ? null : correo.Trim().ToLowerInvariant();
        CiudadId = ciudadId;
    }

    /// <summary>
    /// Da de baja (<c>false</c>: eliminado suave, guarda la fecha en <see cref="EliminadoEn"/>) o reactiva (<c>true</c>:
    /// vuelve a estar activo y se borra esa fecha). Si ya estaba de baja, conserva la fecha original.
    /// </summary>
    public void CambiarEstado(bool activo, DateTimeOffset ahora)
    {
        if (activo)
        {
            Activo = true;
            EliminadoEn = null;
        }
        else
        {
            if (Activo || EliminadoEn is null) EliminadoEn = ahora;
            Activo = false;
        }
    }

    /// <summary>Indica si el formato de teléfono es válido (8–20 caracteres con solo dígitos, espacios, + y -).</summary>
    public static bool EsTelefonoValido(string? telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono)) return false;
        var t = telefono.Trim();
        if (t.Length is < 8 or > 20) return false;
        if (!t.All(c => char.IsDigit(c) || c is ' ' or '+' or '-')) return false;
        return t.Count(char.IsDigit) >= 8;   // al menos 8 dígitos reales
    }
    
    /// <summary>Enlaza la cuenta web de usuario.</summary>
    public void EnlazarUsuario(int usuarioId) => UsuarioId = usuarioId;
}
