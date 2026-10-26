using GestionPedidos.Contracts;
using GestionPedidos.Domain.Clientes;
using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Application.Seguridad;

/// <summary>Repositorio de usuarios.</summary>
public interface IUsuarioRepository
{
    /// <summary>Busca por nombre de usuario o correo (con seguimiento de cambios).</summary>
    Task<Usuario?> ObtenerPorIdentificadorAsync(string identificador, CancellationToken ct);
    /// <summary>Busca por identificador (con seguimiento de cambios, incluye el rol).</summary>
    Task<Usuario?> ObtenerPorIdAsync(int id, CancellationToken ct);
    /// <summary>Lista usuarios con filtros y paginación.</summary>
    Task<(IReadOnlyList<Usuario> Elementos, int Total)> BuscarAsync(string? q, int? rolId, bool? activo, int pagina,
        int tamano, CancellationToken ct);
    /// <summary>Indica si ya existe el nombre de usuario (sin contar a <paramref name="exceptoId"/>).</summary>
    Task<bool> ExisteNombreUsuarioAsync(string nombreUsuario, int? exceptoId, CancellationToken ct);
    /// <summary>Indica si ya existe el correo (sin contar a <paramref name="exceptoId"/>).</summary>
    Task<bool> ExisteCorreoAsync(string correo, int? exceptoId, CancellationToken ct);
    /// <summary>Cuenta los administradores activos.</summary>
    Task<int> ContarAdministradoresActivosAsync(CancellationToken ct);
    /// <summary>Marca la versión original leída para detectar cambios concurrentes.</summary>
    void FijarVersionOriginal(Usuario usuario, byte[] rowVersion);
    /// <summary>Agrega un usuario nuevo.</summary>
    void Agregar(Usuario usuario);
    /// <summary>Marca al usuario para eliminarlo definitivamente al guardar.</summary>
    void Eliminar(Usuario usuario);
    /// <summary>Indica si el usuario tiene pedidos registrados a su nombre (no se puede eliminar).</summary>
    Task<bool> TienePedidosAsync(int usuarioId, CancellationToken ct);
}

/// <summary>Repositorio de roles y permisos.</summary>
public interface IRolRepository
{
    /// <summary>Lista los roles con sus permisos.</summary>
    Task<IReadOnlyList<RolDto>> ListarAsync(CancellationToken ct);
    /// <summary>Devuelve el rol por identificador, o <c>null</c>.</summary>
    Task<Rol?> ObtenerAsync(int id, CancellationToken ct);
    /// <summary>Devuelve el rol por nombre, o <c>null</c>.</summary>
    Task<Rol?> ObtenerPorNombreAsync(string nombre, CancellationToken ct);
    /// <summary>Códigos de permiso de un rol.</summary>
    Task<IReadOnlyList<string>> CodigosPermisosAsync(int rolId, CancellationToken ct);
    /// <summary>Catálogo completo de permisos.</summary>
    Task<IReadOnlyList<PermisoDto>> ListarPermisosAsync(CancellationToken ct);
    /// <summary>Devuelve el rol con sus permisos cargados y con seguimiento de cambios, o <c>null</c>.</summary>
    Task<Rol?> ObtenerConPermisosAsync(int id, CancellationToken ct);
    /// <summary>Devuelve los permisos que corresponden a los códigos dados (los que no existan se omiten).</summary>
    Task<IReadOnlyList<Permiso>> ObtenerPermisosPorCodigosAsync(IEnumerable<string> codigos, CancellationToken ct);
}

/// <summary>Acceso a clientes y ciudades necesario para el registro web.</summary>
public interface IRegistroClienteRepository
{
    /// <summary>Cliente con ese correo, con seguimiento de cambios, o <c>null</c>.</summary>
    Task<Cliente?> ObtenerClientePorCorreoAsync(string correo, CancellationToken ct);
    /// <summary>Indica si existe la ciudad.</summary>
    Task<bool> ExisteCiudadAsync(int ciudadId, CancellationToken ct);
    /// <summary>Agrega un cliente nuevo.</summary>
    void Agregar(Cliente cliente);
}

/// <summary>Hash y verificación de contraseñas.</summary>
public interface IHasherClave
{
    /// <summary>Calcula el hash de una contraseña.</summary>
    string Hashear(string clave);
    /// <summary>Verifica una contraseña contra su hash.</summary>
    bool Verificar(string hash, string clave);
    /// <summary>Gasta el mismo tiempo que una verificación real (evita enumerar usuarios por tiempos).</summary>
    void GastarTiempo();
}

/// <summary>Emite el JWT de sesión.</summary>
public interface IEmisorTokens
{
    /// <summary>Crea un token firmado para el usuario.</summary>
    (string Token, DateTimeOffset ExpiraEn) Emitir(UsuarioActualDto usuario, bool debeCambiarClave);
}
