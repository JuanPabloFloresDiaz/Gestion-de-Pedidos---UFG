using System.Globalization;
using GestionPedidos.Application.Reportes;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Pedidos;
using Microsoft.EntityFrameworkCore;

namespace GestionPedidos.Infrastructure.Reportes;

/// <summary>Consultas de los reportes: todas de solo lectura (<c>AsNoTracking</c>) y con la agrupación hecha en SQL.</summary>
public sealed class ReportesRepository : IReportesRepository, IOpcionesDeReporte
{
    /// <summary>Máximo de productos que se listan en el reporte de productos (el catálogo puede crecer).</summary>
    public const int MaximoFilasProductos = 5000;

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-SV");
    private static readonly TimeSpan HoraElSalvador = TimeSpan.FromHours(-6);   // El Salvador no usa horario de verano (UTC−6)

    private readonly Persistence.GestionPedidosDbContext _db;

    /// <summary>Crea el repositorio.</summary>
    public ReportesRepository(Persistence.GestionPedidosDbContext db) => _db = db;

    private static ColumnaReporte T(string titulo) => new(titulo);
    private static ColumnaReporte N(string titulo) => new(titulo, true);
    private static IReadOnlyList<object?> F(params object?[] celdas) => celdas;

    // Una cifra clave ya formateada: enteros con separador de miles, importes con dos decimales.
    private static DatoReporte Dato(string etiqueta, object valor) => new(etiqueta, valor switch
    {
        decimal d => d.ToString("N2", Cultura),
        int i => i.ToString("N0", Cultura),
        long l => l.ToString("N0", Cultura),
        _ => valor.ToString() ?? "",
    });

