namespace GestionPedidos.Contracts;

/// <summary>Solicitud de inicio de sesión.</summary>
/// <param name="Identificador">Nombre de usuario o correo.</param>
/// <param name="Clave">Contraseña.</param>
public sealed record LoginSolicitud(string Identificador, string Clave);

/// <summary>Usuario autenticado y sus permisos.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="NombreUsuario">Nombre de usuario.</param>
/// <param name="NombreCompleto">Nombre completo.</param>
/// <param name="Rol">Nombre del rol.</param>
/// <param name="Permisos">Códigos de permiso del rol.</param>
public sealed record UsuarioActualDto(int Id, string NombreUsuario, string NombreCompleto, string Rol,
    IReadOnlyList<string> Permisos);

/// <summary>Sesión devuelta tras un login correcto.</summary>
/// <param name="AccessToken">JWT de 60 minutos.</param>
/// <param name="ExpiraEn">Instante de expiración del token (UTC).</param>
/// <param name="DebeCambiarClave">Si debe cambiar la contraseña antes de operar.</param>
/// <param name="Usuario">Datos del usuario.</param>
public sealed record SesionDto(string AccessToken, DateTimeOffset ExpiraEn, bool DebeCambiarClave,
    UsuarioActualDto Usuario);

/// <summary>Solicitud para cambiar la contraseña propia.</summary>
/// <param name="ClaveActual">Contraseña actual.</param>
/// <param name="ClaveNueva">Contraseña nueva.</param>
public sealed record CambiarClaveSolicitud(string ClaveActual, string ClaveNueva);

/// <summary>Registro de un cliente desde la web ([PortalWeb.md §3]).</summary>
/// <param name="Nombres">Nombres.</param>
/// <param name="Apellidos">Apellidos.</param>
/// <param name="Telefono">Teléfono.</param>
/// <param name="Direccion">Dirección.</param>
/// <param name="Correo">Correo (será el nombre de usuario).</param>
/// <param name="CiudadId">Ciudad (distrito).</param>
/// <param name="Clave">Contraseña.</param>
/// <param name="TelefonoConfirmacion">Teléfono registrado, solo para enlazar con un cliente existente.</param>
public sealed record RegistroClienteSolicitud(string Nombres, string Apellidos, string Telefono, string Direccion,
    string Correo, int CiudadId, string Clave, string? TelefonoConfirmacion = null);

/// <summary>Códigos de error de negocio que la interfaz distingue.</summary>
public static class CodigosError
{
    /// <summary>Credenciales inválidas, usuario inactivo o bloqueado (mensaje genérico).</summary>
    public const string CredencialesInvalidas = "CREDENCIALES_INVALIDAS";
    /// <summary>El correo ya tiene una cuenta.</summary>
    public const string CorreoEnUso = "CORREO_EN_USO";
    /// <summary>Hay un cliente con ese correo sin cuenta: se debe confirmar el teléfono.</summary>
    public const string ClienteExistente = "CLIENTE_EXISTENTE";
    /// <summary>El teléfono de confirmación no coincide.</summary>
    public const string TelefonoNoCoincide = "TELEFONO_NO_COINCIDE";
    /// <summary>La contraseña no cumple la política.</summary>
    public const string ClaveInvalida = "CLAVE_INVALIDA";
    /// <summary>Entrada inválida.</summary>
    public const string Validacion = "VALIDACION";
    /// <summary>No existe el recurso.</summary>
    public const string NoEncontrado = "NO_ENCONTRADO";
    /// <summary>Choque de unicidad o de concurrencia.</summary>
    public const string Conflicto = "CONFLICTO";
    /// <summary>Se violó una regla de negocio.</summary>
    public const string Regla = "REGLA_DE_NEGOCIO";
    /// <summary>No alcanza el stock de uno o más productos del pedido.</summary>
    public const string StockInsuficiente = "STOCK_INSUFICIENTE";
    /// <summary>Debe cambiar la contraseña antes de continuar.</summary>
    public const string DebeCambiarClave = "DEBE_CAMBIAR_CLAVE";
}
