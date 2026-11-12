namespace GestionPedidos.Contracts;

/// <summary>Cliente para listados y edición ([Clientes.md]).</summary>
/// <param name="Id">Identificador.</param>
/// <param name="Nombres">Nombres.</param>
/// <param name="Apellidos">Apellidos.</param>
/// <param name="NombreCompleto">Nombre completo.</param>
/// <param name="Telefono">Teléfono.</param>
/// <param name="Direccion">Dirección.</param>
/// <param name="Correo">Correo electrónico (opcional).</param>
/// <param name="CiudadId">Identificador de la ciudad (distrito).</param>
/// <param name="Ciudad">Nombre de la ciudad.</param>
/// <param name="DepartamentoId">Identificador del departamento.</param>
/// <param name="Departamento">Nombre del departamento.</param>
/// <param name="TieneCuentaWeb">Si el cliente ya tiene una cuenta web enlazada.</param>
/// <param name="Activo">Si está activo (<c>false</c> = dado de baja suave).</param>
/// <param name="EliminadoEn">Cuándo se dio de baja; <c>null</c> si está activo.</param>
/// <param name="RowVersion">Versión (base64) para concurrencia optimista.</param>
public sealed record ClienteDto(int Id, string Nombres, string Apellidos, string NombreCompleto, string Telefono,
    string Direccion, string? Correo, int CiudadId, string Ciudad, int DepartamentoId, string Departamento,
    bool TieneCuentaWeb, bool Activo, DateTimeOffset? EliminadoEn, string RowVersion);

/// <summary>Alta de un cliente desde la administración.</summary>
/// <param name="Nombres">Nombres del cliente (1–100).</param>
/// <param name="Apellidos">Apellidos del cliente (1–100).</param>
/// <param name="Telefono">Teléfono (8–20 caracteres, formato salvadoreño).</param>
/// <param name="Direccion">Dirección de entrega (1–250).</param>
/// <param name="Correo">Correo electrónico opcional (único si se indica).</param>
/// <param name="CiudadId">Ciudad donde reside.</param>
public sealed record CrearClienteSolicitud(string Nombres, string Apellidos, string Telefono, string Direccion,
    string? Correo, int CiudadId);

/// <summary>Edición de un cliente existente.</summary>
/// <param name="Nombres">Nombres del cliente (1–100).</param>
/// <param name="Apellidos">Apellidos del cliente (1–100).</param>
/// <param name="Telefono">Teléfono (8–20 caracteres, formato salvadoreño).</param>
/// <param name="Direccion">Dirección de entrega (1–250).</param>
/// <param name="Correo">Correo electrónico opcional (único si se indica).</param>
/// <param name="CiudadId">Ciudad donde reside.</param>
/// <param name="RowVersion">Versión (base64) para control de concurrencia optimista.</param>
public sealed record ActualizarClienteSolicitud(string Nombres, string Apellidos, string Telefono, string Direccion,
    string? Correo, int CiudadId, string? RowVersion);