    private static string FechaHora(DateTimeOffset f) => f.ToOffset(HoraElSalvador).ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);

    // «clientes.ver» → «Clientes»: el módulo es lo que va antes del punto.
    private static string Modulo(string codigo)
    {
        var parte = codigo.Split('.')[0];
        return parte.Length == 0 ? "" : char.ToUpper(parte[0], Cultura) + parte[1..];
    }

    private static string Plural(int n, string singular, string plural) => $"{Miles(n)} {(n == 1 ? singular : plural)}";

    // Entero con separador de miles, con la misma cultura (es-SV) que el resto del reporte.
    private static string Miles(int n) => n.ToString("N0", Cultura);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OpcionReporteDto>> UsuariosAsync(CancellationToken ct) =>
        await _db.Usuarios.AsNoTracking().OrderBy(u => u.NombreCompleto)
            .Select(u => new OpcionReporteDto(u.Id, u.NombreCompleto + " (" + u.NombreUsuario + ")")).ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OpcionReporteDto>> CategoriasAsync(CancellationToken ct) =>
        await _db.Categorias.AsNoTracking().OrderBy(c => c.Nombre)
            .Select(c => new OpcionReporteDto(c.Id, c.Activo ? c.Nombre : c.Nombre + " (inactiva)")).ToListAsync(ct);

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
                filas.Select(x => F(x.Departamento, x.Distrito, x.Total, x.Activos, x.Total - x.Activos)).ToList(), ahora)
            {
                Indicadores = IndicadoresDeClientes(filas.Sum(x => x.Total), filas.Sum(x => x.Activos), filas.Count, "Distritos"),
            };
        }
        var porDepartamento = await clientes.GroupBy(c => c.Ciudad!.Departamento!.Nombre)
            .Select(g => new { Departamento = g.Key, Total = g.Count(), Activos = g.Count(c => c.Activo) })
            .OrderBy(x => x.Departamento).ToListAsync(ct);
        return new TablaReporte("Clientes por departamento", "Cantidad de clientes registrados en cada departamento.",
            new[] { T("Departamento"), N("Clientes"), N("Activos"), N("Inactivos") },
            porDepartamento.Select(x => F(x.Departamento, x.Total, x.Activos, x.Total - x.Activos)).ToList(), ahora)
        {
            Indicadores = IndicadoresDeClientes(porDepartamento.Sum(x => x.Total), porDepartamento.Sum(x => x.Activos), porDepartamento.Count, "Departamentos"),
        };
    }

    private static IReadOnlyList<DatoReporte> IndicadoresDeClientes(int total, int activos, int zonas, string etiquetaZonas) =>
        new[] { Dato("Clientes", total), Dato("Activos", activos), Dato("Inactivos", total - activos), Dato(etiquetaZonas, zonas) };

    /// <inheritdoc/>
    public Task<TablaReporte?> ProductosPorCategoriaAsync(int? categoriaId, DateTimeOffset ahora, CancellationToken ct) =>
        categoriaId is { } id ? ProductosDeUnaCategoriaAsync(id, ahora, ct) : ProductosDeTodasLasCategoriasAsync(ahora, ct);

    private async Task<TablaReporte?> ProductosDeTodasLasCategoriasAsync(DateTimeOffset ahora, CancellationToken ct)
    {
        // Productos sin clasificar (SubcategoriaId nulo) se agrupan aparte para que el total cuadre con el inventario.
        var resumen = await _db.Productos.AsNoTracking()
            .GroupBy(p => new { Categoria = p.Subcategoria == null ? "Sin clasificar" : p.Subcategoria.Categoria!.Nombre,
                                Subcategoria = p.Subcategoria == null ? "—" : p.Subcategoria.Nombre })
            .Select(g => new
            {
                g.Key.Categoria, g.Key.Subcategoria, Productos = g.Count(), Activos = g.Count(p => p.Activo),
                Stock = g.Sum(p => p.CantidadStock), Valor = g.Sum(p => p.CantidadStock * p.PrecioVenta),
            })
            .OrderBy(x => x.Categoria).ThenBy(x => x.Subcategoria).ToListAsync(ct);

        // El listado: los productos de cada categoría (las «sin clasificar» al final), con tope para que el archivo no crezca sin límite.
        var productos = await _db.Productos.AsNoTracking()
            .OrderBy(p => p.Subcategoria == null ? 1 : 0).ThenBy(p => p.Subcategoria == null ? "" : p.Subcategoria.Categoria!.Nombre)
            .ThenBy(p => p.Subcategoria == null ? "" : p.Subcategoria.Nombre).ThenBy(p => p.Nombre)
            .Take(MaximoFilasProductos + 1)
            .Select(p => new
            {
                Categoria = p.Subcategoria == null ? "Sin clasificar" : p.Subcategoria.Categoria!.Nombre,
                Subcategoria = p.Subcategoria == null ? "—" : p.Subcategoria.Nombre,
                p.Codigo, p.Nombre, p.PrecioVenta, p.CantidadStock, p.Activo,
            }).ToListAsync(ct);
        var recortado = productos.Count > MaximoFilasProductos;
        if (recortado) productos.RemoveAt(productos.Count - 1);

        var secciones = productos.GroupBy(p => p.Categoria).Select(g =>
        {
            var existencias = g.Sum(p => p.CantidadStock);
            return new SeccionReporte(g.Key, $"{Plural(g.Count(), "producto", "productos")} · {Miles(existencias)} existencias",
                ColumnasDeProductos(),
                g.Select(p => F(p.Codigo, p.Nombre, p.Subcategoria, p.PrecioVenta, p.CantidadStock, p.Activo ? "Activo" : "De baja")).ToList());
        }).ToList();

        var totalProductos = resumen.Sum(x => x.Productos);
        return new TablaReporte("Productos por categoría",
            "Resumen de productos por categoría y subcategoría, con sus existencias y el valor del inventario a precio de venta; debajo, los productos de cada categoría.",
            new[] { T("Categoría"), T("Subcategoría"), N("Productos"), N("Activos"), N("Existencias"), N("Valor del inventario ($)") },
            resumen.Select(x => F(x.Categoria, x.Subcategoria, x.Productos, x.Activos, x.Stock, x.Valor)).ToList(), ahora,
            recortado ? $"Se listan los primeros {Miles(MaximoFilasProductos)} productos de {Miles(totalProductos)}; para ver el resto, genera el reporte de una categoría." : null)
        {
            Indicadores = new[]
            {
                Dato("Categorías", resumen.Where(x => x.Categoria != "Sin clasificar").Select(x => x.Categoria).Distinct().Count()),
                Dato("Productos", totalProductos), Dato("Activos", resumen.Sum(x => x.Activos)),
                Dato("Existencias", resumen.Sum(x => x.Stock)), Dato("Valor del inventario ($)", resumen.Sum(x => x.Valor)),
            },
            Secciones = secciones,
        };
    }

    private static IReadOnlyList<ColumnaReporte> ColumnasDeProductos() =>
        new[] { T("Código"), T("Producto"), T("Subcategoría"), N("Precio ($)"), N("Existencias"), T("Estado") };

    private async Task<TablaReporte?> ProductosDeUnaCategoriaAsync(int categoriaId, DateTimeOffset ahora, CancellationToken ct)
    {
        var categoria = await _db.Categorias.AsNoTracking().Where(c => c.Id == categoriaId)
            .Select(c => new { c.Codigo, c.Nombre, c.Descripcion, c.Activo }).FirstOrDefaultAsync(ct);
        if (categoria is null) return null;

        var subcategorias = await _db.Subcategorias.AsNoTracking().Where(s => s.CategoriaId == categoriaId)
            .OrderBy(s => s.Nombre).Select(s => new { s.Id, s.Nombre, s.Descripcion, s.Activo }).ToListAsync(ct);
        var filtros = await _db.Filtros.AsNoTracking().Where(f => f.Subcategoria!.CategoriaId == categoriaId)
            .OrderBy(f => f.Nombre).Select(f => new { f.SubcategoriaId, f.Nombre, f.Activo }).ToListAsync(ct);
        var opciones = await _db.OpcionesFiltro.AsNoTracking().Where(o => o.Filtro!.Subcategoria!.CategoriaId == categoriaId)
            .OrderBy(o => o.Valor).Select(o => new { o.FiltroId, FiltroNombre = o.Filtro!.Nombre, o.Filtro.SubcategoriaId, o.Valor, o.Activo }).ToListAsync(ct);
        var totales = await _db.Productos.AsNoTracking().Where(p => p.Subcategoria!.CategoriaId == categoriaId)
            .GroupBy(p => p.SubcategoriaId)
            .Select(g => new { SubcategoriaId = g.Key, Productos = g.Count(), Activos = g.Count(p => p.Activo),
                Stock = g.Sum(p => p.CantidadStock), Valor = g.Sum(p => p.CantidadStock * p.PrecioVenta) })
            .ToDictionaryAsync(x => x.SubcategoriaId!.Value, ct);
        var productos = await _db.Productos.AsNoTracking().Where(p => p.Subcategoria!.CategoriaId == categoriaId)
            .OrderBy(p => p.Subcategoria!.Nombre).ThenBy(p => p.Nombre).Take(MaximoFilasProductos + 1)
            .Select(p => new { Subcategoria = p.Subcategoria!.Nombre, p.Codigo, p.Nombre, p.PrecioVenta, p.CantidadStock, p.Activo }).ToListAsync(ct);
        var recortado = productos.Count > MaximoFilasProductos;
        if (recortado) productos.RemoveAt(productos.Count - 1);

        // Tabla principal: una fila por subcategoría (aunque no tenga productos), con sus totales.
        var filasSub = subcategorias.Select(s =>
        {
            totales.TryGetValue(s.Id, out var t);
            return F(s.Activo ? s.Nombre : s.Nombre + " (inactiva)", t?.Productos ?? 0, t?.Activos ?? 0, t?.Stock ?? 0, t?.Valor ?? 0m);
        }).ToList();

        // Sección de filtros: una fila por filtro con sus opciones; una subcategoría sin filtros lo dice.
        var filasFiltros = new List<IReadOnlyList<object?>>();
        foreach (var s in subcategorias)
        {
            var susFiltros = filtros.Where(f => f.SubcategoriaId == s.Id).ToList();
            if (susFiltros.Count == 0) { filasFiltros.Add(F(s.Nombre, "—", "Sin filtros")); continue; }
            foreach (var f in susFiltros)
            {
                var valores = opciones.Where(o => o.FiltroNombre == f.Nombre && o.SubcategoriaId == s.Id)
                    .Select(o => o.Activo ? o.Valor : o.Valor + " (inactiva)").ToList();
                filasFiltros.Add(F(s.Nombre, f.Activo ? f.Nombre : f.Nombre + " (inactivo)", valores.Count == 0 ? "Sin opciones" : string.Join(", ", valores)));
            }
        }

        var totalProductos = totales.Values.Sum(t => t.Productos);
        var secciones = new List<SeccionReporte>
        {
            new("Subcategorías y filtros", $"{Plural(subcategorias.Count, "subcategoría", "subcategorías")} · {Plural(filtros.Count, "filtro", "filtros")}",
                new[] { T("Subcategoría"), T("Filtro"), T("Opciones") }, filasFiltros, "Esta categoría no tiene subcategorías."),
            new("Productos de la categoría", Plural(totalProductos, "producto", "productos"), ColumnasDeProductos(),
                productos.Select(p => F(p.Codigo, p.Nombre, p.Subcategoria, p.PrecioVenta, p.CantidadStock, p.Activo ? "Activo" : "De baja")).ToList(),
                "Esta categoría no tiene productos."),
        };

        return new TablaReporte($"Categoría: {categoria.Nombre}", "Detalle de una categoría: sus subcategorías con totales, los filtros de cada una y los productos que contiene.",
            new[] { T("Subcategoría"), N("Productos"), N("Activos"), N("Existencias"), N("Valor del inventario ($)") }, filasSub, ahora,
            recortado ? $"Se listan los primeros {Miles(MaximoFilasProductos)} productos de {Miles(totalProductos)}." : null)
        {
            Ficha = new[]
            {
                new DatoReporte("Código", categoria.Codigo), new DatoReporte("Categoría", categoria.Nombre),
                new DatoReporte("Descripción", string.IsNullOrWhiteSpace(categoria.Descripcion) ? "—" : categoria.Descripcion!),
                new DatoReporte("Estado", categoria.Activo ? "Activa" : "Inactiva"),
            },
            Indicadores = new[]
            {
                Dato("Subcategorías", subcategorias.Count), Dato("Filtros", filtros.Count), Dato("Productos", totalProductos),
                Dato("Existencias", totales.Values.Sum(t => t.Stock)), Dato("Valor del inventario ($)", totales.Values.Sum(t => t.Valor)),
            },
            Secciones = secciones,
        };
    }

    /// <inheritdoc/>
    public async Task<TablaReporte> ProductosMasVendidosAsync(DateTimeOffset ahora, CancellationToken ct)
    {
        var filas = await _db.Pedidos.AsNoTracking()
            .Where(p => p.Activo && p.Estado != EstadoPedido.Cancelado)
            .SelectMany(p => p.Detalles)
            .GroupBy(d => new
            {
                d.ProductoId,
                Codigo = d.Producto!.Codigo,
                Nombre = d.Producto!.Nombre,
                Categoria = d.Producto!.Subcategoria == null ? "Sin clasificar" : d.Producto.Subcategoria.Categoria!.Nombre,
            })
            .Select(g => new
            {
                g.Key.Codigo,
                g.Key.Nombre,
                g.Key.Categoria,
                UnidadesVendidas = g.Sum(d => d.Cantidad),
                Pedidos = g.Select(d => d.PedidoId).Distinct().Count(),
                TotalVendido = g.Sum(d => d.Subtotal),
            })
            .OrderByDescending(x => x.UnidadesVendidas)
            .ThenByDescending(x => x.TotalVendido)
            .ThenBy(x => x.Nombre)
            .ToListAsync(ct);

        // Pedidos distintos que aportan ventas (un pedido con varios productos se cuenta una sola vez).
        var pedidosConVentas = await _db.Pedidos.AsNoTracking()
            .CountAsync(p => p.Activo && p.Estado != EstadoPedido.Cancelado && p.Detalles.Any(), ct);
        var unidades = filas.Sum(x => x.UnidadesVendidas);
        var facturado = filas.Sum(x => x.TotalVendido);

        // La participación de cada producto en las unidades vendidas, con dos decimales (la suma da 100 salvo por redondeo).
        decimal Participacion(int u) => unidades == 0 ? 0m : Math.Round(u * 100m / unidades, 2);

        // Segundo bloque: lo mismo agrupado por categoría, para ver de dónde vienen las ventas.
        var porCategoria = filas.GroupBy(x => x.Categoria)
            .Select(g => new { Categoria = g.Key, Productos = g.Count(), Unidades = g.Sum(x => x.UnidadesVendidas), Total = g.Sum(x => x.TotalVendido) })
            .OrderByDescending(x => x.Unidades).ThenBy(x => x.Categoria).ToList();

        return new TablaReporte(
            "Productos más vendidos",
            "Productos ordenados por unidades vendidas en pedidos activos (pendientes o entregados); los pedidos cancelados o dados de baja no cuentan.",
            new[] { N("N.º"), T("Código"), T("Producto"), T("Categoría"), N("Unidades vendidas"), N("Participación (%)"), N("Pedidos"), N("Total vendido ($)") },
            filas.Select((x, i) => F(i + 1, x.Codigo, x.Nombre, x.Categoria, x.UnidadesVendidas, Participacion(x.UnidadesVendidas), x.Pedidos, x.TotalVendido)).ToList(),
            ahora)
        {
            Indicadores = new[]
            {
                Dato("Productos vendidos", filas.Count), Dato("Unidades vendidas", unidades),
                Dato("Pedidos con ventas", pedidosConVentas), Dato("Total vendido ($)", facturado),
            },
            Secciones = new[]
            {
                new SeccionReporte("Ventas por categoría", $"{Plural(porCategoria.Count, "categoría", "categorías")} con ventas",
                    new[] { T("Categoría"), N("Productos"), N("Unidades vendidas"), N("Total vendido ($)") },
                    porCategoria.Select(x => F(x.Categoria, x.Productos, x.Unidades, x.Total)).ToList(), "Todavía no hay ventas."),
            },
        };
    }

    /// <inheritdoc/>
    public async Task<TablaReporte> UsuariosPorRolAsync(DateTimeOffset ahora, CancellationToken ct)
    {
        var filas = await _db.Usuarios.AsNoTracking()
            .OrderBy(u => u.Rol!.Nombre).ThenBy(u => u.NombreCompleto)
            .Select(u => new { Rol = u.Rol!.Nombre, u.NombreUsuario, u.NombreCompleto, u.Correo, u.Activo }).ToListAsync(ct);
        return new TablaReporte("Usuarios por rol", "Usuarios del sistema agrupados por rol, con su estado.",
            new[] { T("Rol"), T("Usuario"), T("Nombre completo"), T("Correo"), T("Estado") },
            filas.Select(x => F(x.Rol, x.NombreUsuario, x.NombreCompleto, x.Correo, x.Activo ? "Activo" : "De baja")).ToList(), ahora)
        {
            Indicadores = new[]
            {
                Dato("Usuarios", filas.Count), Dato("Activos", filas.Count(x => x.Activo)), Dato("De baja", filas.Count(x => !x.Activo)),
                Dato("Roles", filas.Select(x => x.Rol).Distinct().Count()),
            },
        };
    }

    /// <inheritdoc/>
    public async Task<TablaReporte> PermisosPorRolAsync(DateTimeOffset ahora, CancellationToken ct)
    {
        var roles = await _db.Roles.AsNoTracking().OrderBy(r => r.Nombre).Select(r => new { r.Id, r.Nombre, r.Descripcion }).ToListAsync(ct);
        var usuariosPorRol = await _db.Usuarios.AsNoTracking().GroupBy(u => u.RolId)
            .Select(g => new { RolId = g.Key, Total = g.Count() }).ToDictionaryAsync(x => x.RolId, x => x.Total, ct);
        var permisos = await _db.RolesPermisos.AsNoTracking()
            .Select(rp => new { rp.RolId, rp.Permiso!.Codigo, rp.Permiso.Descripcion }).ToListAsync(ct);
        var totalPermisos = await _db.Permisos.AsNoTracking().CountAsync(ct);

        // Un bloque por rol: arriba el rol (con cuántos permisos y usuarios tiene) y debajo la lista de sus permisos.
        var secciones = roles.Select(r =>
        {
            var suyos = permisos.Where(p => p.RolId == r.Id).OrderBy(p => p.Codigo, StringComparer.Ordinal).ToList();
            usuariosPorRol.TryGetValue(r.Id, out var usuarios);
            var subtitulo = $"{Plural(suyos.Count, "permiso", "permisos")} · {Plural(usuarios, "usuario", "usuarios")}";
            if (!string.IsNullOrWhiteSpace(r.Descripcion)) subtitulo = r.Descripcion + " · " + subtitulo;
            return new SeccionReporte(r.Nombre, subtitulo, new[] { T("Módulo"), T("Permiso"), T("Descripción") },
                suyos.Select(p => F(Modulo(p.Codigo), p.Codigo, p.Descripcion)).ToList(), "Este rol no tiene permisos asignados.");
        }).ToList();

        return new TablaReporte("Permisos por rol", "Cada rol del sistema con el listado de los permisos que tiene asignados.",
            Array.Empty<ColumnaReporte>(), Array.Empty<IReadOnlyList<object?>>(), ahora)
        {
            Indicadores = new[] { Dato("Roles", roles.Count), Dato("Permisos en el catálogo", totalPermisos), Dato("Asignaciones", permisos.Count) },
            Secciones = secciones,
        };
    }

    /// <inheritdoc/>
    public async Task<TablaReporte?> ActividadesPorUsuarioAsync(int? usuarioId, DateTimeOffset? desde, DateTimeOffset? hasta, int maximoFilas,
        DateTimeOffset ahora, CancellationToken ct)
    {
        var periodo = (desde, hasta) switch
        {
            ({ } d, { } h) => $"{d.ToOffset(HoraElSalvador):dd/MM/yyyy} al {h.AddDays(-1).ToOffset(HoraElSalvador):dd/MM/yyyy}",
            ({ } d, null) => $"desde el {d.ToOffset(HoraElSalvador):dd/MM/yyyy}",
            (null, { } h) => $"hasta el {h.AddDays(-1).ToOffset(HoraElSalvador):dd/MM/yyyy}",
            _ => "todo el historial",
        };

        var consulta = _db.Actividades.AsNoTracking().AsQueryable();
        if (desde is { } d1) consulta = consulta.Where(a => a.Fecha >= d1);
        if (hasta is { } h1) consulta = consulta.Where(a => a.Fecha < h1);

        if (usuarioId is { } uid)
        {
            var usuario = await _db.Usuarios.AsNoTracking().Where(u => u.Id == uid)
                .Select(u => new { u.NombreUsuario, u.NombreCompleto, u.Correo, Rol = u.Rol!.Nombre, u.Activo }).FirstOrDefaultAsync(ct);
            if (usuario is null) return null;

            var suyas = consulta.Where(a => a.UsuarioId == uid);
            var total = await suyas.CountAsync(ct);
            var porAccion = await suyas.GroupBy(a => a.Accion).Select(g => new { Accion = g.Key, Total = g.Count() })
                .OrderByDescending(x => x.Total).ThenBy(x => x.Accion).ToListAsync(ct);
            var filas = await suyas.OrderByDescending(a => a.Fecha).Take(maximoFilas)
                .Select(a => new { a.Fecha, a.Accion, a.Entidad, a.EntidadId, a.Detalle }).ToListAsync(ct);
            DateTimeOffset? primera = total == 0 ? null : await suyas.MinAsync(a => (DateTimeOffset?)a.Fecha, ct);
            DateTimeOffset? ultima = total == 0 ? null : await suyas.MaxAsync(a => (DateTimeOffset?)a.Fecha, ct);

            var nota = total > maximoFilas ? $"Se muestran las {Miles(maximoFilas)} actividades más recientes de {Miles(total)}; acota el reporte con un rango de fechas." : null;
            return new TablaReporte($"Actividades de {usuario.NombreCompleto}", $"Registro de actividades del usuario «{usuario.NombreUsuario}» ({periodo}), con un resumen por acción y el detalle de cada una.",
                new[] { T("Acción"), N("Actividades") }, porAccion.Select(x => F(x.Accion, x.Total)).ToList(), ahora, nota)
            {
                Ficha = new[]
                {
                    new DatoReporte("Usuario", usuario.NombreUsuario), new DatoReporte("Nombre", usuario.NombreCompleto),
                    new DatoReporte("Correo", usuario.Correo), new DatoReporte("Rol", usuario.Rol),
                    new DatoReporte("Estado", usuario.Activo ? "Activo" : "De baja"), new DatoReporte("Período", periodo),
                },
                Indicadores = new[]
                {
                    Dato("Actividades", total), Dato("Tipos de acción", porAccion.Count),
                    new DatoReporte("Primera", primera is null ? "—" : primera.Value.ToOffset(HoraElSalvador).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
                    new DatoReporte("Última", ultima is null ? "—" : ultima.Value.ToOffset(HoraElSalvador).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
                },
                Secciones = new[]
                {
                    new SeccionReporte("Detalle de actividades", "De la más reciente a la más antigua", new[] { T("Fecha (El Salvador)"), T("Acción"), T("Entidad"), T("Detalle") },
                        filas.Select(x => F(FechaHora(x.Fecha), x.Accion, x.EntidadId is null ? x.Entidad : $"{x.Entidad} #{x.EntidadId}", x.Detalle)).ToList(),
                        "Este usuario no tiene actividades en ese período."),
                },
            };
        }

        // Todos los usuarios: un resumen por usuario y, debajo, un bloque con las actividades de cada uno.
        var totalGeneral = await consulta.CountAsync(ct);
        var resumen = await consulta.GroupBy(a => new { Usuario = a.Usuario == null ? "(sin usuario)" : a.Usuario.NombreUsuario,
                                                       Rol = a.Usuario == null ? "—" : a.Usuario.Rol!.Nombre })
            .Select(g => new { g.Key.Usuario, g.Key.Rol, Total = g.Count() }).OrderBy(x => x.Usuario).ToListAsync(ct);
        var detalle = await consulta.OrderBy(a => a.Usuario!.NombreUsuario).ThenByDescending(a => a.Fecha).Take(maximoFilas)
            .Select(a => new { Usuario = a.Usuario == null ? "(sin usuario)" : a.Usuario.NombreUsuario,
                               Nombre = a.Usuario == null ? "" : a.Usuario.NombreCompleto, a.Fecha, a.Accion, a.Entidad, a.EntidadId, a.Detalle }).ToListAsync(ct);
        var notaTodos = totalGeneral > maximoFilas ? $"Se muestran las primeras {Miles(maximoFilas)} de {Miles(totalGeneral)} actividades; elige un usuario o un rango de fechas para acotar el reporte." : null;
        var bloques = detalle.GroupBy(x => x.Usuario).Select(g =>
            new SeccionReporte(string.IsNullOrWhiteSpace(g.First().Nombre) ? g.Key : $"{g.First().Nombre} ({g.Key})", Plural(g.Count(), "actividad", "actividades"),
                new[] { T("Fecha (El Salvador)"), T("Acción"), T("Entidad"), T("Detalle") },
                g.Select(x => F(FechaHora(x.Fecha), x.Accion, x.EntidadId is null ? x.Entidad : $"{x.Entidad} #{x.EntidadId}", x.Detalle)).ToList())).ToList();
        return new TablaReporte("Actividades por usuario", $"Acciones registradas en el historial ({periodo}), con un resumen por usuario y el detalle de cada uno (la más reciente primero).",
            new[] { T("Usuario"), T("Rol"), N("Actividades") }, resumen.Select(x => F(x.Usuario, x.Rol, x.Total)).ToList(), ahora, notaTodos)
        {
            Indicadores = new[] { Dato("Usuarios con actividad", resumen.Count), Dato("Actividades", totalGeneral) },
            Secciones = bloques,
        };
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
            ahora)
        {
            Indicadores = new[] { Dato("Pedidos", grupos.Sum(g => g.Total)), Dato("Activos", grupos.Sum(g => g.Activos)), Dato("Monto total ($)", grupos.Sum(g => g.Monto)) },
        };
    }
}