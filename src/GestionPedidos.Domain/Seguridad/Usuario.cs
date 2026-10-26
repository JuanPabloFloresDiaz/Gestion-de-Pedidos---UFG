namespace GestionPedidos.Domain.Seguridad;

/// <summary>
/// Usuario del sistema (personal o cliente de la web). Protege sus propias reglas de acceso:
/// bloqueo temporal tras fallos y baja lógica ([AutenticacionYPermisos.md §2 y §5]).
/// </summary>
public class Usuario : IAuditable
{
    /// <summary>Fallos consecutivos que provocan el bloqueo.</summary>
    public const int MaximoIntentosFallidos = 5;

    /// <summary>Duración del bloqueo temporal.</summary>
    public static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(15);

    /// <summary>Identificador.</summary>
    public int Id { get; private set; }
    /// <summary>Nombre de usuario único (en el registro web es el correo).</summary>
    public string NombreUsuario { get; private set; } = "";
    /// <summary>Nombre completo para mostrar.</summary>
    public string NombreCompleto { get; private set; } = "";
    /// <summary>Correo único.</summary>
    public string Correo { get; private set; } = "";
    /// <summary>Hash de la contraseña (nunca la clave en claro).</summary>
    public string ClaveHash { get; private set; } = "";
    /// <summary>Rol del usuario (uno solo).</summary>
    public int RolId { get; private set; }
    /// <summary>Navegación al rol.</summary>
    public Rol? Rol { get; private set; }
    /// <summary>Baja lógica: <c>false</c> = inactivo.</summary>
    public bool Activo { get; private set; } = true;
    /// <summary>Cuándo se dio de baja (eliminado suave, <i>deleted_at</i>); <c>null</c> mientras esté activo.</summary>
    public DateTimeOffset? EliminadoEn { get; private set; }
    /// <summary>Obliga a cambiar la clave antes de operar.</summary>
    public bool DebeCambiarClave { get; private set; }
    /// <summary>Fallos de acceso consecutivos.</summary>
    public int IntentosFallidos { get; private set; }
    /// <summary>Hasta cuándo está bloqueado, si lo está.</summary>
    public DateTimeOffset? BloqueadoHasta { get; private set; }
    /// <summary>Último acceso correcto.</summary>
    public DateTimeOffset? UltimoAcceso { get; private set; }
    /// <summary>Fecha de creación (UTC).</summary>
    public DateTimeOffset CreadoEn { get; set; }
    /// <summary>Fecha de la última modificación (UTC).</summary>
    public DateTimeOffset ActualizadoEn { get; set; }
    /// <summary>Marca de versión para concurrencia optimista.</summary>
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private Usuario() { }

    /// <summary>Crea un usuario nuevo y activo.</summary>
    /// <param name="nombreUsuario">Nombre de usuario único.</param>
    /// <param name="nombreCompleto">Nombre completo.</param>
    /// <param name="correo">Correo único.</param>
    /// <param name="claveHash">Hash de la contraseña inicial.</param>
    /// <param name="rolId">Rol asignado.</param>
    /// <param name="debeCambiarClave">Si la clave inicial es temporal.</param>
    public static Usuario Crear(string nombreUsuario, string nombreCompleto, string correo, string claveHash,
        int rolId, bool debeCambiarClave) => new()
    {
        NombreUsuario = nombreUsuario.Trim(),
        NombreCompleto = nombreCompleto.Trim(),
        Correo = correo.Trim().ToLowerInvariant(),
        ClaveHash = claveHash,
        RolId = rolId,
        DebeCambiarClave = debeCambiarClave,
    };

    /// <summary>Indica si el usuario está bloqueado en el instante dado.</summary>
    public bool EstaBloqueado(DateTimeOffset ahora) => BloqueadoHasta is { } hasta && hasta > ahora;

    /// <summary>Registra un fallo de acceso; al llegar al máximo bloquea el usuario.</summary>
    public void RegistrarFalloAcceso(DateTimeOffset ahora)
    {
        IntentosFallidos++;
        if (IntentosFallidos >= MaximoIntentosFallidos)
        {
            BloqueadoHasta = ahora + DuracionBloqueo;
            IntentosFallidos = 0;   // el contador se reinicia al bloquear
        }
    }

    /// <summary>Registra un acceso correcto: limpia fallos y bloqueo.</summary>
    public void RegistrarAccesoExitoso(DateTimeOffset ahora)
    {
        IntentosFallidos = 0;
        BloqueadoHasta = null;
        UltimoAcceso = ahora;
    }

    /// <summary>Cambia la contraseña y quita la obligación de cambiarla.</summary>
    public void CambiarClave(string nuevoHash, bool debeCambiarClave = false)
    {
        ClaveHash = nuevoHash;
        DebeCambiarClave = debeCambiarClave;
    }

    /// <summary>Actualiza los datos editables del usuario.</summary>
    public void Actualizar(string nombreCompleto, string correo, int rolId)
    {
        NombreCompleto = nombreCompleto.Trim();
        Correo = correo.Trim().ToLowerInvariant();
        RolId = rolId;
    }

    /// <summary>
    /// Da de baja (<c>false</c>: eliminado suave, guarda la fecha en <see cref="EliminadoEn"/>) o reactiva (<c>true</c>:
    /// vuelve a estar activo y se borra esa fecha). Si ya estaba de baja, conserva la fecha original.
    /// </summary>
    /// <param name="activo">Estado deseado.</param>
    /// <param name="ahora">Instante del cambio (UTC).</param>
    public void CambiarEstado(bool activo, DateTimeOffset ahora)
    {
        if (activo) { Activo = true; EliminadoEn = null; IntentosFallidos = 0; BloqueadoHasta = null; }
        else { if (Activo || EliminadoEn is null) EliminadoEn = ahora; Activo = false; }
    }
}
