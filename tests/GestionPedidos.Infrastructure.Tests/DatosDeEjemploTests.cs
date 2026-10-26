using GestionPedidos.Domain.Pedidos;
using GestionPedidos.Domain.Seguridad;
using GestionPedidos.Infrastructure.Almacen;
using GestionPedidos.Infrastructure.Persistence.Seed;
using GestionPedidos.Infrastructure.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GestionPedidos.Infrastructure.Tests;

/// <summary>El sembrador de datos de ejemplo llena una base real con datos coherentes y no se repite.</summary>
[Collection(BaseDeDatosCollection.Nombre)]
public class DatosDeEjemploTests
{
    private readonly BaseDeDatosFixture _bd;
    public DatosDeEjemploTests(BaseDeDatosFixture bd) => _bd = bd;

    private readonly AlmacenImagenesEnMemoria _almacen = new();

    private async Task<string> BaseSembradaAsync(string nombre)
    {
        await using (var db = _bd.Contexto(nombre))
        {
            await db.Database.MigrateAsync();
            db.Usuarios.Add(Usuario.Crear("admin", "Administrador", "admin@gestionpedidos.local", "hash", 1, false));
            await db.SaveChangesAsync();
        }
        await using (var db = _bd.Contexto(nombre))
            await new DatosDeEjemplo(db, new HasherClave(), TimeProvider.System, NullLogger<DatosDeEjemplo>.Instance, _almacen).SembrarAsync(default);
        return nombre;
    }

    [Fact]
    public async Task Catalogo_CumpleLoPedido_SeisCategoriasConDosACincoSubcategoriasYUnoATresProductosCada()
    {
        await using var db = _bd.Contexto(await BaseSembradaAsync("EjemploCatalogo"));

        var activas = await db.Categorias.Where(c => c.Activo).Include(c => c.Subcategorias).ToListAsync();
        Assert.True(activas.Count >= 6);
        Assert.All(activas, c => Assert.InRange(c.Subcategorias.Count, 2, 5));

        foreach (var sub in activas.SelectMany(c => c.Subcategorias))
        {
            var productos = await db.Productos.CountAsync(p => p.SubcategoriaId == sub.Id);
            // Laptops (35) y Celulares (25) llevan marcas reales y mucho volumen a propósito; el resto, de 1 a 3 productos
            Assert.InRange(productos, 1, sub.Nombre == "Laptops" ? 35 : sub.Nombre == "Celulares" ? 25 : 3);
            Assert.True(await db.Filtros.AnyAsync(f => f.SubcategoriaId == sub.Id), $"{sub.Nombre} sin filtros");
        }
        // Todos los filtros tienen opciones y todo producto activo está descrito con una opción por filtro de su subcategoría
        Assert.False(await db.Filtros.AnyAsync(f => !f.Opciones.Any()));
        var productosActivos = await db.Productos.Where(p => p.Activo).Include(p => p.Opciones).ToListAsync();
        Assert.True(productosActivos.Count >= 50);
        foreach (var p in productosActivos)
            Assert.Equal(await db.Filtros.CountAsync(f => f.SubcategoriaId == p.SubcategoriaId), p.Opciones.Count);

        // Estados de ejemplo para probar la interfaz: algo inactivo y algo que se puede eliminar
        Assert.True(await db.Categorias.AnyAsync(c => !c.Activo && !c.Subcategorias.Any()));
        Assert.True(await db.Productos.AnyAsync(p => !p.Activo && p.EliminadoEn != null));
        Assert.True(await db.OpcionesFiltro.AnyAsync(o => !o.Activo));
        Assert.True(await db.Productos.AnyAsync(p => p.CantidadStock == 0));
    }

