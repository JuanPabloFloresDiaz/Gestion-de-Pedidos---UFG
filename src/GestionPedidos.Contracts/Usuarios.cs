namespace GestionPedidos.Contracts;

/// <summary>Usuario para listados y edición.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="NombreUsuario">Nombre de usuario.</param>
/// <param name="NombreCompleto">Nombre completo.</param>
/// <param name="Correo">Correo.</param>
/// <param name="RolId">Rol.</param>
/// <param name="Rol">Nombre del rol.</param>
/// <param name="Activo">Si está activo.</param>
/// <param name="DebeCambiarClave">Si debe cambiar su clave.</param>
/// <param name="UltimoAcceso">Último acceso correcto.</param>
/// <param name="RowVersion">Versión (base64) para concurrencia optimista.</param>
/// <param name="EliminadoEn">Cuándo se dio de baja (eliminado suave); <c>null</c> si está activo.</param>
public sealed record UsuarioDto(int Id, string NombreUsuario, string NombreCompleto, string Correo, int RolId,
    string Rol, bool Activo, bool DebeCambiarClave, DateTimeOffset? UltimoAcceso, string RowVersion,
    DateTimeOffset? EliminadoEn = null);

/// <summary>Alta de un usuario del personal.</summary>
/// <param name="NombreUsuario">Nombre de usuario único.</param>
/// <param name="NombreCompleto">Nombre completo.</param>
/// <param name="Correo">Correo único.</param>
/// <param name="RolId">Rol asignado.</param>
/// <param name="ClaveInicial">Contraseña inicial (temporal: deberá cambiarla).</param>
public sealed record CrearUsuarioSolicitud(string NombreUsuario, string NombreCompleto, string Correo, int RolId,
    string ClaveInicial);

/// <summary>Edición de un usuario.</summary>
/// <param name="NombreCompleto">Nombre completo.</param>
/// <param name="Correo">Correo único.</param>
/// <param name="RolId">Rol asignado.</param>
/// <param name="RowVersion">Versión (base64) leída al cargar, para detectar cambios concurrentes.</param>
public sealed record ActualizarUsuarioSolicitud(string NombreCompleto, string Correo, int RolId, string? RowVersion);

/// <summary>Cambio de estado (baja o reactivación).</summary>
/// <param name="Activo">Estado deseado.</param>
public sealed record CambiarEstadoSolicitud(bool Activo);

/// <summary>Rol con sus permisos.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="Nombre">Nombre.</param>
/// <param name="Descripcion">Descripción.</param>
/// <param name="Permisos">Códigos de permiso.</param>
/// <param name="Obligatorios">Permisos que a este rol no se le pueden quitar.</param>
/// <param name="Prohibidos">Permisos que a este rol no se le pueden dar.</param>
public sealed record RolDto(int Id, string Nombre, string? Descripcion, IReadOnlyList<string> Permisos,
    IReadOnlyList<string>? Obligatorios = null, IReadOnlyList<string>? Prohibidos = null);
