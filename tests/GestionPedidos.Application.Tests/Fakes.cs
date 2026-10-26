using System.Reflection;
using GestionPedidos.Application.Common;
using GestionPedidos.Application.Catalogos;
using GestionPedidos.Application.Historial;
using GestionPedidos.Application.Seguridad;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Clientes;
using GestionPedidos.Domain.Historial;
using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Application.Tests;

/// <summary>Dobles de prueba en memoria de los puertos de la aplicación (sin base de datos).</summary>
internal static class Fabrica
{
    public static readonly Rol Admin = Rol(1, NombresRol.Administrador);
    public static readonly Rol Operador = Rol(2, NombresRol.Operador);
    public static readonly Rol ClienteRol = Rol(3, NombresRol.Cliente);

    public static Rol Rol(int id, string nombre) => new() { Id = id, Nombre = nombre };

    public static Usuario Usuario(int id, string nombre, Rol rol, bool activo = true, string clave = "hash:Clave_Valida_123")
    {
        var u = GestionPedidos.Domain.Seguridad.Usuario.Crear(nombre, nombre, $"{nombre}@ejemplo.com", clave, rol.Id, false);
        Asignar(u, nameof(GestionPedidos.Domain.Seguridad.Usuario.Id), id);
        Asignar(u, nameof(GestionPedidos.Domain.Seguridad.Usuario.Rol), rol);
        if (!activo) u.CambiarEstado(false, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));
        return u;
    }

    public static void Asignar(object objetivo, string propiedad, object? valor) =>
        objetivo.GetType().GetProperty(propiedad, BindingFlags.Public | BindingFlags.Instance)!.SetValue(objetivo, valor);
}

internal sealed class UsuariosFake : IUsuarioRepository
{
    public List<Usuario> Datos { get; } = new();
    public List<Usuario> Agregados { get; } = new();
    public List<Usuario> Eliminados { get; } = new();
    public HashSet<int> ConPedidos { get; } = new();
    private int _siguiente = 100;

    public Task<Usuario?> ObtenerPorIdentificadorAsync(string identificador, CancellationToken ct) =>
        Task.FromResult(Datos.Concat(Agregados).FirstOrDefault(u =>
            u.NombreUsuario == identificador.Trim() || u.Correo == identificador.Trim().ToLowerInvariant()));
    public Task<Usuario?> ObtenerPorIdAsync(int id, CancellationToken ct) =>
        Task.FromResult(Datos.Concat(Agregados).FirstOrDefault(u => u.Id == id));
    public Task<(IReadOnlyList<Usuario> Elementos, int Total)> BuscarAsync(string? q, int? rolId, bool? activo, int pagina, int tamano, CancellationToken ct) =>
        Task.FromResult(((IReadOnlyList<Usuario>)Datos.ToList(), Datos.Count));
    public Task<bool> ExisteNombreUsuarioAsync(string nombreUsuario, int? exceptoId, CancellationToken ct) =>
        Task.FromResult(Datos.Concat(Agregados).Any(u => u.NombreUsuario == nombreUsuario && u.Id != exceptoId));
    public Task<bool> ExisteCorreoAsync(string correo, int? exceptoId, CancellationToken ct) =>
        Task.FromResult(Datos.Concat(Agregados).Any(u => u.Correo == correo.Trim().ToLowerInvariant() && u.Id != exceptoId));
    public Task<int> ContarAdministradoresActivosAsync(CancellationToken ct) =>
        Task.FromResult(Datos.Concat(Agregados).Count(u => u.Activo && u.Rol?.Nombre == NombresRol.Administrador));
    public void FijarVersionOriginal(Usuario usuario, byte[] rowVersion) { }
    public void Eliminar(Usuario usuario) { Eliminados.Add(usuario); Datos.Remove(usuario); Agregados.Remove(usuario); }
    public Task<bool> TienePedidosAsync(int usuarioId, CancellationToken ct) => Task.FromResult(ConPedidos.Contains(usuarioId));
    public void Agregar(Usuario usuario)
    {
        Fabrica.Asignar(usuario, nameof(Usuario.Id), _siguiente++);
        var rol = new[] { Fabrica.Admin, Fabrica.Operador, Fabrica.ClienteRol }.First(r => r.Id == usuario.RolId);
        Fabrica.Asignar(usuario, nameof(Usuario.Rol), rol);
        Agregados.Add(usuario);
    }
}

internal sealed class RolesFake : IRolRepository
{
    private static readonly Rol[] Todos = { Fabrica.Admin, Fabrica.Operador, Fabrica.ClienteRol };
    public Task<IReadOnlyList<RolDto>> ListarAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<RolDto>>(Todos.Select(r => new RolDto(r.Id, r.Nombre, null, Array.Empty<string>())).ToList());
    public Task<Rol?> ObtenerAsync(int id, CancellationToken ct) => Task.FromResult(Todos.FirstOrDefault(r => r.Id == id));
    public Task<Rol?> ObtenerPorNombreAsync(string nombre, CancellationToken ct) => Task.FromResult(Todos.FirstOrDefault(r => r.Nombre == nombre));
    public Task<IReadOnlyList<string>> CodigosPermisosAsync(int rolId, CancellationToken ct) =>
        Task.FromResult(CatalogoPermisos.DeRol(Todos.First(r => r.Id == rolId).Nombre));

