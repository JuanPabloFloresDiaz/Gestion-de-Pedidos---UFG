using GestionPedidos.Application.Seguridad;
using GestionPedidos.Domain.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace GestionPedidos.Infrastructure.Persistence.Repositories;

/// <summary>Repositorio de usuarios sobre EF Core (consultas siempre parametrizadas por LINQ).</summary>
public sealed class UsuarioRepository : IUsuarioRepository
{
    private readonly GestionPedidosDbContext _db;

    /// <summary>Crea el repositorio.</summary>
    public UsuarioRepository(GestionPedidosDbContext db) => _db = db;

    /// <inheritdoc/>
    public Task<Usuario?> ObtenerPorIdentificadorAsync(string identificador, CancellationToken ct)
    {
        var id = identificador.Trim();
        var correo = id.ToLowerInvariant();
        return _db.Usuarios.Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.NombreUsuario == id || u.Correo == correo, ct);
    }

    /// <inheritdoc/>
    public Task<Usuario?> ObtenerPorIdAsync(int id, CancellationToken ct) =>
        _db.Usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.Id == id, ct);

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<Usuario> Elementos, int Total)> BuscarAsync(string? q, int? rolId, bool? activo,
        int pagina, int tamano, CancellationToken ct)
    {
        var consulta = _db.Usuarios.AsNoTracking().Include(u => u.Rol).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            consulta = consulta.Where(u => u.NombreUsuario.Contains(q) || u.NombreCompleto.Contains(q) || u.Correo.Contains(q));
        if (rolId is not null) consulta = consulta.Where(u => u.RolId == rolId);
        if (activo is not null) consulta = consulta.Where(u => u.Activo == activo);

        var total = await consulta.CountAsync(ct);
        var elementos = await consulta.OrderBy(u => u.NombreCompleto).ThenBy(u => u.Id)
            .Skip((pagina - 1) * tamano).Take(tamano).ToListAsync(ct);
        return (elementos, total);
    }

    /// <inheritdoc/>
    public Task<bool> ExisteNombreUsuarioAsync(string nombreUsuario, int? exceptoId, CancellationToken ct) =>
        _db.Usuarios.AnyAsync(u => u.NombreUsuario == nombreUsuario && u.Id != exceptoId, ct);

    /// <inheritdoc/>
    public Task<bool> ExisteCorreoAsync(string correo, int? exceptoId, CancellationToken ct)
    {
        var c = correo.Trim().ToLowerInvariant();
        return _db.Usuarios.AnyAsync(u => u.Correo == c && u.Id != exceptoId, ct);
    }

    /// <inheritdoc/>
    public Task<int> ContarAdministradoresActivosAsync(CancellationToken ct) =>
        _db.Usuarios.CountAsync(u => u.Activo && u.Rol!.Nombre == NombresRol.Administrador, ct);

    /// <inheritdoc/>
    public void FijarVersionOriginal(Usuario usuario, byte[] rowVersion) =>
        _db.Entry(usuario).Property(u => u.RowVersion).OriginalValue = rowVersion;

    /// <inheritdoc/>
    public void Agregar(Usuario usuario) => _db.Usuarios.Add(usuario);

    /// <inheritdoc/>
    public void Eliminar(Usuario usuario) => _db.Usuarios.Remove(usuario);

    /// <inheritdoc/>
    public Task<bool> TienePedidosAsync(int usuarioId, CancellationToken ct) => _db.Pedidos.AnyAsync(p => p.UsuarioId == usuarioId, ct);
}
