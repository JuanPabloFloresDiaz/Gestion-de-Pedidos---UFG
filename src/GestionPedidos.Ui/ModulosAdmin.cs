using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun.Documentos;
using Blazicons;
using GestionPedidos.Ui.Modulos.Ayuda;
using GestionPedidos.Ui.Modulos.Catalogos;
using GestionPedidos.Ui.Modulos.Categorias;
using GestionPedidos.Ui.Modulos.Clientes;
using GestionPedidos.Ui.Modulos.Historial;
using GestionPedidos.Ui.Modulos.Pedidos;
using GestionPedidos.Ui.Modulos.Productos;
using GestionPedidos.Ui.Modulos.Roles;
using GestionPedidos.Ui.Modulos.Usuarios;

namespace GestionPedidos.Ui;

/// <summary>Entrada de menú de un módulo de administración.</summary>
/// <param name="Grupo">Menú al que pertenece (<c>Gestión</c>, <c>Administración</c>, <c>Ayuda</c>).</param>
/// <param name="Documento">Documento (pestaña) que abre.</param>
/// <param name="Permiso">Permiso requerido para verlo; <c>null</c> = todos.</param>
/// <param name="Icono">Ícono (Lucide) de la tarjeta y del menú.</param>
/// <param name="Descripcion">Una línea que explica para qué sirve (se ve en la tarjeta de la bienvenida y del lanzador).</param>
public sealed record ModuloAdmin(string Grupo, Documento Documento, string? Permiso, SvgIcon Icono, string Descripcion);

/// <summary>
/// Registro de los módulos de administración. Cada módulo nuevo añade UNA línea aquí (y su página/ViewModel/servicio);
/// así los integrantes no se pisan en el shell ([Implementacion.md §4]).
/// </summary>
public static class ModulosAdmin
{
    /// <summary>Módulos en el orden en que aparecen en el menú.</summary>
    public static readonly IReadOnlyList<ModuloAdmin> Todos = new[]
    {
        // Gestión: el trabajo diario. Clientes, Productos y Pedidos están en progreso (los arman los integrantes, ver Tareas.md).
        new ModuloAdmin("Gestión", new Documento("clientes", "Clientes", typeof(ClientesPagina)), Permisos.ClientesVer, Lucide.Users,
            "Contacto, dirección y cuenta web de cada cliente."),
        new ModuloAdmin("Gestión", new Documento("productos", "Productos", typeof(ProductosPagina)), Permisos.ProductosVer, Lucide.Package,
            "Inventario, precios, existencias y fotos."),
        new ModuloAdmin("Gestión", new Documento("categorias", "Categorías y filtros", typeof(CategoriasPagina)), Permisos.CategoriasVer, Lucide.Tags,
            "Cómo se clasifican y describen los productos."),
        new ModuloAdmin("Gestión", new Documento("pedidos", "Pedidos", typeof(PedidosPagina)), Permisos.PedidosVer, Lucide.ClipboardList,
            "Pedidos con sus líneas, estados y búsquedas."),
        // Administración y ayuda
        new ModuloAdmin("Administración", new Documento("usuarios", "Usuarios", typeof(UsuariosPagina)), Permisos.UsuariosVer, Lucide.UserCog,
            "Personal con acceso, su rol y su estado."),
        new ModuloAdmin("Administración", new Documento("roles", "Roles y permisos", typeof(RolesPagina)), Permisos.RolesVer, Lucide.ShieldCheck,
            "Qué puede hacer cada rol."),
        new ModuloAdmin("Administración", new Documento("historial", "Historial de actividades", typeof(HistorialPagina)), Permisos.HistorialVer, Lucide.ScrollText,
            "Quién hizo qué y cuándo."),
        new ModuloAdmin("Administración", new Documento("catalogos", "Ciudades y departamentos", typeof(CatalogosPagina)), Permisos.CatalogosVer, Lucide.MapPin,
            "Departamentos y distritos de El Salvador."),
        new ModuloAdmin("Ayuda", new Documento("integrantes", "Integrantes del equipo", typeof(IntegrantesPagina)), null, Lucide.UsersRound,
            "Quiénes hicieron este proyecto."),
    };
}
