namespace GestionPedidos.Contracts;

/// <summary>
/// Códigos de permiso que la interfaz necesita conocer para mostrar u ocultar menús y botones.
/// Deben coincidir con los de <c>CatalogoPermisos</c> del dominio (una prueba de arquitectura lo verifica).
/// </summary>
public static class Permisos
{
    /// <summary>Entrar a la Administración.</summary>
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
    /// <summary>Eliminar definitivamente usuarios dados de baja.</summary>
    public const string UsuariosEliminar = "usuarios.eliminar";
    /// <summary>Editar los permisos de un rol.</summary>
    public const string RolesEditar = "roles.editar";
    /// <summary>Ver los catálogos de departamentos y ciudades.</summary>
    public const string CatalogosVer = "catalogos.ver";
    /// <summary>Ver categorías, subcategorías, filtros y opciones.</summary>
    public const string CategoriasVer = "categorias.ver";
    /// <summary>Crear categorías, subcategorías, filtros y opciones.</summary>
    public const string CategoriasCrear = "categorias.crear";
    /// <summary>Editar categorías, subcategorías, filtros y opciones.</summary>
    public const string CategoriasEditar = "categorias.editar";
    /// <summary>Dar de baja o reactivar elementos de la clasificación.</summary>
    public const string CategoriasDesactivar = "categorias.desactivar";
    /// <summary>Eliminar definitivamente elementos de la clasificación.</summary>
    public const string CategoriasEliminar = "categorias.eliminar";
}
