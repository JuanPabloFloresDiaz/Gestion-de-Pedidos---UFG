using System.Reflection;
using GestionPedidos.Application.Common;
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
        if (!activo) u.CambiarEstado(false);
        return u;
    }

    public static void Asignar(object objetivo, string propiedad, object? valor) =>
        objetivo.GetType().GetProperty(propiedad, BindingFlags.Public | BindingFlags.Instance)!.SetValue(objetivo, valor);
}

internal sealed class UsuariosFake : IUsuarioRepository
{
    public List<Usuario> Datos { get; } = new();
    public List<Usuario> Agregados { get; } = new();
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
