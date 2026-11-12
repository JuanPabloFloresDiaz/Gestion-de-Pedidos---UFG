using GestionPedidos.Api.Errores;
using GestionPedidos.Api.Seguridad;
using GestionPedidos.Application.Pedidos;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Api.Modulos.Pedidos;

/// <summary>Endpoints del módulo administrativo de pedidos ([Pedidos.md §5]).</summary>
public static class PedidosEndpoints
{
    /// <summary>Registra las rutas de <c>/api/v1/pedidos</c>.</summary>
    public static IEndpointRouteBuilder MapPedidos(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/pedidos").WithTags("Pedidos");

        g.MapGet("/", async (
                int? clienteId,
                string? cliente,
                int? productoId,
                string? producto,
                decimal? precioMin,
                decimal? precioMax,
                string? estado,
                DateTimeOffset? pedidoDesde,
                DateTimeOffset? pedidoHasta,
                DateOnly? entregaDesde,
                DateOnly? entregaHasta,
                int? usuarioId,
                bool? incluirInactivos,
                int? pagina,
                int? tamanoPagina,
                BuscarPedidos caso,
                CancellationToken ct) =>
            {
                var filtro = new FiltroPedidosSolicitud(
                    clienteId, cliente, productoId, producto, precioMin, precioMax,
                    estado, pedidoDesde, pedidoHasta, entregaDesde, entregaHasta,
                    usuarioId, incluirInactivos, pagina, tamanoPagina);

                return (await caso.EjecutarAsync(filtro, ct)).AHttp(Results.Ok);
            })
            .RequierePermiso(CatalogoPermisos.PedidosVer);

        g.MapGet("/clientes-selector", async (string? q, IPedidoRepository repo, CancellationToken ct) =>
                Results.Ok(await repo.ListarClientesSelectorAsync(q, ct)))
            .RequierePermiso(CatalogoPermisos.PedidosVer);

        g.MapGet("/productos-selector", async (string? q, IPedidoRepository repo, CancellationToken ct) =>
                Results.Ok(await repo.ListarProductosSelectorAsync(q, ct)))
            .RequierePermiso(CatalogoPermisos.PedidosVer);

        g.MapGet("/{id:int}", async (int id, ObtenerPedido caso, CancellationToken ct) =>
                (await caso.EjecutarAsync(id, ct)).AHttp(Results.Ok))
            .RequierePermiso(CatalogoPermisos.PedidosVer);

        g.MapPost("/", async (CrearPedidoSolicitud s, HttpContext http, RegistrarPedido caso, CancellationToken ct) =>
                (await caso.EjecutarAsync(s, http.User.IdUsuario(), ct))
                    .AHttp(p => Results.Created($"/api/v1/pedidos/{p.Id}", p)))
            .RequierePermiso(CatalogoPermisos.PedidosCrear);

        g.MapPut("/{id:int}", async (int id, ActualizarPedidoSolicitud s, HttpContext http, ActualizarPedido caso, CancellationToken ct) =>
                (await caso.EjecutarAsync(id, s, http.User.IdUsuario(), ct)).AHttp(Results.Ok))
            .RequierePermiso(CatalogoPermisos.PedidosEditar);

        g.MapPatch("/{id:int}/estado", async (int id, CambiarEstadoPedidoSolicitud s, HttpContext http, CambiarEstadoPedido caso, CancellationToken ct) =>
                (await caso.EjecutarAsync(id, s, http.User.IdUsuario(), ct)).AHttp(Results.Ok))
            .RequierePermiso(CatalogoPermisos.PedidosCambiarEstado);

        g.MapPatch("/{id:int}/desactivar", async (int id, HttpContext http, DesactivarPedido caso, CancellationToken ct) =>
                (await caso.EjecutarAsync(id, http.User.IdUsuario(), ct)).AHttp())
            .RequierePermiso(CatalogoPermisos.PedidosDesactivar);

        return app;
    }
}
