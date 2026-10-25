using GestionPedidos.Application.Seguridad;
using GestionPedidos.Domain.Clientes;
using Microsoft.EntityFrameworkCore;

namespace GestionPedidos.Infrastructure.Persistence.Repositories;

/// <summary>Acceso a clientes y ciudades para el registro web.</summary>
public sealed class RegistroClienteRepository : IRegistroClienteRepository
{
    private readonly GestionPedidosDbContext _db;

    /// <summary>Crea el repositorio.</summary>
    public RegistroClienteRepository(GestionPedidosDbContext db) => _db = db;

    /// <inheritdoc/>
    public Task<Cliente?> ObtenerClientePorCorreoAsync(string correo, CancellationToken ct) =>
        _db.Clientes.FirstOrDefaultAsync(c => c.Correo == correo, ct);

    /// <inheritdoc/>
    public Task<bool> ExisteCiudadAsync(int ciudadId, CancellationToken ct) => _db.Ciudades.AnyAsync(c => c.Id == ciudadId, ct);

    /// <inheritdoc/>
    public void Agregar(Cliente cliente) => _db.Clientes.Add(cliente);
}
