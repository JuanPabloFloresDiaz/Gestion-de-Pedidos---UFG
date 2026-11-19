using System.Text;
using ClosedXML.Excel;
using GestionPedidos.Application.Reportes;
using GestionPedidos.Domain.Seguridad;
using GestionPedidos.Infrastructure.Persistence.Seed;
using GestionPedidos.Infrastructure.Reportes;
using GestionPedidos.Infrastructure.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GestionPedidos.Infrastructure.Tests;

/// <summary>Exportadores de reportes: PDF, Excel y CSV (sin base de datos).</summary>
public class ExportadorDeReportesTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 18, 3, 30, 0, TimeSpan.Zero);
    private static readonly ExportadorDeReportes Exportador = new();

    private static TablaReporte Tabla(params object?[][] filas) => new("Reporte de prueba", "Descripción de prueba",
        new[] { new ColumnaReporte("Nombre"), new ColumnaReporte("Cantidad", true), new ColumnaReporte("Valor ($)", true) },
        filas.Select(f => (IReadOnlyList<object?>)f).ToList(), Ahora);

    // Primera fila de la hoja cuya primera celda dice exactamente ese texto.
    private static int FilaConTexto(IXLWorksheet hoja, string texto)
    {
        for (var f = 1; f <= 80; f++) if (hoja.Cell(f, 1).GetString() == texto) return f;
        throw new InvalidOperationException($"No hay una fila que empiece por «{texto}».");
    }

    // Un reporte con todo: ficha, indicadores, tabla principal y dos bloques (uno vacío).
    private static TablaReporte Completo() => new("Reporte completo", "Con ficha, indicadores y bloques",
        new[] { new ColumnaReporte("Acción"), new ColumnaReporte("Actividades", true) },
        new IReadOnlyList<object?>[] { new object?[] { "Login", 3 }, new object?[] { "=Cmd", 1 } }, Ahora, "Nota al pie")
    {
        Ficha = new[] { new DatoReporte("Usuario", "ana"), new DatoReporte("Rol", "Administrador"), new DatoReporte("Estado", "Activo") },
        Indicadores = new[] { new DatoReporte("Actividades", "4"), new DatoReporte("Tipos", "2"), new DatoReporte("Primera", "01/11/2026"),
            new DatoReporte("Última", "10/11/2026"), new DatoReporte("Quinto", "5") },
        Secciones = new[]
        {
            new SeccionReporte("Bloque con datos", "2 elementos", new[] { new ColumnaReporte("Código"), new ColumnaReporte("Detalle"), new ColumnaReporte("Cantidad", true) },
                new IReadOnlyList<object?>[] { new object?[] { "A-1", "Texto largo ñandú", 10 }, new object?[] { "=B-2", "Otro", 20 } }),
            new SeccionReporte("Bloque vacío", null, new[] { new ColumnaReporte("X") }, Array.Empty<IReadOnlyList<object?>>(), "Nada que mostrar aquí."),
        },
    };

    [Fact]
    public void Csv_LlevaBomUtf8_EncabezadoYNumerosConPuntoDecimal()
    {
        var bytes = Exportador.Exportar(Tabla(new object?[] { "Ñandú, el \"grande\"", 1234, 1234.5m }), FormatoReporte.Csv).Contenido;

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
        var lineas = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Nombre,Cantidad,Valor ($)", lineas[0]);
        Assert.Equal("\"Ñandú, el \"\"grande\"\"\",1234,1234.50", lineas[1]);   // comillas dobladas, sin separador de miles
    }

    [Theory]
    [InlineData("=SUMA(A1:A9)")]
    [InlineData("+cmd|' /C calc'!A0")]
    [InlineData("-2+3")]
    [InlineData("@SUM(1)")]
    public void Csv_NeutralizaLaInyeccionDeFormulas_EnLasColumnasDeTexto(string peligroso)
    {
        var bytes = Exportador.Exportar(Tabla(new object?[] { peligroso, 1, 1m }), FormatoReporte.Csv).Contenido;
        var linea = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3).Split("\r\n", StringSplitOptions.RemoveEmptyEntries)[1];
        Assert.StartsWith("'" , linea.TrimStart('"'));   // el apóstrofo evita que Excel lo trate como fórmula
    }

    [Fact]
    public void Excel_GuardaLosNumerosComoNumero_YElTextoComoTexto_AunqueEmpieceConIgual()
    {
        var archivo = Exportador.Exportar(Tabla(new object?[] { "=1+1", 7, 19.99m }), FormatoReporte.Excel);

        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", archivo.TipoContenido);
        using var libro = new XLWorkbook(new MemoryStream(archivo.Contenido));
        var hoja = libro.Worksheet(1);
        Assert.Equal("Reporte de prueba", hoja.Cell(4, 1).GetString());   // la banda del título va bajo el logo
        var encabezado = FilaConTexto(hoja, "Nombre");
        Assert.Equal("=1+1", hoja.Cell(encabezado + 1, 1).GetString());
        Assert.False(hoja.Cell(encabezado + 1, 1).HasFormula);   // es texto, nunca una fórmula
        Assert.Equal(XLDataType.Number, hoja.Cell(encabezado + 1, 2).DataType);
        Assert.Equal(7, hoja.Cell(encabezado + 1, 2).GetDouble());
        Assert.Equal(19.99, hoja.Cell(encabezado + 1, 3).GetDouble(), 2);
    }

    [Fact]
    public void Excel_ElNombreDeHojaRespetaLosLimitesDeExcel()
    {
        var tabla = Tabla(new object?[] { "a", 1, 1m }) with { Titulo = "Un título muy largo [con] símbolos: prohibidos? y más de treinta y un caracteres" };
        using var libro = new XLWorkbook(new MemoryStream(Exportador.Exportar(tabla, FormatoReporte.Excel).Contenido));
        var nombre = libro.Worksheet(1).Name;
        Assert.True(nombre.Length <= 31);
        Assert.DoesNotContain(nombre, c => "[]:*?/\\".Contains(c));
    }

    [Fact]
    public void Pdf_EsUnPdfValido_AunSinFilas_YConMuchasFilas()
    {
        var vacio = Exportador.Exportar(Tabla(), FormatoReporte.Pdf);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(vacio.Contenido, 0, 5));
        Assert.Equal("application/pdf", vacio.TipoContenido);

        var filas = Enumerable.Range(1, 400).Select(i => new object?[] { $"Producto {i} con acentos áéíóú ñ", i, i * 1.25m }).ToArray();
        var grande = Exportador.Exportar(Tabla(filas), FormatoReporte.Pdf);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(grande.Contenido, 0, 5));
        Assert.True(grande.Contenido.Length > vacio.Contenido.Length);   // varias páginas
    }

    [Fact]
    public void Csv_ConBloques_SaleComoDocumento_ConTituloFichaIndicadoresYCadaBloque()
    {
        var bytes = Exportador.Exportar(Completo(), FormatoReporte.Csv).Contenido;
        var texto = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        var lineas = texto.Split("\r\n");

        Assert.Equal("Reporte completo", lineas[0]);
        Assert.Contains("Usuario,ana", lineas);
        Assert.Contains("Quinto,5", lineas);                       // los indicadores también van
        Assert.Contains("Acción,Actividades", lineas);             // tabla principal
        Assert.Contains("'=Cmd,1", lineas);                        // fórmula neutralizada
        Assert.Contains("Bloque con datos", lineas);
        Assert.Contains("Código,Detalle,Cantidad", lineas);
        Assert.Contains("'=B-2,Otro,20", lineas);
        Assert.Contains("Nada que mostrar aquí.", lineas);         // el bloque vacío dice por qué
        Assert.Contains("Nota al pie", lineas);
    }

    [Fact]
    public void Csv_SinBloques_NoLlevaFichaNiIndicadores_SoloLaTabla()
    {
        var tabla = Tabla(new object?[] { "a", 1, 1m }) with { Indicadores = new[] { new DatoReporte("Total", "1") } };
        var bytes = Exportador.Exportar(tabla, FormatoReporte.Csv).Contenido;
        var primera = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3).Split("\r\n")[0];
        Assert.Equal("Nombre,Cantidad,Valor ($)", primera);   // listo para importar, sin textos antes del encabezado
    }

    [Fact]
    public void Excel_LlevaElLogo_ElColorDeAcento_LaFichaLosIndicadoresYLosBloques()
    {
        var archivo = Exportador.Exportar(Completo(), FormatoReporte.Excel, "verde");
        using var libro = new XLWorkbook(new MemoryStream(archivo.Contenido));
        var hoja = libro.Worksheet(1);

        Assert.Single(hoja.Pictures);   // el logo
        var banda = hoja.Cell(4, 1).Style.Fill.BackgroundColor.Color;
        Assert.Equal("#15803D", $"#{banda.R:X2}{banda.G:X2}{banda.B:X2}");   // banda del título con el verde elegido
        Assert.Equal("ana", hoja.Cell(FilaConTexto(hoja, "Usuario"), 2).GetString());
        Assert.Equal("5", hoja.Cell(FilaConTexto(hoja, "Quinto"), 2).GetString());

        var bloque = FilaConTexto(hoja, "Bloque con datos");
        Assert.Equal("2 elementos", hoja.Cell(bloque + 1, 1).GetString());
        Assert.Equal("Código", hoja.Cell(bloque + 2, 1).GetString());
        Assert.Equal("=B-2", hoja.Cell(bloque + 4, 1).GetString());
        Assert.False(hoja.Cell(bloque + 4, 1).HasFormula);
        Assert.Equal(20, hoja.Cell(bloque + 4, 3).GetDouble());
        Assert.NotEqual(0, FilaConTexto(hoja, "Nada que mostrar aquí."));
    }

    [Theory]
    [InlineData("azul", "#2F5BEA")]
    [InlineData("rosa", "#BE185D")]
    [InlineData(null, "#2F5BEA")]
    public void Excel_ElEncabezadoDeLaTablaUsaElColorDeAcento(string? acento, string esperado)
    {
        using var libro = new XLWorkbook(new MemoryStream(Exportador.Exportar(Tabla(new object?[] { "a", 1, 1m }), FormatoReporte.Excel, acento).Contenido));
        var hoja = libro.Worksheet(1);
        var color = hoja.Cell(FilaConTexto(hoja, "Nombre"), 1).Style.Fill.BackgroundColor.Color;
        Assert.Equal(esperado, $"#{color.R:X2}{color.G:X2}{color.B:X2}");
    }

    [Theory]
    [InlineData("azul")]
    [InlineData("ambar")]
    [InlineData("grafito")]
    public void Pdf_ConFichaIndicadoresYBloques_EsValido_EnCadaColor(string acento)
    {
        var archivo = Exportador.Exportar(Completo(), FormatoReporte.Pdf, acento);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(archivo.Contenido, 0, 5));
        Assert.True(archivo.Contenido.Length > 5_000);   // lleva el logo y las tablas
    }

    [Fact]
    public void Pdf_ConUnReporteSoloDeBloques_YSinTablaPrincipal_NoFalla()
    {
        var soloBloques = Completo() with { Columnas = Array.Empty<ColumnaReporte>(), Filas = Array.Empty<IReadOnlyList<object?>>() };
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(Exportador.Exportar(soloBloques, FormatoReporte.Pdf).Contenido, 0, 5));
        var excel = Exportador.Exportar(soloBloques, FormatoReporte.Excel);
        using var libro = new XLWorkbook(new MemoryStream(excel.Contenido));
        Assert.NotEqual(0, FilaConTexto(libro.Worksheet(1), "Bloque con datos"));
    }
}

