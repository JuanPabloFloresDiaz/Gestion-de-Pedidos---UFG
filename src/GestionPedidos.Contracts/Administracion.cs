namespace GestionPedidos.Contracts;

/// <summary>Permiso del catálogo del sistema.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="Codigo">Código único (por ejemplo <c>clientes.crear</c>).</param>
/// <param name="Descripcion">Descripción legible.</param>
public sealed record PermisoDto(int Id, string Codigo, string Descripcion);

/// <summary>Nueva lista completa de permisos de un rol.</summary>
/// <param name="Permisos">Códigos de los permisos que el rol debe tener (los demás se le quitan).</param>
public sealed record ActualizarPermisosRolSolicitud(IReadOnlyList<string> Permisos);

/// <summary>Departamento de El Salvador.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="Codigo">Código ISO 3166-2.</param>
/// <param name="Nombre">Nombre.</param>
/// <param name="TotalCiudades">Cantidad de ciudades (distritos) que contiene.</param>
public sealed record DepartamentoDto(int Id, string Codigo, string Nombre, int TotalCiudades);

/// <summary>Ciudad = distrito municipal de El Salvador.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="DepartamentoId">Departamento al que pertenece.</param>
/// <param name="Departamento">Nombre del departamento.</param>
/// <param name="Municipio">Municipio vigente (uno de los 44) al que pertenece el distrito.</param>
/// <param name="Nombre">Nombre del distrito.</param>
public sealed record CiudadDto(int Id, int DepartamentoId, string Departamento, string Municipio, string Nombre);

/// <summary>Valores disponibles para los filtros del historial.</summary>
/// <param name="Entidades">Entidades que aparecen en el historial.</param>
/// <param name="Acciones">Acciones que aparecen en el historial.</param>
public sealed record FiltrosHistorialDto(IReadOnlyList<string> Entidades, IReadOnlyList<string> Acciones);

/// <summary>Registro del historial de actividades.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="Fecha">Momento (UTC).</param>
/// <param name="UsuarioId">Usuario que realizó la acción, si se conoce.</param>
/// <param name="Usuario">Nombre de usuario de quien la realizó.</param>
/// <param name="Accion">Acción (<c>Login</c>, <c>Registrar</c>, <c>Actualizar</c>…).</param>
/// <param name="Entidad">Entidad afectada.</param>
/// <param name="EntidadId">Identificador de la entidad afectada.</param>
/// <param name="Detalle">Detalle legible, sin datos sensibles.</param>
public sealed record ActividadDto(long Id, DateTimeOffset Fecha, int? UsuarioId, string? Usuario, string Accion,
    string Entidad, int? EntidadId, string? Detalle);