    [Fact]
    public async Task ClientesPedidosEHistorial_SonCoherentes()
    {
        await using var db = _bd.Contexto(await BaseSembradaAsync("EjemploNegocio"));

        Assert.True(await db.Clientes.CountAsync() >= 40);
        Assert.True(await db.Clientes.AnyAsync(c => !c.Activo));
        Assert.Equal(6, await db.Clientes.CountAsync(c => c.UsuarioId != null));   // cuentas web para probar la tienda
        var pedidos = await db.Pedidos.Include(p => p.Detalles).ToListAsync();
        Assert.True(pedidos.Count >= 70);
        Assert.Contains(pedidos, p => p.Estado == EstadoPedido.Pendiente);
        Assert.Contains(pedidos, p => p.Estado == EstadoPedido.Entregado);
        Assert.Contains(pedidos, p => p.Estado == EstadoPedido.Cancelado);
        Assert.All(pedidos, p => Assert.Equal(p.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario), p.Total));
        Assert.False(await db.Productos.AnyAsync(p => p.CantidadStock < 0));
        Assert.True(await db.Actividades.CountAsync() > 200);
        Assert.True(await db.Usuarios.AnyAsync(u => u.NombreUsuario == "rcruz" && !u.Activo));
    }

    [Fact]
    public async Task Imagenes_SeSubenConElCodigoComoNombre_YQuedanLigadasAlProductoYALaCategoria()
    {
        await using var db = _bd.Contexto(await BaseSembradaAsync("EjemploImagenes"));

        // Todos los productos y categorías tienen un código humano y estable; las imágenes se llaman como él
        Assert.False(await db.Productos.AnyAsync(p => p.Codigo.StartsWith("PRD-")));
        Assert.Contains(await db.Productos.Select(p => p.Codigo).ToListAsync(), c => c == "ROP-CAM-001");
        var conImagen = await db.Productos.Where(p => p.ImagenClave != null).ToListAsync();
        Assert.True(conImagen.Count >= 100);
        Assert.All(conImagen, p => Assert.Equal($"productos/{p.Codigo}.jpg", p.ImagenClave));
        Assert.All(await db.Categorias.Where(c => c.ImagenClave != null).ToListAsync(), c => Assert.Equal($"categorias/{c.Codigo}.jpg", c.ImagenClave));
        Assert.True(await db.Categorias.CountAsync(c => c.ImagenClave != null) >= 6);
        Assert.Equal(conImagen.Count + await db.Categorias.CountAsync(c => c.ImagenClave != null), _almacen.Cantidad);   // nada de objetos sueltos

        // Pedido del usuario: 35 laptops y 25 celulares, cada uno con su marca real y su foto
        var laptops = await db.Productos.Where(p => p.Subcategoria!.Nombre == "Laptops").Include(p => p.Opciones).ThenInclude(o => o.Opcion).Include(p => p.Opciones).ThenInclude(o => o.Filtro).ToListAsync();
        var celulares = await db.Productos.Where(p => p.Subcategoria!.Nombre == "Celulares").Include(p => p.Opciones).ThenInclude(o => o.Opcion).Include(p => p.Opciones).ThenInclude(o => o.Filtro).ToListAsync();
        Assert.Equal(35, laptops.Count);
        Assert.Equal(25, celulares.Count);
        Assert.All(laptops.Concat(celulares), p => Assert.NotNull(p.ImagenClave));
        string Marca(Domain.Productos.Producto p) => p.Opciones.Single(o => o.Filtro!.Nombre == "Marca").Opcion!.Valor;
        Assert.All(new[] { "HP", "ASUS", "Lenovo", "Dell", "Acer", "MSI", "Apple" }, m => Assert.Contains(m, laptops.Select(Marca)));
        Assert.Contains("HP", laptops.Select(Marca)); Assert.Contains("ASUS", laptops.Select(Marca));
        Assert.Contains("Samsung", celulares.Select(Marca)); Assert.Contains("Motorola", celulares.Select(Marca));
        Assert.True(laptops.Select(Marca).Distinct().Count() >= 8);
        Assert.True(celulares.Select(Marca).Distinct().Count() >= 10);
        Assert.Equal(60, laptops.Concat(celulares).Select(p => p.ImagenClave).Distinct().Count());   // cada producto tiene SU imagen

        // Los que no tienen foto quedan sin clave (la tienda muestra su ícono), no con una clave rota
        Assert.True(await db.Productos.AnyAsync(p => p.ImagenClave == null));
    }

    [Fact]
    public async Task SinAlmacen_SeSiembraTodoMenosLasImagenes()
    {
        await using (var db = _bd.Contexto("EjemploSinAlmacen"))
        {
            await db.Database.MigrateAsync();
            db.Usuarios.Add(Usuario.Crear("admin", "Administrador", "admin@gestionpedidos.local", "hash", 1, false));
            await db.SaveChangesAsync();
        }
        await using (var db = _bd.Contexto("EjemploSinAlmacen"))
            await new DatosDeEjemplo(db, new HasherClave(), TimeProvider.System, NullLogger<DatosDeEjemplo>.Instance).SembrarAsync(default);
        await using var comprobar = _bd.Contexto("EjemploSinAlmacen");
        Assert.True(await comprobar.Productos.CountAsync() >= 60);
        Assert.False(await comprobar.Productos.AnyAsync(p => p.ImagenClave != null));
    }

    [Fact]
    public async Task Sembrar_UnaSegundaVez_NoDuplicaNada()
    {
        var nombre = await BaseSembradaAsync("EjemploIdempotente");
        await using var db = _bd.Contexto(nombre);
        var (categorias, productos, pedidos) = (await db.Categorias.CountAsync(), await db.Productos.CountAsync(), await db.Pedidos.CountAsync());

        await new DatosDeEjemplo(db, new HasherClave(), TimeProvider.System, NullLogger<DatosDeEjemplo>.Instance, _almacen).SembrarAsync(default);

        Assert.Equal(categorias, await db.Categorias.CountAsync());
        Assert.Equal(productos, await db.Productos.CountAsync());
        Assert.Equal(pedidos, await db.Pedidos.CountAsync());
    }
}
