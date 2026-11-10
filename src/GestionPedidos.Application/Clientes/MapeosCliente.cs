using GestionPedidos.Contracts;
using GestionPedidos.Domain.Clientes;

namespace GestionPedidos.Application.Clientes;

/// <summary>Conversiones de la entidad Cliente a DTO.</summary>
public static class MapeosCliente
{
    /// <summary>Convierte un cliente (con ciudad y departamento cargados) a <see cref="ClienteDto"/>.</summary>
    public static ClienteDto ADto(this Cliente c) => new(
        c.Id,
        c.Nombres,
        c.Apellidos,
        c.NombreCompleto,
        c.Telefono,
        c.Direccion,
        c.Correo,
        c.CiudadId,
        c.Ciudad?.Nombre ?? "",
        c.Ciudad?.DepartamentoId ?? 0,
        c.Ciudad?.Departamento?.Nombre ?? "",
        c.UsuarioId is not null,
        c.Activo,
        c.EliminadoEn,
        Convert.ToBase64String(c.RowVersion));
}
