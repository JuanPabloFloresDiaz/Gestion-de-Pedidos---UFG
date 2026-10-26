using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun;

namespace GestionPedidos.Ui.Tienda;

/// <summary>Estado y lógica de la página de un producto: su detalle, la cantidad elegida y productos relacionados.</summary>
public sealed class ProductoViewModel : ViewModelBase
{
    private readonly TiendaService _tienda;
    private readonly CarritoService _carrito;

    /// <summary>Crea el ViewModel.</summary>
    public ProductoViewModel(TiendaService tienda, CarritoService carrito) { _tienda = tienda; _carrito = carrito; }

    /// <summary>Producto mostrado, o <c>null</c> si no existe.</summary>
    public ProductoTiendaDto? Producto { get; private set; }
    /// <summary>Otros productos de la misma subcategoría.</summary>
    public IReadOnlyList<ProductoTiendaDto> Relacionados { get; private set; } = Array.Empty<ProductoTiendaDto>();
    /// <summary>Unidades elegidas.</summary>
    public int Cantidad { get; private set; } = 1;
    /// <summary>Indica si el producto no existe o no está a la venta.</summary>
    public bool NoEncontrado { get; private set; }
    /// <summary>Mensaje tras agregar al carrito, o <c>null</c>.</summary>
    public string? Aviso { get; private set; }

    /// <summary>Carga el producto y sus relacionados.</summary>
    public async Task CargarAsync(int id)
    {
        NoEncontrado = false; Producto = null; Cantidad = 1; Aviso = null;
        var ok = await EjecutarAsync(async ct =>
        {
            Producto = await _tienda.ObtenerAsync(id, ct);
            if (Producto.SubcategoriaId is { } sub)
            {
                var f = new FiltrosCatalogo(SubcategoriaId: sub, Pagina: 1);
                var pagina = await _tienda.BuscarAsync(f, ct);
                Relacionados = pagina.Elementos.Where(p => p.Id != id && p.Disponible).Take(4).ToList();
            }
        });
        // 404 de la API = el producto no existe, está dado de baja o su categoría está inactiva
        if (!ok && Producto is null && Error is not null) { NoEncontrado = true; Error = null; }
        OnCambiado();
    }

    /// <summary>Sube o baja la cantidad (entre 1 y el máximo por pedido).</summary>
    public void CambiarCantidad(int delta)
    {
        if (Producto is null) return;
        Cantidad = Math.Clamp(Cantidad + delta, 1, Math.Max(Producto.MaximoPorPedido, 1));
        OnCambiado();
    }

    /// <summary>Agrega la cantidad elegida al carrito.</summary>
    public async Task AgregarAlCarritoAsync()
    {
        if (Producto is not { Disponible: true } p) return;
        var agregadas = await _carrito.AgregarAsync(LineaDe(p, Cantidad));
        Aviso = agregadas > 0 ? $"Se agregó {p.Nombre} al carrito." : $"Ya tiene el máximo de {p.MaximoPorPedido} unidades de este producto en el carrito.";
        OnCambiado();
    }

    /// <summary>Línea de carrito de un producto (para agregar desde el detalle o desde una tarjeta).</summary>
    public static LineaCarrito LineaDe(ProductoTiendaDto p, int cantidad) => new(p.Id, p.Nombre, p.Precio, cantidad, p.MaximoPorPedido,
        p.Subcategoria, p.Categoria, p.CategoriaId, p.ImagenUrl);
}
