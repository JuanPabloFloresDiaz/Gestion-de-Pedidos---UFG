using GestionPedidos.Api.Errores;
using GestionPedidos.Api.Seguridad;
using GestionPedidos.Application.Reportes;
using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Api.Modulos.Reportes;

/// <summary>
/// Endpoints de reportes (solo lectura). Cada reporte exige el mismo permiso que la pantalla de donde sale, así no se crea
/// ningún permiso nuevo: quien puede ver los datos puede exportarlos.
/// </summary>
public static class ReportesEndpoints
{
    /// <summary>Registra <c>/api/v1/reportes/{tipo}?formato=pdf|xlsx|csv</c>.</summary>
    public static IEndpointRouteBuilder MapReportes(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/reportes").WithTags("Reportes");

        Mapear(g, TiposDeReporte.ClientesPorZona, CatalogoPermisos.ClientesVer);
        Mapear(g, TiposDeReporte.ProductosPorCategoria, CatalogoPermisos.CategoriasVer, conOpciones: true);
        Mapear(g, TiposDeReporte.UsuariosPorRol, CatalogoPermisos.UsuariosVer);
        Mapear(g, TiposDeReporte.PermisosPorRol, CatalogoPermisos.RolesVer);
        Mapear(g, TiposDeReporte.ActividadesPorUsuario, CatalogoPermisos.HistorialVer, conOpciones: true);
        Mapear(g, TiposDeReporte.PedidosPorEstado, CatalogoPermisos.PedidosVer);
        Mapear(g, TiposDeReporte.ProductosMasVendidos, CatalogoPermisos.ProductosVer);
        return app;
    }

    private static void Mapear(RouteGroupBuilder g, string tipo, string permiso, bool conOpciones = false)
    {
        // Los reportes que se pueden acotar (a un usuario o a una categoría) ofrecen la lista de opciones para elegir,
        // con el mismo permiso que el reporte: quien puede exportarlo puede ver a quién o a qué lo puede acotar.
        if (conOpciones)
            g.MapGet("/" + tipo + "/opciones", async (GenerarReporte caso, CancellationToken ct) =>
                    (await caso.OpcionesAsync(tipo, ct)).AHttp(Results.Ok))
                .RequierePermiso(permiso);

        g.MapGet("/" + tipo, async (string? formato, string? agruparPor, int? usuarioId, DateOnly? desde, DateOnly? hasta,
                int? categoriaId, string? acento, GenerarReporte caso, CancellationToken ct) =>
            {
                var r = await caso.EjecutarAsync(tipo, formato, new FiltrosReporte(agruparPor, usuarioId, desde, hasta, categoriaId, acento), ct);
                // Content-Disposition con el nombre sugerido; el archivo nunca se cachea (puede traer datos personales).
                return r.AHttp(a => Results.File(a.Contenido, a.TipoContenido, a.NombreArchivo));
            })
            .RequierePermiso(permiso);
    }
}