    // ---- Edición de permisos: roles NUEVOS por cada fake (los estáticos de arriba no se mutan entre pruebas) ----
    private static readonly IReadOnlyList<Permiso> Catalogo =
        CatalogoPermisos.Todos.Select((p, i) => new Permiso { Id = i + 1, Codigo = p.Codigo, Descripcion = p.Descripcion }).ToList();
    private readonly Dictionary<int, Rol> _editables = new[] { Fabrica.Admin, Fabrica.Operador, Fabrica.ClienteRol }.ToDictionary(
        r => r.Id, r => new Rol
        {
            Id = r.Id, Nombre = r.Nombre,
            Permisos = CatalogoPermisos.DeRol(r.Nombre).Select(c => Catalogo.First(p => p.Codigo == c))
                .Select(p => new RolPermiso { RolId = r.Id, PermisoId = p.Id, Permiso = p }).ToList(),
        });
    public Rol Editable(int id) => _editables[id];
    public Task<IReadOnlyList<PermisoDto>> ListarPermisosAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PermisoDto>>(Catalogo.Select(p => new PermisoDto(p.Id, p.Codigo, p.Descripcion)).ToList());
    public Task<Rol?> ObtenerConPermisosAsync(int id, CancellationToken ct) => Task.FromResult(_editables.GetValueOrDefault(id));
    public Task<IReadOnlyList<Permiso>> ObtenerPermisosPorCodigosAsync(IEnumerable<string> codigos, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Permiso>>(Catalogo.Where(p => codigos.Contains(p.Codigo)).ToList());
}

internal sealed class ClientesFake : IRegistroClienteRepository
{
    public List<Cliente> Datos { get; } = new();
    public List<Cliente> Agregados { get; } = new();
    public Task<Cliente?> ObtenerClientePorCorreoAsync(string correo, CancellationToken ct) =>
        Task.FromResult(Datos.Concat(Agregados).FirstOrDefault(c => c.Correo == correo));
    public Task<bool> ExisteCiudadAsync(int ciudadId, CancellationToken ct) => Task.FromResult(ciudadId is > 0 and <= 262);
    public void Agregar(Cliente cliente) => Agregados.Add(cliente);
}

internal sealed class UnitOfWorkFake : IUnitOfWork
{
    public int Guardados { get; private set; }
    public Task GuardarAsync(CancellationToken ct) { Guardados++; return Task.CompletedTask; }
}

internal sealed class HasherFake : IHasherClave
{
    public int TiemposGastados { get; private set; }
    public string Hashear(string clave) => "hash:" + clave;
    public bool Verificar(string hash, string clave) => hash == "hash:" + clave;
    public void GastarTiempo() => TiemposGastados++;
}

internal sealed class TokensFake : IEmisorTokens
{
    public (string Token, DateTimeOffset ExpiraEn) Emitir(UsuarioActualDto usuario, bool debeCambiarClave) =>
        ($"token-{usuario.NombreUsuario}{(debeCambiarClave ? "-debe" : "")}", DateTimeOffset.UtcNow.AddMinutes(60));
}

internal sealed class RegistroActividadesFake : IRegistroActividades
{
    public List<Actividad> Registradas { get; } = new();
    public void Registrar(Actividad actividad) => Registradas.Add(actividad);
}

internal sealed class CatalogoGeoFake : ICatalogoGeoRepository
{
    public (int? Departamento, string? Q, int Pagina, int Tamano)? UltimaBusqueda { get; private set; }
    public Task<IReadOnlyList<DepartamentoDto>> ListarDepartamentosAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DepartamentoDto>>(new[] { new DepartamentoDto(1, "AH", "Ahuachapán", 12) });
    public Task<bool> ExisteDepartamentoAsync(int id, CancellationToken ct) => Task.FromResult(id is >= 1 and <= 14);
    public Task<(IReadOnlyList<CiudadDto> Elementos, int Total)> BuscarCiudadesAsync(int? departamentoId, string? q, int pagina, int tamano, CancellationToken ct)
    {
        UltimaBusqueda = (departamentoId, q, pagina, tamano);
        return Task.FromResult(((IReadOnlyList<CiudadDto>)Array.Empty<CiudadDto>(), 0));
    }
}

internal sealed class ActividadesFake : IActividadRepository
{
    public (int? Usuario, string? Q, string? Entidad, string? Accion, DateTimeOffset? Desde, DateTimeOffset? Hasta, int Pagina, int Tamano)? Ultima { get; private set; }
    public Task<(IReadOnlyList<ActividadDto> Elementos, int Total)> BuscarAsync(int? usuarioId, string? q, string? entidad, string? accion,
        DateTimeOffset? desde, DateTimeOffset? hasta, int pagina, int tamano, CancellationToken ct)
    {
        Ultima = (usuarioId, q, entidad, accion, desde, hasta, pagina, tamano);
        return Task.FromResult(((IReadOnlyList<ActividadDto>)Array.Empty<ActividadDto>(), 0));
    }
    public Task<(IReadOnlyList<string> Entidades, IReadOnlyList<string> Acciones)> ValoresDeFiltroAsync(CancellationToken ct) =>
        Task.FromResult(((IReadOnlyList<string>)new[] { "Usuario" }, (IReadOnlyList<string>)new[] { "Login" }));
}
