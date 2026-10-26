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
    /// <summary>Eliminar definitivamente usuarios que ya están dados de baja.</summary>
    public const string UsuariosEliminar = "usuarios.eliminar";
    /// <summary>Editar los permisos de un rol.</summary>
    public const string RolesEditar = "roles.editar";
    /// <summary>Ver los catálogos de departamentos y ciudades en la administración.</summary>
    public const string CatalogosVer = "catalogos.ver";
    /// <summary>Ver categorías, subcategorías, filtros y opciones.</summary>
    public const string CategoriasVer = "categorias.ver";
    /// <summary>Crear categorías, subcategorías, filtros y opciones.</summary>
    public const string CategoriasCrear = "categorias.crear";
    /// <summary>Editar categorías, subcategorías, filtros y opciones.</summary>
    public const string CategoriasEditar = "categorias.editar";
    /// <summary>Dar de baja o reactivar elementos de la clasificación.</summary>
    public const string CategoriasDesactivar = "categorias.desactivar";
    /// <summary>Eliminar definitivamente elementos de la clasificación (inactivos y sin uso).</summary>
    public const string CategoriasEliminar = "categorias.eliminar";
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
        // Añadidos después de la Fase 1: SIEMPRE al final (los Id de la semilla dependen de la posición)
        (RolesEditar, "Editar los permisos de un rol"),
        (CatalogosVer, "Ver los catálogos de departamentos y ciudades"),
        (UsuariosEliminar, "Eliminar definitivamente usuarios dados de baja"),
        (CategoriasVer, "Ver categorías, subcategorías, filtros y opciones"),
        (CategoriasCrear, "Crear categorías, subcategorías, filtros y opciones"),
        (CategoriasEditar, "Editar categorías, subcategorías, filtros y opciones"),
        (CategoriasDesactivar, "Dar de baja o reactivar categorías, subcategorías, filtros y opciones"),
        (CategoriasEliminar, "Eliminar definitivamente elementos inactivos y sin uso de la clasificación"),
    };

    /// <summary>Permisos que tiene cada rol (matriz confirmada el 03/10/2026).</summary>
    public static IReadOnlyList<string> DeRol(string rol) => rol switch
    {
        NombresRol.Administrador => Todos.Select(p => p.Codigo)
            .Where(c => c is not (MisPedidosVer or MisPedidosCrear or MisPedidosCancelar or PerfilEditar)).ToList(),
        NombresRol.Operador => new[]
        {
            AdminAcceso, ClientesVer, ClientesCrear, ClientesEditar, ProductosVer,
            PedidosVer, PedidosCrear, PedidosEditar, PedidosCambiarEstado, CatalogoVer, CatalogosVer, CategoriasVer,
        },
        NombresRol.Cliente => new[] { CatalogoVer, MisPedidosVer, MisPedidosCrear, MisPedidosCancelar, PerfilEditar },
        _ => Array.Empty<string>(),
    };
}

/// <summary>Reglas de protección de los permisos de los roles ([AutenticacionYPermisos.md §3.1]).</summary>
public static class PermisosProtegidos
{
    /// <summary>
    /// Permisos que el rol Administrador nunca puede perder: sin ellos nadie podría volver a administrar
    /// usuarios y roles (se quedaría el sistema sin quién lo arregle).
    /// </summary>
    public static readonly IReadOnlySet<string> DelAdministrador = new HashSet<string>
    {
        CatalogoPermisos.AdminAcceso, CatalogoPermisos.UsuariosVer, CatalogoPermisos.UsuariosCrear,
        CatalogoPermisos.UsuariosEditar, CatalogoPermisos.UsuariosDesactivar, CatalogoPermisos.UsuariosEliminar,
        CatalogoPermisos.RolesVer, CatalogoPermisos.RolesEditar,
    };

    /// <summary>
    /// Permisos que solo tienen sentido para el rol Cliente (la tienda web): no se pueden dar al personal.
    /// </summary>
    public static readonly IReadOnlySet<string> SoloCliente = new HashSet<string>
    {
        CatalogoPermisos.MisPedidosVer, CatalogoPermisos.MisPedidosCrear, CatalogoPermisos.MisPedidosCancelar,
        CatalogoPermisos.PerfilEditar,
    };

    /// <summary>
    /// Reglas de cada rol: <b>obligatorios</b> (no se le pueden quitar) y <b>prohibidos</b> (no se le pueden dar).
    /// Una sola fuente de verdad: la usan la validación de la API y la pantalla de roles (que bloquea esas casillas).
    /// </summary>
    /// <param name="rol">Nombre del rol.</param>
    public static (IReadOnlySet<string> Obligatorios, IReadOnlySet<string> Prohibidos) ParaRol(string rol) => rol switch
    {
        NombresRol.Administrador => (DelAdministrador, SoloCliente),
        NombresRol.Operador => (new HashSet<string> { CatalogoPermisos.AdminAcceso }, SoloCliente),
        NombresRol.Cliente => (new HashSet<string>(),
            CatalogoPermisos.Todos.Select(p => p.Codigo).Where(AbreLaAdministracion).ToHashSet()),
        _ => (new HashSet<string>(), new HashSet<string>()),
    };

    /// <summary>Permisos que el rol Cliente nunca puede tener: abren la administración.</summary>
    public static bool AbreLaAdministracion(string codigo) =>
        codigo is not (CatalogoPermisos.CatalogoVer or CatalogoPermisos.MisPedidosVer or CatalogoPermisos.MisPedidosCrear
            or CatalogoPermisos.MisPedidosCancelar or CatalogoPermisos.PerfilEditar);
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
