using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun.Documentos;
using GestionPedidos.Ui.Modulos.Ayuda;
using GestionPedidos.Ui.Modulos.Catalogos;
using GestionPedidos.Ui.Modulos.Categorias;
using GestionPedidos.Ui.Modulos.Historial;
using GestionPedidos.Ui.Modulos.Roles;
using GestionPedidos.Ui.Modulos.Usuarios;

namespace GestionPedidos.Ui;

/// <summary>Entrada de menú de un módulo de administración.</summary>
/// <param name="Grupo">Menú al que pertenece (<c>Gestión</c>, <c>Administración</c>, <c>Ayuda</c>).</param>
/// <param name="Documento">Documento (pestaña) que abre.</param>
/// <param name="Permiso">Permiso requerido para verlo; <c>null</c> = todos.</param>
public sealed record ModuloAdmin(string Grupo, Documento Documento, string? Permiso);

/// <summary>
/// Registro de los módulos de administración. Cada módulo nuevo añade UNA línea aquí (y su página/ViewModel/servicio);
/// así los integrantes no se pisan en el shell ([Implementacion.md §4]).
/// </summary>
public static class ModulosAdmin
{
    /// <summary>Módulos en el orden en que aparecen en el menú.</summary>
    public static readonly IReadOnlyList<ModuloAdmin> Todos = new[]
    {
        // Fase 2: Clientes y Productos · Fase 3: Pedidos (añadir aquí sus líneas, grupo "Gestión").
        new ModuloAdmin("Gestión", new Documento("categorias", "Categorías y filtros", typeof(CategoriasPagina)), Permisos.CategoriasVer),
        new ModuloAdmin("Administración", new Documento("usuarios", "Usuarios", typeof(UsuariosPagina)), Permisos.UsuariosVer),
        new ModuloAdmin("Administración", new Documento("roles", "Roles y permisos", typeof(RolesPagina)), Permisos.RolesVer),
        new ModuloAdmin("Administración", new Documento("historial", "Historial de actividades", typeof(HistorialPagina)), Permisos.HistorialVer),
        new ModuloAdmin("Administración", new Documento("catalogos", "Ciudades y departamentos", typeof(CatalogosPagina)), Permisos.CatalogosVer),
        new ModuloAdmin("Ayuda", new Documento("integrantes", "Integrantes del equipo", typeof(IntegrantesPagina)), null),
    };
}