/// <summary>Los reportes contra una base real sembrada: lo que cuentan coincide con lo que hay en las tablas.</summary>
[Collection(BaseDeDatosCollection.Nombre)]
public class ReportesPersistenciaTests
{
    private readonly BaseDeDatosFixture _bd;
    public ReportesPersistenciaTests(BaseDeDatosFixture bd) => _bd = bd;

    private async Task<string> BaseSembradaAsync(string nombre)
    {
        await using (var db = _bd.Contexto(nombre))
        {
            await db.Database.MigrateAsync();
            db.Usuarios.Add(Usuario.Crear("admin", "Administrador", "admin@gestionpedidos.local", "hash", 1, false));
            await db.SaveChangesAsync();
        }
        await using (var db = _bd.Contexto(nombre))
            await new DatosDeEjemplo(db, new HasherClave(), TimeProvider.System, NullLogger<DatosDeEjemplo>.Instance,
                new GestionPedidos.Infrastructure.Almacen.AlmacenImagenesEnMemoria()).SembrarAsync(default);
        return nombre;
    }

    private static decimal Suma(TablaReporte t, int columna) => t.Filas.Sum(f => Convert.ToDecimal(f[columna]));

    [Fact]
    public async Task LosReportes_CuadranConLasTablas()
    {
        await using var db = _bd.Contexto(await BaseSembradaAsync("ReportesCuadran"));
        var repo = new ReportesRepository(db);
        var ahora = DateTimeOffset.UtcNow;

        var porDepartamento = await repo.ClientesPorZonaAsync(false, ahora, default);
        var porDistrito = await repo.ClientesPorZonaAsync(true, ahora, default);
        var clientes = await db.Clientes.CountAsync();
        Assert.True(clientes > 0);
        Assert.Equal(clientes, Suma(porDepartamento, 1));                 // todos los clientes aparecen una vez
        Assert.Equal(clientes, Suma(porDistrito, 2));
        Assert.All(porDepartamento.Filas, f => Assert.Equal(Convert.ToInt32(f[1]), Convert.ToInt32(f[2]) + Convert.ToInt32(f[3])));   // activos + inactivos = total

        var productos = (await repo.ProductosPorCategoriaAsync(null, ahora, default))!;
        Assert.Equal(await db.Productos.CountAsync(), Suma(productos, 2));
        Assert.Equal(await db.Productos.SumAsync(p => p.CantidadStock * p.PrecioVenta), Suma(productos, 5));
        // Debajo del resumen va el listado: cada producto aparece en el bloque de su categoría (o en «Sin clasificar»).
        Assert.Equal(await db.Productos.CountAsync(), productos.Secciones!.Sum(b => b.Filas.Count));
        Assert.All(productos.Secciones!, b => Assert.All(b.Filas, f => Assert.NotNull(f[0])));   // todos con su código

        var usuarios = await repo.UsuariosPorRolAsync(ahora, default);
        Assert.Equal(await db.Usuarios.CountAsync(), usuarios.Filas.Count);
        Assert.DoesNotContain(usuarios.Columnas, c => c.Titulo.Contains("lave", StringComparison.OrdinalIgnoreCase));   // nunca la clave ni su hash

        // Permisos: un bloque por rol (el rol arriba y la lista de sus permisos debajo); no hay tabla principal.
        var permisos = await repo.PermisosPorRolAsync(ahora, default);
        Assert.Empty(permisos.Columnas);
        Assert.Equal(await db.Roles.CountAsync(), permisos.Secciones!.Count);
        Assert.Equal(await db.RolesPermisos.CountAsync(), permisos.Secciones!.Sum(b => b.Filas.Count));
        var admin = permisos.Secciones!.Single(b => b.Titulo == "Administrador");
        Assert.Equal(await db.RolesPermisos.CountAsync(rp => rp.Rol!.Nombre == "Administrador"), admin.Filas.Count);
        Assert.Contains(admin.Filas, f => (string?)f[1] == "clientes.ver" && (string?)f[0] == "Clientes");   // módulo, código y descripción
        Assert.Equal(new[] { "Módulo", "Permiso", "Descripción" }, admin.Columnas.Select(c => c.Titulo));

        var pedidos = await repo.PedidosPorEstadoAsync(null, null, ahora, default);
        Assert.True(pedidos.Filas.Count >= 3);
        Assert.Equal(await db.Pedidos.CountAsync(), Suma(pedidos, 1));
        Assert.All(pedidos.Filas, f => Assert.Equal(Convert.ToInt32(f[1]), Convert.ToInt32(f[2]) + Convert.ToInt32(f[3])));
    }

