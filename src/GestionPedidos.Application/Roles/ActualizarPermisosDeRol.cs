using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Application.Seguridad;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Application.Roles;

/// <summary>
/// Caso de uso: cambiar los permisos de un rol ([AutenticacionYPermisos.md §3.1]). Protege las reglas que evitan
/// dejar el sistema inservible: el Administrador no pierde sus permisos de administración, el personal
/// conserva el acceso a la administración y el Cliente nunca recibe permisos de administración.
/// </summary>
public sealed class ActualizarPermisosDeRol
{
    private readonly IRolRepository _roles;
    private readonly IUnitOfWork _uow;
    private readonly EventosAplicacion _eventos;

    /// <summary>Crea el caso de uso con sus dependencias.</summary>
    public ActualizarPermisosDeRol(IRolRepository roles, IUnitOfWork uow, EventosAplicacion eventos)
    {
        _roles = roles; _uow = uow; _eventos = eventos;
    }

    /// <summary>Establece los permisos del rol.</summary>
    /// <param name="rolId">Rol a modificar.</param>
    /// <param name="codigos">Códigos de los permisos que debe tener.</param>
    /// <param name="actorId">Administrador que hace el cambio.</param>
    /// <param name="ct">Cancelación.</param>
    public async Task<Resultado<RolDto>> EjecutarAsync(int rolId, IReadOnlyList<string> codigos, int actorId, CancellationToken ct)
    {
        var rol = await _roles.ObtenerConPermisosAsync(rolId, ct);
        if (rol is null)
            return Resultado<RolDto>.Falla(TipoError.NoEncontrado, CodigosError.NoEncontrado, "El rol no existe.");

        var pedidos = (codigos ?? Array.Empty<string>()).Select(c => c.Trim()).Where(c => c.Length > 0).ToHashSet();
        var permisos = await _roles.ObtenerPermisosPorCodigosAsync(pedidos, ct);
        var desconocidos = pedidos.Except(permisos.Select(p => p.Codigo)).ToList();
        if (desconocidos.Count > 0)
            return Resultado<RolDto>.Falla(TipoError.Validacion, CodigosError.Validacion,
                $"Permisos que no existen: {string.Join(", ", desconocidos)}.");

        var error = Reglas(rol.Nombre, pedidos);
        if (error is not null)
            return Resultado<RolDto>.Falla(TipoError.Regla, CodigosError.Regla, error);

        var antes = rol.Permisos.Select(p => p.Permiso?.Codigo ?? "").Where(c => c.Length > 0).ToHashSet();
        var añadidos = pedidos.Except(antes).OrderBy(c => c).ToList();
        var quitados = antes.Except(pedidos).OrderBy(c => c).ToList();

        if (añadidos.Count > 0 || quitados.Count > 0)
        {
            rol.EstablecerPermisos(permisos.Select(p => p.Id).ToList());
            await _uow.GuardarAsync(ct);
            // Solo tras confirmar el cambio se avisa a los suscriptores (historial).
            _eventos.OnEntidadCambiada(new EntidadCambiadaServidorEventArgs
            {
                Entidad = "Rol", EntidadId = rol.Id, Tipo = TipoCambioServidor.Actualizada, UsuarioId = actorId,
                Detalle = Resumen(rol.Nombre, añadidos, quitados),
            });
        }
        return Resultado<RolDto>.Ok(new RolDto(rol.Id, rol.Nombre, rol.Descripcion, pedidos.OrderBy(c => c).ToList()));
    }

    // Devuelve el mensaje de la primera regla violada, o null si el conjunto es válido para ese rol.
    private static string? Reglas(string nombreRol, IReadOnlySet<string> codigos)
    {
        var (obligatorios, prohibidos) = PermisosProtegidos.ParaRol(nombreRol);
        var faltan = obligatorios.Except(codigos).OrderBy(c => c).ToList();
        if (faltan.Count > 0)
            return $"El rol {nombreRol} no puede quedarse sin estos permisos: {string.Join(", ", faltan)}.";
        var indebidos = codigos.Where(prohibidos.Contains).OrderBy(c => c).ToList();
        if (indebidos.Count > 0)
            return $"Estos permisos no se pueden asignar al rol {nombreRol}: {string.Join(", ", indebidos)}.";
        return null;
    }

    private static string Resumen(string rol, List<string> añadidos, List<string> quitados)
    {
        var texto = $"Permisos del rol {rol}: +{añadidos.Count} -{quitados.Count}";
        if (añadidos.Count > 0) texto += $" (añadidos: {string.Join(", ", añadidos)})";
        if (quitados.Count > 0) texto += $" (quitados: {string.Join(", ", quitados)})";
        return texto.Length > 500 ? texto[..497] + "…" : texto;
    }
}
