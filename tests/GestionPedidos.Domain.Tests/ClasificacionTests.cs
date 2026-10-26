using GestionPedidos.Domain.Catalogo;
using GestionPedidos.Domain.Productos;

namespace GestionPedidos.Domain.Tests;

public class ClasificacionTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DarDeBaja_GuardaLaFecha_YReactivarLaLimpia()
    {
        var categoria = new Categoria { Nombre = "Ropa" };

        categoria.CambiarEstado(false, Ahora);
        Assert.False(categoria.Activo);
        Assert.Equal(Ahora, categoria.EliminadoEn);

        categoria.CambiarEstado(true, Ahora.AddDays(1));
        Assert.True(categoria.Activo);
        Assert.Null(categoria.EliminadoEn);
    }

    [Fact]
    public void DarDeBajaDosVeces_ConservaLaFechaOriginal()
    {
        var opcion = new OpcionFiltro { Valor = "Rojo" };
        opcion.CambiarEstado(false, Ahora);
        opcion.CambiarEstado(false, Ahora.AddDays(5));
        Assert.Equal(Ahora, opcion.EliminadoEn);
    }

    [Fact]
    public void Producto_EstablecerClasificacion_ReemplazaElDetalle()
    {
        var p = new Producto { Nombre = "Camisa" };
        p.EstablecerClasificacion(7, new[] { (1, 10), (2, 20) });
        Assert.Equal(7, p.SubcategoriaId);
        Assert.Equal(2, p.Opciones.Count);

        p.EstablecerClasificacion(8, new[] { (3, 30) });   // cambiar de subcategoría descarta lo anterior
        Assert.Equal(8, p.SubcategoriaId);
        var unica = Assert.Single(p.Opciones);
        Assert.Equal(30, unica.OpcionFiltroId);
    }

    [Fact]
    public void Producto_UnaSolaOpcionPorFiltro()
    {
        var p = new Producto();
        Assert.Throws<ArgumentException>(() => p.EstablecerClasificacion(1, new[] { (1, 10), (1, 11) }));
    }

    [Fact]
    public void Producto_SinSubcategoria_NoPuedeTenerDetalle_PeroSiQuedarSinClasificar()
    {
        var p = new Producto();
        Assert.Throws<ArgumentException>(() => p.EstablecerClasificacion(null, new[] { (1, 10) }));
        p.EstablecerClasificacion(null, Array.Empty<(int, int)>());
        Assert.Null(p.SubcategoriaId);
    }
}
