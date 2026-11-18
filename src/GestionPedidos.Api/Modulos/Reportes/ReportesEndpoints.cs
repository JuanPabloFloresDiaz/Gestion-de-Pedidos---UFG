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
        Mapear(g, TiposDeReporte.ProductosPorCategoria, CatalogoPermisos.CategoriasVer);
        Mapear(g, TiposDeReporte.UsuariosPorRol, CatalogoPermisos.UsuariosVer);
        Mapear(g, TiposDeReporte.PermisosPorRol, CatalogoPermisos.RolesVer);
        Mapear(g, TiposDeReporte.ActividadesPorUsuario, CatalogoPermisos.HistorialVer);
        return app;
    }

    private static void Mapear(RouteGroupBuilder g, string tipo, string permiso) =>
        g.MapGet("/" + tipo, async (string? formato, string? agruparPor, int? usuarioId, DateOnly? desde, DateOnly? hasta,
                GenerarReporte caso, CancellationToken ct) =>
            {
                var r = await caso.EjecutarAsync(tipo, formato, new FiltrosReporte(agruparPor, usuarioId, desde, hasta), ct);
                // Content-Disposition con el nombre sugerido; el archivo nunca se cachea (puede traer datos personales).
                return r.AHttp(a => Results.File(a.Contenido, a.TipoContenido, a.NombreArchivo));
            })
            .RequierePermiso(permiso);
}
