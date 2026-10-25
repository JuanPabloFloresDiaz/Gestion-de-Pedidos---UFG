using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Domain.Historial;

/// <summary>Registro inalterable de una acción realizada en la aplicación ([HistorialActividades.md]).</summary>
public class Actividad
{
    /// <summary>Identificador.</summary>
    public long Id { get; set; }
    /// <summary>Usuario que la realizó; nulo si el usuario intentado no existe.</summary>
    public int? UsuarioId { get; set; }
    /// <summary>Navegación al usuario.</summary>
    public Usuario? Usuario { get; set; }
    /// <summary>Momento (UTC).</summary>
    public DateTimeOffset Fecha { get; set; }
    /// <summary>Acción (<c>Login</c>, <c>Registrar</c>, <c>Actualizar</c>, <c>CambiarEstado</c>…).</summary>
    public string Accion { get; set; } = "";
    /// <summary>Entidad afectada (<c>Cliente</c>, <c>Pedido</c>, <c>Usuario</c>…).</summary>
    public string Entidad { get; set; } = "";
    /// <summary>Identificador de la entidad afectada.</summary>
    public int? EntidadId { get; set; }
    /// <summary>Detalle legible (nunca contraseñas ni tokens).</summary>
    public string? Detalle { get; set; }
}
