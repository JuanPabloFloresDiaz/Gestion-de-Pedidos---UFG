using GestionPedidos.Application.Common;
using GestionPedidos.Contracts;

namespace GestionPedidos.Application.Reportes;

/// <summary>Formatos en los que se puede descargar un reporte.</summary>
public enum FormatoReporte
{
    /// <summary>Documento PDF para imprimir o compartir.</summary>
    Pdf,
    /// <summary>Hoja de cálculo de Excel (.xlsx).</summary>
    Excel,
    /// <summary>Texto separado por comas (.csv), para abrirlo en cualquier programa.</summary>
    Csv,
}

/// <summary>Identificadores de los reportes disponibles (los que viajan en la dirección <c>/api/v1/reportes/{tipo}</c>).</summary>
public static class TiposDeReporte
{
    /// <summary>Clientes agrupados por departamento o por distrito.</summary>
    public const string ClientesPorZona = "clientes-por-zona";
    /// <summary>Productos agrupados por categoría y subcategoría.</summary>
    public const string ProductosPorCategoria = "productos-por-categoria";
    /// <summary>Usuarios del sistema agrupados por rol.</summary>
    public const string UsuariosPorRol = "usuarios-por-rol";
    /// <summary>Permisos que tiene cada rol.</summary>
    public const string PermisosPorRol = "permisos-por-rol";
    /// <summary>Historial de actividades, agrupado por usuario.</summary>
    public const string ActividadesPorUsuario = "actividades-por-usuario";
    /// <summary>Pedidos del sistema agrupados por su estado (Entregado, Cancelado, Pendiente).</summary>
    public const string PedidosPorEstado = "pedidos-por-estado";
}

/// <summary>Una columna del reporte.</summary>
/// <param name="Titulo">Encabezado.</param>
/// <param name="Numerica">Si contiene números (se alinean a la derecha y en Excel quedan como número, no como texto).</param>
public sealed record ColumnaReporte(string Titulo, bool Numerica = false);

/// <summary>
/// Un reporte ya calculado, independiente del formato: lo arman los repositorios y lo convierten en PDF, Excel o CSV los exportadores.
/// Cada celda es texto, número (<c>int</c>/<c>decimal</c>) o <c>null</c>.
/// </summary>
/// <param name="Titulo">Título del reporte.</param>
/// <param name="Descripcion">Qué muestra y con qué filtros se generó.</param>
/// <param name="Columnas">Columnas, en orden.</param>
/// <param name="Filas">Filas; cada una tiene tantas celdas como columnas.</param>
/// <param name="GeneradoEn">Cuándo se generó (UTC).</param>
/// <param name="Nota">Aviso al pie (por ejemplo, que se recortó por exceder el máximo de filas).</param>
public sealed record TablaReporte(string Titulo, string Descripcion, IReadOnlyList<ColumnaReporte> Columnas,
    IReadOnlyList<IReadOnlyList<object?>> Filas, DateTimeOffset GeneradoEn, string? Nota = null);

/// <summary>Filtros opcionales de los reportes (cada reporte usa solo los que le corresponden).</summary>
/// <param name="AgruparPor"><c>departamento</c> o <c>distrito</c> (reporte de clientes).</param>
/// <param name="UsuarioId">Solo las actividades de este usuario.</param>
/// <param name="Desde">Fecha inicial de las actividades (incluida).</param>
/// <param name="Hasta">Fecha final de las actividades (incluida).</param>
public sealed record FiltrosReporte(string? AgruparPor = null, int? UsuarioId = null, DateOnly? Desde = null, DateOnly? Hasta = null);

/// <summary>Un archivo listo para descargar.</summary>
/// <param name="Contenido">Bytes del archivo.</param>
/// <param name="TipoContenido">Tipo MIME.</param>
/// <param name="NombreArchivo">Nombre sugerido, con extensión.</param>
public sealed record ArchivoReporte(byte[] Contenido, string TipoContenido, string NombreArchivo);

/// <summary>Consultas que arman cada reporte (solo lectura).</summary>
public interface IReportesRepository
{
    /// <summary>Clientes por departamento o distrito, con activos e inactivos.</summary>
    Task<TablaReporte> ClientesPorZonaAsync(bool porDistrito, DateTimeOffset ahora, CancellationToken ct);
    /// <summary>Productos por categoría y subcategoría, con existencias y valor del inventario.</summary>
    Task<TablaReporte> ProductosPorCategoriaAsync(DateTimeOffset ahora, CancellationToken ct);
    /// <summary>Usuarios agrupados por rol.</summary>
    Task<TablaReporte> UsuariosPorRolAsync(DateTimeOffset ahora, CancellationToken ct);
    /// <summary>Permisos de cada rol.</summary>
    Task<TablaReporte> PermisosPorRolAsync(DateTimeOffset ahora, CancellationToken ct);
    /// <summary>Actividades por usuario, de más reciente a más antigua, hasta <paramref name="maximoFilas"/> filas.</summary>
    Task<TablaReporte> ActividadesPorUsuarioAsync(int? usuarioId, DateTimeOffset? desde, DateTimeOffset? hasta, int maximoFilas, DateTimeOffset ahora, CancellationToken ct);
    /// <summary>Pedidos agrupados por estado (Entregado, Cancelado, Pendiente), con conteo de activos, inactivos y total.</summary>
    Task<TablaReporte> PedidosPorEstadoAsync(DateTimeOffset? desde, DateTimeOffset? hasta, DateTimeOffset ahora, CancellationToken ct);
}

