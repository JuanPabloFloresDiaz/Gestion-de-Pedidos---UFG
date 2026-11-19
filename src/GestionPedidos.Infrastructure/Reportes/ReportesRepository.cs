using GestionPedidos.Application.Reportes;
using Microsoft.EntityFrameworkCore;

namespace GestionPedidos.Infrastructure.Reportes;

/// <summary>Consultas de los reportes: todas de solo lectura (<c>AsNoTracking</c>) y con la agrupación hecha en SQL.</summary>
public sealed class ReportesRepository : IReportesRepository
{
    private readonly Persistence.GestionPedidosDbContext _db;

    /// <summary>Crea el repositorio.</summary>
    public ReportesRepository(Persistence.GestionPedidosDbContext db) => _db = db;

    private static ColumnaReporte T(string titulo) => new(titulo);
    private static ColumnaReporte N(string titulo) => new(titulo, true);
    private static IReadOnlyList<object?> F(params object?[] celdas) => celdas;

    /// <inheritdoc/>
    public async Task<TablaReporte> ClientesPorZonaAsync(bool porDistrito, DateTimeOffset ahora, CancellationToken ct)
    {
        var clientes = _db.Clientes.AsNoTracking();
        if (porDistrito)
        {
            var filas = await clientes.GroupBy(c => new { Departamento = c.Ciudad!.Departamento!.Nombre, Distrito = c.Ciudad!.Nombre })
                .Select(g => new { g.Key.Departamento, g.Key.Distrito, Total = g.Count(), Activos = g.Count(c => c.Activo) })
                .OrderBy(x => x.Departamento).ThenBy(x => x.Distrito).ToListAsync(ct);
            return new TablaReporte("Clientes por distrito", "Cantidad de clientes registrados en cada distrito, agrupados por departamento.",
                new[] { T("Departamento"), T("Distrito"), N("Clientes"), N("Activos"), N("Inactivos") },
                filas.Select(x => F(x.Departamento, x.Distrito, x.Total, x.Activos, x.Total - x.Activos)).ToList(), ahora);
        }
        var porDepartamento = await clientes.GroupBy(c => c.Ciudad!.Departamento!.Nombre)
            .Select(g => new { Departamento = g.Key, Total = g.Count(), Activos = g.Count(c => c.Activo) })
            .OrderBy(x => x.Departamento).ToListAsync(ct);
        return new TablaReporte("Clientes por departamento", "Cantidad de clientes registrados en cada departamento.",
            new[] { T("Departamento"), N("Clientes"), N("Activos"), N("Inactivos") },
            porDepartamento.Select(x => F(x.Departamento, x.Total, x.Activos, x.Total - x.Activos)).ToList(), ahora);
    }

    /// <inheritdoc/>
    public async Task<TablaReporte> ProductosPorCategoriaAsync(DateTimeOffset ahora, CancellationToken ct)
    {
        // Productos sin clasificar (SubcategoriaId nulo) se agrupan aparte para que el total cuadre con el inventario.
        var filas = await _db.Productos.AsNoTracking()
            .GroupBy(p => new { Categoria = p.Subcategoria == null ? "Sin clasificar" : p.Subcategoria.Categoria!.Nombre,
                                Subcategoria = p.Subcategoria == null ? "—" : p.Subcategoria.Nombre })
            .Select(g => new
            {
                g.Key.Categoria, g.Key.Subcategoria, Productos = g.Count(), Activos = g.Count(p => p.Activo),
                Stock = g.Sum(p => p.CantidadStock), Valor = g.Sum(p => p.CantidadStock * p.PrecioVenta),
            })
            .OrderBy(x => x.Categoria).ThenBy(x => x.Subcategoria).ToListAsync(ct);
        return new TablaReporte("Productos por categoría", "Productos de cada categoría y subcategoría, con sus existencias y el valor del inventario a precio de venta.",
            new[] { T("Categoría"), T("Subcategoría"), N("Productos"), N("Activos"), N("Existencias"), N("Valor del inventario ($)") },
            filas.Select(x => F(x.Categoria, x.Subcategoria, x.Productos, x.Activos, x.Stock, x.Valor)).ToList(), ahora);
    }

    /// <inheritdoc/>
    public async Task<TablaReporte> UsuariosPorRolAsync(DateTimeOffset ahora, CancellationToken ct)
    {
        var filas = await _db.Usuarios.AsNoTracking()
            .OrderBy(u => u.Rol!.Nombre).ThenBy(u => u.NombreCompleto)
            .Select(u => new { Rol = u.Rol!.Nombre, u.NombreUsuario, u.NombreCompleto, u.Correo, u.Activo }).ToListAsync(ct);
        return new TablaReporte("Usuarios por rol", "Usuarios del sistema agrupados por rol, con su estado.",
            new[] { T("Rol"), T("Usuario"), T("Nombre completo"), T("Correo"), T("Estado") },
            filas.Select(x => F(x.Rol, x.NombreUsuario, x.NombreCompleto, x.Correo, x.Activo ? "Activo" : "De baja")).ToList(), ahora);
    }

