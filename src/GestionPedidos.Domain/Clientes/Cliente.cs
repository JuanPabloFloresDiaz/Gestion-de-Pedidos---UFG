using GestionPedidos.Domain.Geografia;
using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Domain.Clientes;

/// <summary>
/// Cliente dueño de pedidos. En la Fase 0.5 solo define la forma persistida; las reglas de
/// negocio (validaciones, baja con pedidos pendientes…) se añaden en la Fase 2 ([Clientes.md]).
/// </summary>
public class Cliente : IAuditable
{
    /// <summary>Identificador.</summary>
    public int Id { get; set; }
    /// <summary>Nombres.</summary>
    public string Nombres { get; set; } = "";
    /// <summary>Apellidos.</summary>
    public string Apellidos { get; set; } = "";
    /// <summary>Teléfono.</summary>
    public string Telefono { get; set; } = "";
    /// <summary>Dirección.</summary>
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
}
