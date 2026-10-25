using GestionPedidos.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace GestionPedidos.Infrastructure.Persistence;

/// <summary>Unidad de trabajo sobre el contexto de EF Core: un solo <c>SaveChanges</c> por caso de uso.</summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly GestionPedidosDbContext _db;

    /// <summary>Crea la unidad de trabajo.</summary>
    public UnitOfWork(GestionPedidosDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task GuardarAsync(CancellationToken ct)
    {
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException ex) { throw new ConcurrenciaException(ex); }
    }
}
