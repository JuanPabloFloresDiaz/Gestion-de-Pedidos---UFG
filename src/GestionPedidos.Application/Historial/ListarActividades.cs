using GestionPedidos.Application.Common;
using GestionPedidos.Contracts;

namespace GestionPedidos.Application.Historial;

/// <summary>Caso de uso: consultar el historial de actividades con filtros y paginación (solo administrador).</summary>
public sealed class ListarActividades
{
    private readonly IActividadRepository _actividades;

    /// <summary>Crea el caso de uso.</summary>
    public ListarActividades(IActividadRepository actividades) => _actividades = actividades;

    /// <summary>Devuelve una página del historial.</summary>
    /// <param name="usuarioId">Filtra por quien realizó la acción.</param>
    /// <param name="q">Texto buscado en el detalle.</param>
    /// <param name="entidad">Filtra por entidad.</param>
    /// <param name="accion">Filtra por acción.</param>
    /// <param name="desde">Fecha inicial (incluida).</param>
    /// <param name="hasta">Fecha final (incluida: abarca todo el día).</param>
    /// <param name="pagina">Página (desde 1).</param>
    /// <param name="tamano">Elementos por página (máximo 100).</param>
    /// <param name="ct">Cancelación.</param>
    public async Task<Resultado<PaginaDto<ActividadDto>>> EjecutarAsync(int? usuarioId, string? q, string? entidad, string? accion,
        DateOnly? desde, DateOnly? hasta, int? pagina, int? tamano, CancellationToken ct)
    {
        if (desde is not null && hasta is not null && desde > hasta)
            return Resultado<PaginaDto<ActividadDto>>.Falla(TipoError.Validacion, CodigosError.Validacion,
                "La fecha inicial no puede ser posterior a la final.");

        var (p, t) = Paginacion.Normalizar(pagina, tamano);
        // El día final se incluye completo; las fechas del filtro son UTC (como se guardan).
        var d = desde?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var h = hasta?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var (elementos, total) = await _actividades.BuscarAsync(usuarioId, q?.Trim(), Texto(entidad), Texto(accion),
            d is null ? null : new DateTimeOffset(d.Value), h is null ? null : new DateTimeOffset(h.Value), p, t, ct);
        return Resultado<PaginaDto<ActividadDto>>.Ok(new PaginaDto<ActividadDto>(elementos, total, p, t));
    }

    /// <summary>Valores disponibles para los filtros de entidad y acción.</summary>
    public async Task<(IReadOnlyList<string> Entidades, IReadOnlyList<string> Acciones)> FiltrosAsync(CancellationToken ct) =>
        await _actividades.ValoresDeFiltroAsync(ct);

    private static string? Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