    [Fact]
    public async Task Actividades_FiltraPorUsuarioYFecha_YRecortaAlMaximo()
    {
        // Base solo migrada (sin datos de ejemplo): esta prueba crea las actividades que necesita y así es más liviana.
        await using var db = _bd.Contexto("ReportesActividades");
        await db.Database.MigrateAsync();
        db.Usuarios.Add(Usuario.Crear("admin", "Administrador", "admin@gestionpedidos.local", "hash", 1, false));
        await db.SaveChangesAsync();
        var admin = await db.Usuarios.FirstAsync(u => u.NombreUsuario == "admin");
        var ahora = DateTimeOffset.UtcNow;
        db.Actividades.AddRange(Enumerable.Range(1, 5).Select(i => new GestionPedidos.Domain.Historial.Actividad
        { UsuarioId = admin.Id, Fecha = ahora.AddDays(-i), Accion = i % 2 == 0 ? "Edición" : "Prueba", Entidad = "Reporte", Detalle = $"n{i}" }));
        await db.SaveChangesAsync();
        var repo = new ReportesRepository(db);

        // De un usuario: ficha con sus datos, resumen por acción como tabla principal y el detalle en un bloque.
        var todas = (await repo.ActividadesPorUsuarioAsync(admin.Id, null, null, 100, ahora, default))!;
        var detalleTodas = todas.Secciones!.Single(b => b.Titulo == "Detalle de actividades");
        Assert.Equal(await db.Actividades.CountAsync(a => a.UsuarioId == admin.Id), detalleTodas.Filas.Count);
        Assert.Null(todas.Nota);
        Assert.Contains(todas.Ficha!, d => d.Etiqueta == "Usuario" && d.Valor == "admin");
        Assert.Contains(todas.Ficha!, d => d.Etiqueta == "Nombre" && d.Valor == "Administrador");
        Assert.Contains(todas.Indicadores!, d => d.Etiqueta == "Actividades" && d.Valor == "5");
        Assert.Equal(5, todas.Filas.Sum(f => Convert.ToInt32(f[1])));               // el resumen por acción suma todas
        Assert.Equal(new[] { "Edición", "Prueba" }, todas.Filas.Select(f => (string?)f[0]).OrderBy(x => x));

        var rango = (await repo.ActividadesPorUsuarioAsync(admin.Id, ahora.AddDays(-3.5), ahora.AddDays(-0.5), 100, ahora, default))!;
        Assert.Equal(new[] { "n1", "n2", "n3" }, rango.Secciones![0].Filas.Select(f => (string?)f[3]).OrderBy(x => x));   // columna Detalle
        Assert.Contains(rango.Ficha!, d => d.Etiqueta == "Período" && d.Valor != "todo el historial");

        var recortada = (await repo.ActividadesPorUsuarioAsync(admin.Id, null, null, 2, ahora, default))!;
        Assert.Equal(2, recortada.Secciones![0].Filas.Count);
        Assert.Contains("2 actividades más recientes", recortada.Nota);

        // Un usuario que no existe no es un reporte vacío: es «no existe».
        Assert.Null(await repo.ActividadesPorUsuarioAsync(987654, null, null, 100, ahora, default));
    }

