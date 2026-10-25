namespace GestionPedidos.Ui.Comun.Eventos;

/// <summary>Tipo de entidad a la que se refiere una notificación.</summary>
public enum TipoEntidad
{
    /// <summary>Cliente.</summary>
    Cliente,
    /// <summary>Producto.</summary>
    Producto,
    /// <summary>Pedido.</summary>
    Pedido,
    /// <summary>Usuario.</summary>
    Usuario,
}

/// <summary>Qué le ocurrió a la entidad.</summary>
public enum TipoCambio
{
    /// <summary>Alta.</summary>
    Registrada,
    /// <summary>Edición.</summary>
    Actualizada,
    /// <summary>Baja lógica o reactivación.</summary>
    EstadoCambiado,
}

/// <summary>Datos del evento <see cref="IPublicadorDeCambios.EntidadCambiada"/>.</summary>
public class EntidadCambiadaEventArgs : EventArgs
{
    /// <summary>Tipo de entidad afectada.</summary>
    public required TipoEntidad Entidad { get; init; }
    /// <summary>Identificador de la entidad.</summary>
    public required int Id { get; init; }
    /// <summary>Qué ocurrió: alta, edición o cambio de estado.</summary>
    public required TipoCambio Tipo { get; init; }
    /// <summary>Texto legible para el usuario (por ejemplo, "Ana Pérez").</summary>
    public required string Descripcion { get; init; }
    /// <summary>Estado activo resultante (útil en cambios de estado).</summary>
    public bool Activo { get; init; } = true;
}

/// <summary>Firma de los manejadores del evento <c>EntidadCambiada</c>.</summary>
/// <param name="sender">Objeto que publicó el evento.</param>
/// <param name="e">Datos del cambio.</param>
public delegate void EntidadCambiadaEventHandler(object sender, EntidadCambiadaEventArgs e);

/// <summary>Contrato de las clases que notifican altas, ediciones y cambios de estado.</summary>
public interface IPublicadorDeCambios
{
    /// <summary>Se produce después de que la API confirma un cambio.</summary>
    event EntidadCambiadaEventHandler? EntidadCambiada;
}

/// <summary>Datos de los eventos de sesión.</summary>
public class SesionEventArgs : EventArgs
{
    /// <summary>Nombre de usuario de la sesión.</summary>
    public string NombreUsuario { get; init; } = "";
    /// <summary>Rol del usuario.</summary>
    public string Rol { get; init; } = "";
    /// <summary>Minutos que quedan de sesión (solo en <c>SesionPorExpirar</c>).</summary>
    public int MinutosRestantes { get; init; }
}

/// <summary>Firma de los manejadores de los eventos de sesión.</summary>
/// <param name="sender">Servicio de sesión.</param>
/// <param name="e">Datos de la sesión.</param>
public delegate void SesionEventHandler(object sender, SesionEventArgs e);

/// <summary>Datos de los eventos de documentos (pestañas).</summary>
public class DocumentoEventArgs : EventArgs
{
    /// <summary>Clave única del documento.</summary>
    public required string Clave { get; init; }
    /// <summary>Título visible.</summary>
    public required string Titulo { get; init; }
}

/// <summary>Firma de los manejadores de los eventos de documentos.</summary>
/// <param name="sender">Servicio de documentos.</param>
/// <param name="e">Datos del documento.</param>
public delegate void DocumentoEventHandler(object sender, DocumentoEventArgs e);
