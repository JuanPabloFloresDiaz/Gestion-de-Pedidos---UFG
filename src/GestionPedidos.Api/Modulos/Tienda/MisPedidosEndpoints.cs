using GestionPedidos.Api.Errores;
using GestionPedidos.Api.Seguridad;
using GestionPedidos.Application.Tienda;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Api.Modulos.Tienda;

/// <summary>
/// Pedidos del cliente de la tienda ([PortalWeb.md §4]). Nunca reciben un identificador de cliente: siempre se usa el cliente
/// enlazado a la cuenta del token, así nadie puede ver ni tocar pedidos ajenos.
/// </summary>
public static class MisPedidosEndpoints
{
    /// <summary>Registra <c>/api/v1/mis-pedidos</c>.</summary>
    public static IEndpointRouteBuilder MapMisPedidos(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/mis-pedidos").WithTags("Mis pedidos (tienda)");

        g.MapGet("/entrega", async (HttpContext http, PrepararPedidoDeCliente caso, CancellationToken ct) =>
                (await caso.EjecutarAsync(http.User.IdUsuario(), ct)).AHttp(Results.Ok))
            .RequierePermiso(CatalogoPermisos.MisPedidosCrear);

        g.MapGet("/", async (int? pagina, int? tamanoPagina, HttpContext http, ListarPedidosDeCliente caso, CancellationToken ct) =>
                (await caso.EjecutarAsync(http.User.IdUsuario(), pagina, tamanoPagina, ct)).AHttp(Results.Ok))
            .RequierePermiso(CatalogoPermisos.MisPedidosVer);

        g.MapGet("/{id:int}", async (int id, HttpContext http, ListarPedidosDeCliente caso, CancellationToken ct) =>
                (await caso.ObtenerAsync(http.User.IdUsuario(), id, ct)).AHttp(Results.Ok))
            .RequierePermiso(CatalogoPermisos.MisPedidosVer);

        g.MapPost("/", async (CrearPedidoClienteSolicitud s, HttpContext http, RealizarPedidoDeCliente caso, CancellationToken ct) =>
                (await caso.EjecutarAsync(http.User.IdUsuario(), s, ct)).AHttp(p => Results.Created($"/api/v1/mis-pedidos/{p.Id}", p)))
            .RequierePermiso(CatalogoPermisos.MisPedidosCrear);

        g.MapPatch("/{id:int}/cancelar", async (int id, HttpContext http, CancelarPedidoDeCliente caso, CancellationToken ct) =>
                (await caso.EjecutarAsync(http.User.IdUsuario(), id, ct)).AHttp(Results.Ok))
            .RequierePermiso(CatalogoPermisos.MisPedidosCancelar);

        return app;
    }
}
