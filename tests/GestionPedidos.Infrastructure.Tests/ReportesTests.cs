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
        Assert.Equal("Reporte de prueba", hoja.Cell(1, 1).GetString());
        Assert.Equal("Nombre", hoja.Cell(5, 1).GetString());
        Assert.Equal("=1+1", hoja.Cell(6, 1).GetString());
        Assert.False(hoja.Cell(6, 1).HasFormula);   // es texto, nunca una fórmula
        Assert.Equal(XLDataType.Number, hoja.Cell(6, 2).DataType);
        Assert.Equal(7, hoja.Cell(6, 2).GetDouble());
        Assert.Equal(19.99, hoja.Cell(6, 3).GetDouble(), 2);
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
    public async Task LosCincoReportes_CuadranConLasTablas()
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

        var productos = await repo.ProductosPorCategoriaAsync(ahora, default);
        Assert.Equal(await db.Productos.CountAsync(), Suma(productos, 2));
        Assert.Equal(await db.Productos.SumAsync(p => p.CantidadStock * p.PrecioVenta), Suma(productos, 5));

        var usuarios = await repo.UsuariosPorRolAsync(ahora, default);
        Assert.Equal(await db.Usuarios.CountAsync(), usuarios.Filas.Count);
        Assert.DoesNotContain(usuarios.Columnas, c => c.Titulo.Contains("lave", StringComparison.OrdinalIgnoreCase));   // nunca la clave ni su hash

        var permisos = await repo.PermisosPorRolAsync(ahora, default);
        Assert.Equal(await db.RolesPermisos.CountAsync(), permisos.Filas.Count);
        Assert.Contains(permisos.Filas, f => (string?)f[0] == "Administrador");
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
        { UsuarioId = admin.Id, Fecha = ahora.AddDays(-i), Accion = "Prueba", Entidad = "Reporte", Detalle = $"n{i}" }));
        await db.SaveChangesAsync();
        var repo = new ReportesRepository(db);

        var todas = await repo.ActividadesPorUsuarioAsync(admin.Id, null, null, 100, ahora, default);
        Assert.Equal(await db.Actividades.CountAsync(a => a.UsuarioId == admin.Id), todas.Filas.Count);
        Assert.Null(todas.Nota);

        var rango = await repo.ActividadesPorUsuarioAsync(admin.Id, ahora.AddDays(-3.5), ahora.AddDays(-0.5), 100, ahora, default);
        Assert.Equal(new[] { "n1", "n2", "n3" }, rango.Filas.Select(f => (string?)f[4]).OrderBy(x => x));

        var recortada = await repo.ActividadesPorUsuarioAsync(admin.Id, null, null, 2, ahora, default);
        Assert.Equal(2, recortada.Filas.Count);
        Assert.Contains("primeras 2", recortada.Nota);
    }
}
