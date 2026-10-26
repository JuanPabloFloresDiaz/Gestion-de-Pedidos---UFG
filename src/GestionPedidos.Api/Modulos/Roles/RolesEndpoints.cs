using GestionPedidos.Api.Errores;
using GestionPedidos.Api.Seguridad;
using GestionPedidos.Application.Roles;
using GestionPedidos.Application.Usuarios;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Api.Modulos.Roles;

/// <summary>Endpoints de roles y permisos ([AutenticacionYPermisos.md §3.1]).</summary>
public static class RolesEndpoints
{
    /// <summary>Registra <c>/api/v1/roles</c> y <c>/api/v1/permisos</c>.</summary>
    public static IEndpointRouteBuilder MapRoles(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/roles", async (ListarRoles caso, CancellationToken ct) => Results.Ok(await caso.EjecutarAsync(ct)))
            .WithTags("Roles y permisos").RequierePermiso(CatalogoPermisos.RolesVer);

        app.MapGet("/api/v1/permisos", async (ListarPermisos caso, CancellationToken ct) => Results.Ok(await caso.EjecutarAsync(ct)))
            .WithTags("Roles y permisos").RequierePermiso(CatalogoPermisos.RolesVer);

        // Los cambios surten efecto al iniciar sesión de nuevo: el token ya emitido lleva los permisos de ese momento.
        app.MapPut("/api/v1/roles/{id:int}/permisos", async (int id, ActualizarPermisosRolSolicitud s, HttpContext http,
                ActualizarPermisosDeRol caso, CancellationToken ct) =>
                (await caso.EjecutarAsync(id, s.Permisos, http.User.IdUsuario(), ct)).AHttp(Results.Ok))
            .WithTags("Roles y permisos").RequierePermiso(CatalogoPermisos.RolesEditar);
        return app;
    }
}
