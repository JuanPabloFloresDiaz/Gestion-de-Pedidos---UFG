using GestionPedidos.Contracts;
using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Application.Autenticacion;

/// <summary>Conversiones de entidades a DTO (mapeo manual, sin librerías).</summary>
public static class Mapeos
{
    /// <summary>Convierte un usuario (con su rol cargado) en <see cref="UsuarioDto"/>.</summary>
    public static UsuarioDto ADto(this Usuario u) => new(u.Id, u.NombreUsuario, u.NombreCompleto, u.Correo, u.RolId,
        u.Rol?.Nombre ?? "", u.Activo, u.DebeCambiarClave, u.UltimoAcceso, Convert.ToBase64String(u.RowVersion), u.EliminadoEn);

    /// <summary>Convierte un usuario y sus permisos en <see cref="UsuarioActualDto"/>.</summary>
    public static UsuarioActualDto AActual(this Usuario u, IReadOnlyList<string> permisos) =>
        new(u.Id, u.NombreUsuario, u.NombreCompleto, u.Rol?.Nombre ?? "", permisos);
}
