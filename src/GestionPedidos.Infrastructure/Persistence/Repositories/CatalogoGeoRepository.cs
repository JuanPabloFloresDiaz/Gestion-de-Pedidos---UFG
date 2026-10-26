using GestionPedidos.Application.Catalogos;
using GestionPedidos.Contracts;
using Microsoft.EntityFrameworkCore;

namespace GestionPedidos.Infrastructure.Persistence.Repositories;

/// <summary>Consultas de solo lectura sobre departamentos y ciudades.</summary>
public sealed class CatalogoGeoRepository : ICatalogoGeoRepository
{
    private readonly GestionPedidosDbContext _db;

    /// <summary>Crea el repositorio.</summary>
    public CatalogoGeoRepository(GestionPedidosDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DepartamentoDto>> ListarDepartamentosAsync(CancellationToken ct) =>
        await _db.Departamentos.AsNoTracking().OrderBy(d => d.Nombre)
            .Select(d => new DepartamentoDto(d.Id, d.Codigo, d.Nombre, _db.Ciudades.Count(c => c.DepartamentoId == d.Id)))
            .ToListAsync(ct);

    /// <inheritdoc/>
    public Task<bool> ExisteDepartamentoAsync(int id, CancellationToken ct) => _db.Departamentos.AnyAsync(d => d.Id == id, ct);

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<CiudadDto> Elementos, int Total)> BuscarCiudadesAsync(int? departamentoId, string? q, int pagina,
        int tamano, CancellationToken ct)
    {
        var consulta = _db.Ciudades.AsNoTracking().AsQueryable();
        if (departamentoId is not null) consulta = consulta.Where(c => c.DepartamentoId == departamentoId);
        if (!string.IsNullOrWhiteSpace(q))
            consulta = consulta.Where(c => EF.Functions.Collate(c.Nombre, "Latin1_General_CI_AI").Contains(q)
                || EF.Functions.Collate(c.Municipio, "Latin1_General_CI_AI").Contains(q));   // sin distinguir acentos ni mayúsculas

        var total = await consulta.CountAsync(ct);
        var elementos = await consulta.OrderBy(c => c.Departamento!.Nombre).ThenBy(c => c.Nombre)
            .Skip((pagina - 1) * tamano).Take(tamano)
            .Select(c => new CiudadDto(c.Id, c.DepartamentoId, c.Departamento!.Nombre, c.Municipio, c.Nombre))
            .ToListAsync(ct);
        return (elementos, total);
    }
}
