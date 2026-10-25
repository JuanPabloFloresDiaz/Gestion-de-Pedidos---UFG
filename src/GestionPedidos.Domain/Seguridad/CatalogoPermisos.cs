namespace GestionPedidos.Domain.Seguridad;

/// <summary>Códigos de permiso del sistema. Fuente única de verdad de la matriz de [AutenticacionYPermisos.md §3].</summary>
public static class CatalogoPermisos
{
    /// <summary>Permite entrar al área de Administración.</summary>
    public const string AdminAcceso = "admin.acceso";
    /// <summary>Ver clientes.</summary>
    public const string ClientesVer = "clientes.ver";
    /// <summary>Crear clientes.</summary>
    public const string ClientesCrear = "clientes.crear";
    /// <summary>Editar clientes.</summary>
    public const string ClientesEditar = "clientes.editar";
    /// <summary>Dar de baja o reactivar clientes.</summary>
    public const string ClientesDesactivar = "clientes.desactivar";
    /// <summary>Ver productos.</summary>
    public const string ProductosVer = "productos.ver";
    /// <summary>Crear productos.</summary>
    public const string ProductosCrear = "productos.crear";
    /// <summary>Editar productos.</summary>
    public const string ProductosEditar = "productos.editar";
    /// <summary>Dar de baja o reactivar productos.</summary>
    public const string ProductosDesactivar = "productos.desactivar";
    /// <summary>Ver pedidos.</summary>
    public const string PedidosVer = "pedidos.ver";
    /// <summary>Crear pedidos.</summary>
    public const string PedidosCrear = "pedidos.crear";
    /// <summary>Editar pedidos.</summary>
    public const string PedidosEditar = "pedidos.editar";
    /// <summary>Cambiar el estado de un pedido.</summary>
    public const string PedidosCambiarEstado = "pedidos.cambiar_estado";
    /// <summary>Dar de baja pedidos.</summary>
    public const string PedidosDesactivar = "pedidos.desactivar";
    /// <summary>Ver usuarios.</summary>
    public const string UsuariosVer = "usuarios.ver";
    /// <summary>Crear usuarios.</summary>
    public const string UsuariosCrear = "usuarios.crear";
    /// <summary>Editar usuarios.</summary>
    public const string UsuariosEditar = "usuarios.editar";
    /// <summary>Dar de baja o reactivar usuarios.</summary>
    public const string UsuariosDesactivar = "usuarios.desactivar";
    /// <summary>Ver roles y permisos.</summary>
    public const string RolesVer = "roles.ver";
    /// <summary>Ver el historial de actividades.</summary>
    public const string HistorialVer = "historial.ver";
    /// <summary>Ver el catálogo de la tienda (también es público).</summary>
    public const string CatalogoVer = "catalogo.ver";
    /// <summary>Ver los pedidos propios (cliente de la web).</summary>
    public const string MisPedidosVer = "mispedidos.ver";
    /// <summary>Crear pedidos propios (cliente de la web).</summary>
    public const string MisPedidosCrear = "mispedidos.crear";
    /// <summary>Cancelar pedidos propios (cliente de la web).</summary>
    public const string MisPedidosCancelar = "mispedidos.cancelar";
    /// <summary>Editar el perfil propio (cliente de la web).</summary>
    public const string PerfilEditar = "perfil.editar";

    /// <summary>Todos los permisos con su descripción, en orden estable (el orden define los Id de la semilla).</summary>
    public static readonly IReadOnlyList<(string Codigo, string Descripcion)> Todos = new (string, string)[]
    {
        (AdminAcceso, "Entrar al área de Administración"),
        (ClientesVer, "Ver clientes"), (ClientesCrear, "Crear clientes"), (ClientesEditar, "Editar clientes"),
        (ClientesDesactivar, "Dar de baja o reactivar clientes"),
        (ProductosVer, "Ver productos"), (ProductosCrear, "Crear productos"), (ProductosEditar, "Editar productos"),
        (ProductosDesactivar, "Dar de baja o reactivar productos"),
        (PedidosVer, "Ver pedidos"), (PedidosCrear, "Crear pedidos"), (PedidosEditar, "Editar pedidos"),
        (PedidosCambiarEstado, "Cambiar el estado de un pedido"), (PedidosDesactivar, "Dar de baja pedidos"),
        (UsuariosVer, "Ver usuarios"), (UsuariosCrear, "Crear usuarios"), (UsuariosEditar, "Editar usuarios"),
        (UsuariosDesactivar, "Dar de baja o reactivar usuarios"),
        (RolesVer, "Ver roles y permisos"), (HistorialVer, "Ver el historial de actividades"),
        (CatalogoVer, "Ver el catálogo de la tienda"),
        (MisPedidosVer, "Ver los pedidos propios"), (MisPedidosCrear, "Crear pedidos propios"),
        (MisPedidosCancelar, "Cancelar pedidos propios"), (PerfilEditar, "Editar el perfil propio"),
    };

    /// <summary>Permisos que tiene cada rol (matriz confirmada el 03/10/2026).</summary>
    public static IReadOnlyList<string> DeRol(string rol) => rol switch
    {
        NombresRol.Administrador => Todos.Select(p => p.Codigo)
            .Where(c => c is not (MisPedidosVer or MisPedidosCrear or MisPedidosCancelar or PerfilEditar)).ToList(),
        NombresRol.Operador => new[]
        {
            AdminAcceso, ClientesVer, ClientesCrear, ClientesEditar, ProductosVer,
            PedidosVer, PedidosCrear, PedidosEditar, PedidosCambiarEstado, CatalogoVer,
        },
        NombresRol.Cliente => new[] { CatalogoVer, MisPedidosVer, MisPedidosCrear, MisPedidosCancelar, PerfilEditar },
        _ => Array.Empty<string>(),
    };
}

/// <summary>Nombres de los roles del sistema.</summary>
public static class NombresRol
{
    /// <summary>Gestión completa.</summary>
    public const string Administrador = "Administrador";
    /// <summary>Trabajo diario con clientes y pedidos.</summary>
    public const string Operador = "Operador";
    /// <summary>Cliente de la tienda web.</summary>
    public const string Cliente = "Cliente";
}
