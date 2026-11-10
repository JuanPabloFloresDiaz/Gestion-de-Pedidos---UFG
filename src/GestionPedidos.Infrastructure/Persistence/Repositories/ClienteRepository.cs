using GestionPedidos.Application.Clientes;
using GestionPedidos.Domain.Clientes;
using GestionPedidos.Domain.Pedidos;
using Microsoft.EntityFrameworkCore;

namespace GestionPedidos.Infrastructure.Persistence.Repositories;

/// <summary>Repositorio de clientes sobre EF Core con consultas parametrizadas ([Clientes.md]).</summary>
public sealed class ClienteRepository : IClienteRepository
{
    private readonly GestionPedidosDbContext _db;

    /// <summary>Crea el repositorio.</summary>
    public ClienteRepository(GestionPedidosDbContext db) => _db = db;

    /// <inheritdoc/>
    public Task<Cliente?> ObtenerPorIdAsync(int id, CancellationToken ct) =>
        _db.Clientes
            .Include(c => c.Ciudad)
            .ThenInclude(ci => ci!.Departamento)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<Cliente> Elementos, int Total)> BuscarAsync(string? q, int? departamentoId,
        int? ciudadId, bool? activo, int pagina, int tamano, CancellationToken ct)
    {
        var consulta = _db.Clientes
            .AsNoTracking()
            .Include(c => c.Ciudad)
            .ThenInclude(ci => ci!.Departamento)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            foreach (var palabra in q.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var p = palabra;   // copia local para la consulta
                consulta = consulta.Where(c =>
                    EF.Functions.Collate(c.Nombres, "Latin1_General_CI_AI").Contains(p)
                    || EF.Functions.Collate(c.Apellidos, "Latin1_General_CI_AI").Contains(p)
                    || EF.Functions.Collate(c.Telefono, "Latin1_General_CI_AI").Contains(p)
                    || (c.Correo != null && EF.Functions.Collate(c.Correo, "Latin1_General_CI_AI").Contains(p)));
            }
        }

        if (departamentoId is not null)
            consulta = consulta.Where(c => c.Ciudad != null && c.Ciudad.DepartamentoId == departamentoId);

        if (ciudadId is not null)
            consulta = consulta.Where(c => c.CiudadId == ciudadId);

        if (activo is not null)
            consulta = consulta.Where(c => c.Activo == activo);

        var total = await consulta.CountAsync(ct);
        var elementos = await consulta
            .OrderBy(c => c.Nombres)
            .ThenBy(c => c.Apellidos)
            .ThenBy(c => c.Id)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .ToListAsync(ct);

        return (elementos, total);
    }

    /// <inheritdoc/>
    public Task<bool> ExisteCorreoAsync(string correo, int? exceptoId, CancellationToken ct) =>
        _db.Clientes.AnyAsync(c => c.Correo == correo && c.Id != exceptoId, ct);

    /// <inheritdoc/>
    public Task<bool> ExisteCiudadAsync(int ciudadId, CancellationToken ct) =>
        _db.Ciudades.AnyAsync(c => c.Id == ciudadId, ct);

    /// <inheritdoc/>
    public Task<bool> TienePedidosPendientesAsync(int clienteId, CancellationToken ct) =>
        _db.Pedidos.AnyAsync(p => p.ClienteId == clienteId && p.Estado == EstadoPedido.Pendiente && p.Activo, ct);

    /// <inheritdoc/>
    public void FijarVersionOriginal(Cliente cliente, byte[] version) =>
        _db.Entry(cliente).Property(c => c.RowVersion).OriginalValue = version;

    /// <inheritdoc/>
    public void Agregar(Cliente cliente) => _db.Clientes.Add(cliente);
}
