using GestionPedidos.Domain.Pedidos;
using GestionPedidos.Domain.Seguridad;
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

    private async Task<string> BaseSembradaAsync(string nombre)
    {
        await using (var db = _bd.Contexto(nombre))
        {
            await db.Database.MigrateAsync();
            db.Usuarios.Add(Usuario.Crear("admin", "Administrador", "admin@gestionpedidos.local", "hash", 1, false));
            await db.SaveChangesAsync();
        }
        await using (var db = _bd.Contexto(nombre))
            await new DatosDeEjemplo(db, new HasherClave(), TimeProvider.System, NullLogger<DatosDeEjemplo>.Instance).SembrarAsync(default);
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
            Assert.InRange(productos, 1, 3);
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
    public async Task Sembrar_UnaSegundaVez_NoDuplicaNada()
    {
        var nombre = await BaseSembradaAsync("EjemploIdempotente");
        await using var db = _bd.Contexto(nombre);
        var (categorias, productos, pedidos) = (await db.Categorias.CountAsync(), await db.Productos.CountAsync(), await db.Pedidos.CountAsync());

        await new DatosDeEjemplo(db, new HasherClave(), TimeProvider.System, NullLogger<DatosDeEjemplo>.Instance).SembrarAsync(default);

        Assert.Equal(categorias, await db.Categorias.CountAsync());
        Assert.Equal(productos, await db.Productos.CountAsync());
        Assert.Equal(pedidos, await db.Pedidos.CountAsync());
    }
}