    [Fact]
    public async Task Actividades_SinUsuario_ResumeYAgrupaUnBloquePorUsuario()
    {
        await using var db = _bd.Contexto("ReportesActividadesTodos");
        await db.Database.MigrateAsync();
        db.Usuarios.Add(Usuario.Crear("admin", "Administrador", "admin@gestionpedidos.local", "hash", 1, false));
        db.Usuarios.Add(Usuario.Crear("ana", "Ana López", "ana@gestionpedidos.local", "hash", 1, false));
        await db.SaveChangesAsync();
        var ids = await db.Usuarios.OrderBy(u => u.NombreUsuario).Select(u => u.Id).ToListAsync();
        var ahora = DateTimeOffset.UtcNow;
        db.Actividades.AddRange(
            new GestionPedidos.Domain.Historial.Actividad { UsuarioId = ids[0], Fecha = ahora.AddDays(-1), Accion = "Login", Entidad = "Usuario", Detalle = "a1" },
            new GestionPedidos.Domain.Historial.Actividad { UsuarioId = ids[0], Fecha = ahora.AddDays(-2), Accion = "Login", Entidad = "Usuario", Detalle = "a2" },
            new GestionPedidos.Domain.Historial.Actividad { UsuarioId = ids[1], Fecha = ahora.AddDays(-1), Accion = "Login", Entidad = "Usuario", Detalle = "b1" });
        await db.SaveChangesAsync();

        var t = (await new ReportesRepository(db).ActividadesPorUsuarioAsync(null, null, null, 100, ahora, default))!;
        Assert.Equal(new[] { "Usuario", "Rol", "Actividades" }, t.Columnas.Select(c => c.Titulo));
        Assert.Equal(new[] { 2, 1 }, t.Filas.Select(f => Convert.ToInt32(f[2])));          // resumen por usuario
        Assert.Equal(2, t.Secciones!.Count);                                               // un bloque por usuario
        Assert.Equal(new[] { "a1", "a2" }, t.Secciones![0].Filas.Select(f => (string?)f[3]));   // la más reciente primero
        Assert.Contains("Administrador", t.Secciones![0].Titulo);
        Assert.Contains(t.Indicadores!, d => d.Etiqueta == "Usuarios con actividad" && d.Valor == "2");
    }

