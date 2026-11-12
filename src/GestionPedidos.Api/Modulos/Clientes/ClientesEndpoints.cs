using GestionPedidos.Api.Errores;
using GestionPedidos.Api.Seguridad;
using GestionPedidos.Application.Clientes;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Api.Modulos.Clientes;

/// <summary>Endpoints del módulo Clientes ([Clientes.md §4]).</summary>
public static class ClientesEndpoints
{
    /// <summary>Registra el grupo de rutas bajo <c>/api/v1/clientes</c>.</summary>
    public static IEndpointRouteBuilder MapClientes(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/clientes").WithTags("Clientes");

        g.MapGet("/", async (string? q, int? departamentoId, int? ciudadId, bool? activo, int? pagina,
                int? tamanoPagina, ListarClientes caso, CancellationToken ct) =>
                Results.Ok(await caso.EjecutarAsync(q, departamentoId, ciudadId, activo, pagina, tamanoPagina, ct)))
            .RequierePermiso(CatalogoPermisos.ClientesVer);

        g.MapGet("/{id:int}", async (int id, ObtenerCliente caso, CancellationToken ct) =>
                (await caso.EjecutarAsync(id, ct)).AHttp(Results.Ok))
            .RequierePermiso(CatalogoPermisos.ClientesVer);

        g.MapPost("/", async (CrearClienteSolicitud s, HttpContext http, RegistrarCliente caso, CancellationToken ct) =>
                (await caso.EjecutarAsync(s, http.User.IdUsuario(), ct))
                    .AHttp(c => Results.Created($"/api/v1/clientes/{c.Id}", c)))
            .RequierePermiso(CatalogoPermisos.ClientesCrear);

        g.MapPut("/{id:int}", async (int id, ActualizarClienteSolicitud s, HttpContext http, ActualizarCliente caso,
                CancellationToken ct) =>
                (await caso.EjecutarAsync(id, s, http.User.IdUsuario(), ct)).AHttp(Results.Ok))
            .RequierePermiso(CatalogoPermisos.ClientesEditar);

        g.MapPatch("/{id:int}/estado", async (int id, CambiarEstadoSolicitud s, HttpContext http,
                CambiarEstadoCliente caso, CancellationToken ct) =>
                (await caso.EjecutarAsync(id, s.Activo, http.User.IdUsuario(), ct)).AHttp(Results.Ok))
            .RequierePermiso(CatalogoPermisos.ClientesDesactivar);

        return app;
    }
}
