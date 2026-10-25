using GestionPedidos.Application.Seguridad;
using GestionPedidos.Domain.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GestionPedidos.Infrastructure.Persistence.Seed;

/// <summary>
/// Crea el administrador inicial si la base no tiene ningún usuario. La contraseña no vive en el repositorio:
/// llega por <c>ADMIN_INITIAL_PASSWORD</c> y el usuario nace obligado a cambiarla.
/// </summary>
public sealed class AdministradorInicial
{
    /// <summary>Nombre de usuario del administrador inicial.</summary>
    public const string NombreUsuario = "admin";

    private readonly GestionPedidosDbContext _db;
    private readonly IHasherClave _hasher;
    private readonly ILogger<AdministradorInicial> _log;

    /// <summary>Crea el sembrador.</summary>
    public AdministradorInicial(GestionPedidosDbContext db, IHasherClave hasher, ILogger<AdministradorInicial> log)
    {
        _db = db; _hasher = hasher; _log = log;
    }

    /// <summary>Siembra el administrador si hace falta (idempotente).</summary>
    /// <param name="clave">Contraseña inicial; si es vacía no se crea nada.</param>
    /// <param name="ct">Cancelación.</param>
    public async Task SembrarAsync(string? clave, CancellationToken ct)
    {
        if (await _db.Usuarios.AnyAsync(ct)) return;
        if (string.IsNullOrWhiteSpace(clave))
        {
            _log.LogWarning("No hay usuarios y ADMIN_INITIAL_PASSWORD está vacía: no se crea el administrador inicial.");
            return;
        }
        var admin = Usuario.Crear(NombreUsuario, "Administrador del sistema", "admin@gestionpedidos.local",
            _hasher.Hashear(clave), rolId: 1, debeCambiarClave: true);
        _db.Usuarios.Add(admin);
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Administrador inicial creado (usuario '{Usuario}'); deberá cambiar la contraseña.", NombreUsuario);
    }
}