    /// <inheritdoc/>
    public async Task<TablaReporte> PermisosPorRolAsync(DateTimeOffset ahora, CancellationToken ct)
    {
        var filas = await _db.RolesPermisos.AsNoTracking()
            .OrderBy(rp => rp.Rol!.Nombre).ThenBy(rp => rp.PermisoId)
            .Select(rp => new { Rol = rp.Rol!.Nombre, rp.Permiso!.Codigo, rp.Permiso.Descripcion }).ToListAsync(ct);
        return new TablaReporte("Permisos por rol", "Permisos que tiene asignados cada rol.",
            new[] { T("Rol"), T("Permiso"), T("Descripción") },
            filas.Select(x => F(x.Rol, x.Codigo, x.Descripcion)).ToList(), ahora);
    }

    /// <inheritdoc/>
    public async Task<TablaReporte> ActividadesPorUsuarioAsync(int? usuarioId, DateTimeOffset? desde, DateTimeOffset? hasta, int maximoFilas,
        DateTimeOffset ahora, CancellationToken ct)
    {
        var consulta = _db.Actividades.AsNoTracking().AsQueryable();
        if (usuarioId is { } u) consulta = consulta.Where(a => a.UsuarioId == u);
        if (desde is { } d) consulta = consulta.Where(a => a.Fecha >= d);
        if (hasta is { } h) consulta = consulta.Where(a => a.Fecha < h);

        var total = await consulta.CountAsync(ct);
        var filas = await consulta.OrderBy(a => a.Usuario!.NombreUsuario).ThenByDescending(a => a.Fecha).Take(maximoFilas)
            .Select(a => new { Usuario = a.Usuario == null ? "(sin usuario)" : a.Usuario.NombreUsuario, a.Fecha, a.Accion, a.Entidad, a.EntidadId, a.Detalle })
            .ToListAsync(ct);
        var nota = total > maximoFilas ? $"Se muestran las primeras {maximoFilas:N0} de {total:N0} actividades; acota el reporte con un usuario o un rango de fechas." : null;
        return new TablaReporte("Actividades por usuario", "Acciones registradas en el historial, agrupadas por usuario (la más reciente primero).",
            new[] { T("Usuario"), T("Fecha (El Salvador)"), T("Acción"), T("Entidad"), T("Detalle") },
            filas.Select(x => F(x.Usuario, x.Fecha.ToOffset(TimeSpan.FromHours(-6)).ToString("dd/MM/yyyy HH:mm:ss"), x.Accion,
                x.EntidadId is null ? x.Entidad : $"{x.Entidad} #{x.EntidadId}", x.Detalle)).ToList(), ahora, nota);
    }

    /// <inheritdoc/>
    public async Task<TablaReporte> PedidosPorEstadoAsync(DateTimeOffset? desde, DateTimeOffset? hasta, DateTimeOffset ahora, CancellationToken ct)
    {
        var consulta = _db.Pedidos.AsNoTracking().AsQueryable();
        if (desde is { } d) consulta = consulta.Where(p => p.FechaPedido >= d);
        if (hasta is { } h) consulta = consulta.Where(p => p.FechaPedido < h);

        var grupos = await consulta
            .GroupBy(p => p.Estado)
            .Select(g => new
            {
                Estado = g.Key,
                Total = g.Count(),
                Activos = g.Count(p => p.Activo),
                Inactivos = g.Count(p => !p.Activo),
                Monto = g.Sum(p => p.Total)
            })
            .ToListAsync(ct);

        var mapa = grupos.ToDictionary(g => g.Estado);
        var estadosCanonicos = new[] { Domain.Pedidos.EstadoPedido.Pendiente, Domain.Pedidos.EstadoPedido.Entregado, Domain.Pedidos.EstadoPedido.Cancelado };

        var filas = new List<IReadOnlyList<object?>>();
        foreach (var est in estadosCanonicos)
        {
            if (mapa.TryGetValue(est, out var info))
                filas.Add(F(est, info.Total, info.Activos, info.Inactivos, info.Monto));
            else
                filas.Add(F(est, 0, 0, 0, 0.00m));
        }

        foreach (var g in grupos.Where(g => !estadosCanonicos.Contains(g.Estado)).OrderBy(g => g.Estado))
            filas.Add(F(g.Estado, g.Total, g.Activos, g.Inactivos, g.Monto));

        return new TablaReporte(
            "Pedidos por estado",
            "Cantidad de pedidos registrados según su estado (Pendiente, Entregado, Cancelado), con detalle de activos e inactivos y monto total.",
            new[] { T("Estado"), N("Pedidos"), N("Activos"), N("Inactivos"), N("Monto Total ($)") },
            filas,
            ahora);
    }
}