/// <summary>Convierte un reporte calculado en un archivo.</summary>
public interface IExportadorDeReportes
{
    /// <summary>Genera el archivo del reporte en el formato pedido.</summary>
    /// <param name="tabla">Reporte ya calculado.</param>
    /// <param name="formato">PDF, Excel o CSV.</param>
    ArchivoReporte Exportar(TablaReporte tabla, FormatoReporte formato);
}

/// <summary>
/// Caso de uso: genera un reporte y lo exporta. Valida el tipo, el formato y los filtros; la API solo decide el permiso que cada reporte exige.
/// </summary>
public sealed class GenerarReporte
{
    /// <summary>Máximo de filas del reporte de actividades (el historial crece sin límite).</summary>
    public const int MaximoFilasActividades = 5000;

    private readonly IReportesRepository _repo;
    private readonly IExportadorDeReportes _exportador;
    private readonly TimeProvider _reloj;

    /// <summary>Crea el caso de uso.</summary>
    public GenerarReporte(IReportesRepository repo, IExportadorDeReportes exportador, TimeProvider reloj)
    {
        _repo = repo; _exportador = exportador; _reloj = reloj;
    }

    /// <summary>Convierte el texto de la dirección (<c>pdf</c>, <c>xlsx</c>, <c>csv</c>) en un formato; <c>null</c> si no existe.</summary>
    public static FormatoReporte? InterpretarFormato(string? texto) => texto?.Trim().ToLowerInvariant() switch
    {
        "pdf" => FormatoReporte.Pdf,
        "xlsx" or "excel" => FormatoReporte.Excel,
        "csv" => FormatoReporte.Csv,
        _ => null,
    };

    /// <summary>Genera el reporte.</summary>
    /// <param name="tipo">Uno de <see cref="TiposDeReporte"/>.</param>
    /// <param name="formato">Texto del formato (<c>pdf</c>, <c>xlsx</c> o <c>csv</c>).</param>
    /// <param name="filtros">Filtros opcionales.</param>
    /// <param name="ct">Cancelación.</param>
    public async Task<Resultado<ArchivoReporte>> EjecutarAsync(string tipo, string? formato, FiltrosReporte filtros, CancellationToken ct)
    {
        if (InterpretarFormato(formato) is not { } fmt)
            return Resultado<ArchivoReporte>.Falla(TipoError.Validacion, CodigosError.Validacion, "El formato debe ser pdf, xlsx o csv.");
        if (filtros.Desde is not null && filtros.Hasta is not null && filtros.Desde > filtros.Hasta)
            return Resultado<ArchivoReporte>.Falla(TipoError.Validacion, CodigosError.Validacion, "La fecha inicial no puede ser posterior a la final.");

        var ahora = _reloj.GetUtcNow();
        TablaReporte tabla;
        switch (tipo)
        {
            case TiposDeReporte.ClientesPorZona:
                var agrupar = filtros.AgruparPor?.Trim().ToLowerInvariant();
                if (agrupar is not (null or "" or "departamento" or "distrito"))
                    return Resultado<ArchivoReporte>.Falla(TipoError.Validacion, CodigosError.Validacion, "Agrupar por «departamento» o «distrito».");
                tabla = await _repo.ClientesPorZonaAsync(agrupar == "distrito", ahora, ct);
                break;
            case TiposDeReporte.ProductosPorCategoria:
                tabla = await _repo.ProductosPorCategoriaAsync(ahora, ct);
                break;
            case TiposDeReporte.UsuariosPorRol:
                tabla = await _repo.UsuariosPorRolAsync(ahora, ct);
                break;
            case TiposDeReporte.PermisosPorRol:
                tabla = await _repo.PermisosPorRolAsync(ahora, ct);
                break;
            case TiposDeReporte.ActividadesPorUsuario:
                // Las fechas del filtro son UTC y el día final se incluye completo (igual que en el historial).
                var d = filtros.Desde?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                var h = filtros.Hasta?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                tabla = await _repo.ActividadesPorUsuarioAsync(filtros.UsuarioId, d is null ? null : new DateTimeOffset(d.Value),
                    h is null ? null : new DateTimeOffset(h.Value), MaximoFilasActividades, ahora, ct);
                break;
            case TiposDeReporte.PedidosPorEstado:
                var dPed = filtros.Desde?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                var hPed = filtros.Hasta?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                tabla = await _repo.PedidosPorEstadoAsync(dPed is null ? null : new DateTimeOffset(dPed.Value),
                    hPed is null ? null : new DateTimeOffset(hPed.Value), ahora, ct);
                break;
            default:
                return Resultado<ArchivoReporte>.Falla(TipoError.NoEncontrado, CodigosError.NoEncontrado, "Ese reporte no existe.");
        }
        return Resultado<ArchivoReporte>.Ok(_exportador.Exportar(tabla, fmt) with { NombreArchivo = NombreDeArchivo(tipo, fmt, ahora) });
    }

    private static string NombreDeArchivo(string tipo, FormatoReporte formato, DateTimeOffset ahora)
    {
        var extension = formato switch { FormatoReporte.Pdf => "pdf", FormatoReporte.Excel => "xlsx", _ => "csv" };
        return $"reporte-{tipo}-{ahora.ToOffset(TimeSpan.FromHours(-6)):yyyyMMdd-HHmm}.{extension}";   // hora de El Salvador (UTC−6)
    }
}