    [Fact]
    public async Task Productos_DeUnaCategoria_TraeFichaSubcategoriasFiltrosYProductos()
    {
        await using var db = _bd.Contexto(await BaseSembradaAsync("ReportesCategoria"));
        var repo = new ReportesRepository(db);
        var ahora = DateTimeOffset.UtcNow;
        var categoria = await db.Categorias.OrderBy(c => c.Id).FirstAsync(c => c.Subcategorias.Any(s => s.Filtros.Any()));

        var t = (await repo.ProductosPorCategoriaAsync(categoria.Id, ahora, default))!;

        Assert.Equal("Categoría: " + categoria.Nombre, t.Titulo);
        Assert.Contains(t.Ficha!, d => d.Etiqueta == "Código" && d.Valor == categoria.Codigo);
        // Tabla principal: una fila por subcategoría, con los productos que la cuadran contra la base.
        Assert.Equal(await db.Subcategorias.CountAsync(s => s.CategoriaId == categoria.Id), t.Filas.Count);
        var productosDeLaCategoria = await db.Productos.CountAsync(p => p.Subcategoria!.CategoriaId == categoria.Id);
        Assert.Equal(productosDeLaCategoria, Suma(t, 1));
        // Bloques: subcategorías con sus filtros y opciones, y los productos de la categoría.
        var filtros = t.Secciones!.Single(b => b.Titulo == "Subcategorías y filtros");
        Assert.Equal(new[] { "Subcategoría", "Filtro", "Opciones" }, filtros.Columnas.Select(c => c.Titulo));
        Assert.Contains(filtros.Filas, f => (string?)f[1] != "—" && !string.IsNullOrEmpty((string?)f[2]));
        var lista = t.Secciones!.Single(b => b.Titulo == "Productos de la categoría");
        Assert.Equal(productosDeLaCategoria, lista.Filas.Count);
        Assert.Contains(t.Indicadores!, d => d.Etiqueta == "Productos" && d.Valor == productosDeLaCategoria.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-SV")));

        // Una categoría que no existe no es un reporte vacío: es «no existe».
        Assert.Null(await repo.ProductosPorCategoriaAsync(987654, ahora, default));
    }

    [Fact]
    public async Task Opciones_ListanUsuariosYCategoriasOrdenados()
    {
        await using var db = _bd.Contexto(await BaseSembradaAsync("ReportesOpciones"));
        var repo = new ReportesRepository(db);
        var usuarios = await repo.UsuariosAsync(default);
        var categorias = await repo.CategoriasAsync(default);
        Assert.Equal(await db.Usuarios.CountAsync(), usuarios.Count);
        Assert.Contains(usuarios, u => u.Texto == "Administrador (admin)");
        Assert.Equal(categorias.Select(c => c.Texto).OrderBy(x => x, StringComparer.Ordinal), categorias.Select(c => c.Texto).OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(await db.Categorias.CountAsync(), categorias.Count);
    }
}
