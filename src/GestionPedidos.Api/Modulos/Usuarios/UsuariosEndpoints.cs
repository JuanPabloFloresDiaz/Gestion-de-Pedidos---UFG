using GestionPedidos.Api.Errores;
using GestionPedidos.Api.Seguridad;
using GestionPedidos.Application.Usuarios;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Api.Modulos.Usuarios;

/// <summary>Endpoints de usuarios ([AutenticacionYPermisos.md §5]).</summary>
public static class UsuariosEndpoints
{
    /// <summary>Registra <c>/api/v1/usuarios</c>.</summary>
    public static IEndpointRouteBuilder MapUsuarios(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/usuarios").WithTags("Usuarios");

        g.MapGet("/", async (string? q, int? rolId, bool? activo, int? pagina, int? tamanoPagina,
                ListarUsuarios caso, CancellationToken ct) =>
                Results.Ok(await caso.EjecutarAsync(q, rolId, activo, pagina, tamanoPagina, ct)))
            .RequierePermiso(CatalogoPermisos.UsuariosVer);

        g.MapGet("/{id:int}", async (int id, ObtenerUsuario caso, CancellationToken ct) =>
                (await caso.EjecutarAsync(id, ct)).AHttp(Results.Ok))
            .RequierePermiso(CatalogoPermisos.UsuariosVer);

        g.MapPost("/", async (CrearUsuarioSolicitud s, HttpContext http, CrearUsuario caso, CancellationToken ct) =>
                (await caso.EjecutarAsync(s, http.User.IdUsuario(), ct))
                    .AHttp(u => Results.Created($"/api/v1/usuarios/{u.Id}", u)))
            .RequierePermiso(CatalogoPermisos.UsuariosCrear);

        g.MapPut("/{id:int}", async (int id, ActualizarUsuarioSolicitud s, HttpContext http, ActualizarUsuario caso,
                CancellationToken ct) =>
                (await caso.EjecutarAsync(id, s, http.User.IdUsuario(), ct)).AHttp(Results.Ok))
            .RequierePermiso(CatalogoPermisos.UsuariosEditar);

        g.MapPatch("/{id:int}/estado", async (int id, CambiarEstadoSolicitud s, HttpContext http,
                CambiarEstadoUsuario caso, CancellationToken ct) =>
                (await caso.EjecutarAsync(id, s.Activo, http.User.IdUsuario(), ct)).AHttp(Results.Ok))
            .RequierePermiso(CatalogoPermisos.UsuariosDesactivar);

        // El DELETE del CRUD: eliminación definitiva, solo de usuarios que ya están dados de baja.
        g.MapDelete("/{id:int}", async (int id, HttpContext http, EliminarUsuario caso, CancellationToken ct) =>
                (await caso.EjecutarAsync(id, http.User.IdUsuario(), ct)).AHttp())
            .RequierePermiso(CatalogoPermisos.UsuariosEliminar);

        return app;
    }
}
