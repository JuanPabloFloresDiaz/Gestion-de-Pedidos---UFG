using GestionPedidos.Domain.Catalogo;
using GestionPedidos.Domain.Productos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GestionPedidos.Infrastructure.Tests;

/// <summary>Restricciones reales de la clasificación del catálogo contra SQL Server ([BaseDatos.md §4.7]).</summary>
[Collection(BaseDeDatosCollection.Nombre)]
public class ClasificacionTests
{
    private readonly BaseDeDatosFixture _bd;
    public ClasificacionTests(BaseDeDatosFixture bd) => _bd = bd;

    private static string U(string p) => $"{p}-{Guid.NewGuid():N}"[..24];

    private async Task<(Categoria Cat, Subcategoria Sub, Filtro Color, Filtro Talla, OpcionFiltro Negro, OpcionFiltro M)> ArmarAsync()
    {
        await using var db = _bd.Contexto();
        var cat = new Categoria { Nombre = U("Cat") };
        var sub = new Subcategoria { Categoria = cat, Nombre = "Camisas" };
        var color = new Filtro { Subcategoria = sub, Nombre = "Color" };
        var talla = new Filtro { Subcategoria = sub, Nombre = "Talla" };
        var negro = new OpcionFiltro { Filtro = color, Valor = "Negro" };
        var m = new OpcionFiltro { Filtro = talla, Valor = "M" };
        db.AddRange(cat, sub, color, talla, negro, m);
        await db.SaveChangesAsync();
        return (cat, sub, color, talla, negro, m);
    }

    [Fact]
    public async Task NombresUnicos_PorPadre_SinDistinguirMayusculas()
    {
        var (cat, sub, color, _, _, _) = await ArmarAsync();
        await using var db = _bd.Contexto();

        db.Categorias.Add(new Categoria { Nombre = cat.Nombre.ToUpperInvariant() });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        db.Subcategorias.Add(new Subcategoria { CategoriaId = cat.Id, Nombre = "CAMISAS" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        db.OpcionesFiltro.Add(new OpcionFiltro { FiltroId = color.Id, Valor = "negro" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        // El mismo nombre en otro padre sí se permite
        db.Subcategorias.Add(new Subcategoria { Categoria = new Categoria { Nombre = U("Otra") }, Nombre = "Camisas" });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Detalle_UnaOpcionPorFiltro_YLaOpcionDebeSerDelFiltroIndicado()
    {
        var (_, sub, color, talla, negro, m) = await ArmarAsync();
        await using var db = _bd.Contexto();
        var p = new Producto { Nombre = U("Camisa"), Descripcion = "d", CantidadStock = 1, PrecioVenta = 10, SubcategoriaId = sub.Id };
        p.EstablecerClasificacion(sub.Id, new[] { (color.Id, negro.Id), (talla.Id, m.Id) });
        db.Productos.Add(p);
        await db.SaveChangesAsync();   // opciones correctas: se guarda
        Assert.Equal(2, await db.ProductosOpciones.CountAsync(x => x.ProductoId == p.Id));

        // Dos opciones del mismo filtro: choca con la clave primaria (ProductoId, FiltroId)
        var duplicada = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlAsync(
            $"INSERT INTO dbo.ProductosOpciones (ProductoId, FiltroId, OpcionFiltroId) VALUES ({p.Id}, {color.Id}, {negro.Id})"));
        Assert.Equal(2627, duplicada.Number);   // violación de clave primaria
        db.ChangeTracker.Clear();

        // Opción de OTRO filtro bajo este filtro: la clave foránea compuesta (Opcion, Filtro) lo impide
        var p2 = new Producto { Nombre = U("Camisa2"), Descripcion = "d", CantidadStock = 1, PrecioVenta = 10, SubcategoriaId = sub.Id };
        db.Productos.Add(p2);
        await db.SaveChangesAsync();
        db.ProductosOpciones.Add(new ProductoOpcion { ProductoId = p2.Id, FiltroId = color.Id, OpcionFiltroId = m.Id });   // M es de Talla
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.IsType<SqlException>(ex.InnerException);
        Assert.Equal(547, ((SqlException)ex.InnerException!).Number);   // violación de clave foránea
    }

    [Fact]
    public async Task NoSePuedeBorrar_LoQueTieneHijosOProductos()
    {
        var (cat, sub, color, _, negro, _) = await ArmarAsync();
        await using var db = _bd.Contexto();
        var p = new Producto { Nombre = U("Camisa"), Descripcion = "d", CantidadStock = 1, PrecioVenta = 10 };
        p.EstablecerClasificacion(sub.Id, new[] { (color.Id, negro.Id) });
        db.Productos.Add(p);
        await db.SaveChangesAsync();

        foreach (var sql in new[]
        {
            $"DELETE FROM dbo.OpcionesFiltro WHERE Id = {negro.Id}",    // la usa un producto
            $"DELETE FROM dbo.Filtros WHERE Id = {color.Id}",           // tiene opciones
            $"DELETE FROM dbo.Subcategorias WHERE Id = {sub.Id}",       // tiene filtros y productos
            $"DELETE FROM dbo.Categorias WHERE Id = {cat.Id}",          // tiene subcategorías
        })
        {
            var ex = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync(sql));
            Assert.Equal(547, ex.Number);
        }
    }

    [Fact]
    public async Task ProductoSinClasificar_SigueSiendoValido_YElDetalleSeBorraConElProducto()
    {
        var (_, sub, color, _, negro, _) = await ArmarAsync();
        await using var db = _bd.Contexto();
        var suelto = new Producto { Nombre = U("Suelto"), Descripcion = "d", CantidadStock = 1, PrecioVenta = 1 };
        db.Productos.Add(suelto);
        await db.SaveChangesAsync();
        Assert.Null((await db.Productos.AsNoTracking().SingleAsync(p => p.Id == suelto.Id)).SubcategoriaId);

        var conDetalle = new Producto { Nombre = U("Con"), Descripcion = "d", CantidadStock = 1, PrecioVenta = 1 };
        conDetalle.EstablecerClasificacion(sub.Id, new[] { (color.Id, negro.Id) });
        db.Productos.Add(conDetalle);
        await db.SaveChangesAsync();
        db.Productos.Remove(conDetalle);
        await db.SaveChangesAsync();
        Assert.Equal(0, await db.ProductosOpciones.CountAsync(x => x.ProductoId == conDetalle.Id));
    }
}
