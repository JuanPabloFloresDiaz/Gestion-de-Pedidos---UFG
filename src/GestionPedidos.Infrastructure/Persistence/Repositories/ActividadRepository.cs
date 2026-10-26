using GestionPedidos.Application.Historial;
using GestionPedidos.Contracts;
using Microsoft.EntityFrameworkCore;

namespace GestionPedidos.Infrastructure.Persistence.Repositories;

/// <summary>Consultas de solo lectura sobre el historial de actividades.</summary>
public sealed class ActividadRepository : IActividadRepository
{
    private readonly GestionPedidosDbContext _db;

    /// <summary>Crea el repositorio.</summary>
    public ActividadRepository(GestionPedidosDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<ActividadDto> Elementos, int Total)> BuscarAsync(int? usuarioId, string? q, string? entidad,
        string? accion, DateTimeOffset? desde, DateTimeOffset? hasta, int pagina, int tamano, CancellationToken ct)
    {
        var consulta = _db.Actividades.AsNoTracking().AsQueryable();
        if (usuarioId is not null) consulta = consulta.Where(a => a.UsuarioId == usuarioId);
        if (entidad is not null) consulta = consulta.Where(a => a.Entidad == entidad);
        if (accion is not null) consulta = consulta.Where(a => a.Accion == accion);
        if (desde is not null) consulta = consulta.Where(a => a.Fecha >= desde);
        if (hasta is not null) consulta = consulta.Where(a => a.Fecha < hasta);
        if (!string.IsNullOrWhiteSpace(q)) consulta = consulta.Where(a => a.Detalle != null && a.Detalle.Contains(q));

        var total = await consulta.CountAsync(ct);
        var elementos = await consulta.OrderByDescending(a => a.Fecha).ThenByDescending(a => a.Id)
            .Skip((pagina - 1) * tamano).Take(tamano)
            .Select(a => new ActividadDto(a.Id, a.Fecha, a.UsuarioId, a.Usuario!.NombreUsuario, a.Accion, a.Entidad, a.EntidadId, a.Detalle))
            .ToListAsync(ct);
        return (elementos, total);
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<string> Entidades, IReadOnlyList<string> Acciones)> ValoresDeFiltroAsync(CancellationToken ct) =>
        (await _db.Actividades.Select(a => a.Entidad).Distinct().OrderBy(e => e).ToListAsync(ct),
         await _db.Actividades.Select(a => a.Accion).Distinct().OrderBy(e => e).ToListAsync(ct));
}
