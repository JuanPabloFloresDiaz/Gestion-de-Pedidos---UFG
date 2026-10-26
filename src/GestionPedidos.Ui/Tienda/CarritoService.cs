using System.Text.Json;
using Microsoft.JSInterop;

namespace GestionPedidos.Ui.Tienda;

/// <summary>Una línea del carrito.</summary>
/// <param name="ProductoId">Producto.</param>
/// <param name="Nombre">Nombre.</param>
/// <param name="Precio">Precio por unidad cuando se agregó.</param>
/// <param name="Cantidad">Unidades.</param>
/// <param name="Maximo">Tope por pedido que informó la tienda.</param>
/// <param name="Icono">Ícono de su categoría (solo presentación).</param>
/// <param name="Tono">Tono de su categoría (solo presentación).</param>
public sealed record LineaCarrito(int ProductoId, string Nombre, decimal Precio, int Cantidad, int Maximo, string Icono, int Tono)
{
    /// <summary>Precio × cantidad.</summary>
    public decimal Subtotal => Precio * Cantidad;
}

/// <summary>
/// Carrito de compras de la visita. Vive en memoria y se guarda en el almacenamiento local del navegador para que no se
/// pierda al recargar. El precio del carrito es solo informativo: al hacer el pedido el servidor usa el precio vigente.
/// </summary>
public sealed class CarritoService
{
    private readonly IJSRuntime _js;
    private readonly List<LineaCarrito> _lineas = new();
    private bool _cargado;

    /// <summary>Crea el servicio.</summary>
    public CarritoService(IJSRuntime js) => _js = js;

    /// <summary>Se produce cuando cambia el contenido del carrito.</summary>
    public event EventHandler? Cambiado;

    /// <summary>Líneas del carrito.</summary>
    public IReadOnlyList<LineaCarrito> Lineas => _lineas;
    /// <summary>Total de unidades.</summary>
    public int Cantidad => _lineas.Sum(l => l.Cantidad);
    /// <summary>Total a pagar.</summary>
    public decimal Total => _lineas.Sum(l => l.Subtotal);

    /// <summary>Lee el carrito guardado (solo se puede llamar después del primer render, cuando hay JavaScript).</summary>
    public async Task InicializarAsync()
    {
        if (_cargado) return;
        _cargado = true;
        try
        {
            var json = await _js.InvokeAsync<string>("gpTienda.leer");
            var guardadas = JsonSerializer.Deserialize<List<LineaCarrito>>(json) ?? new();
            _lineas.Clear();
            _lineas.AddRange(guardadas.Where(l => l.Cantidad > 0 && l.Maximo > 0).Select(l => l with { Cantidad = Math.Min(l.Cantidad, l.Maximo) }));
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JsonException) { /* sin almacenamiento o datos dañados: carrito vacío */ }
        Cambiado?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Agrega unidades de un producto (sin pasar del máximo por pedido). Devuelve cuántas se agregaron.</summary>
    public async Task<int> AgregarAsync(LineaCarrito nueva)
    {
        var i = _lineas.FindIndex(l => l.ProductoId == nueva.ProductoId);
        var antes = i >= 0 ? _lineas[i].Cantidad : 0;
        var despues = Math.Min(antes + Math.Max(nueva.Cantidad, 1), nueva.Maximo);
        if (despues <= 0) return 0;
        var linea = nueva with { Cantidad = despues };
        if (i >= 0) _lineas[i] = linea; else _lineas.Add(linea);
        await GuardarAsync();
        return despues - antes;
    }

    /// <summary>Cambia la cantidad de una línea (entre 1 y su máximo).</summary>
    public async Task CambiarCantidadAsync(int productoId, int cantidad)
    {
        var i = _lineas.FindIndex(l => l.ProductoId == productoId);
        if (i < 0) return;
        _lineas[i] = _lineas[i] with { Cantidad = Math.Clamp(cantidad, 1, _lineas[i].Maximo) };
        await GuardarAsync();
    }

    /// <summary>Quita una línea.</summary>
    public async Task QuitarAsync(int productoId)
    {
        _lineas.RemoveAll(l => l.ProductoId == productoId);
        await GuardarAsync();
    }

    /// <summary>Vacía el carrito.</summary>
    public async Task VaciarAsync()
    {
        _lineas.Clear();
        await GuardarAsync();
    }

    private async Task GuardarAsync()
    {
        try { await _js.InvokeVoidAsync("gpTienda.guardar", JsonSerializer.Serialize(_lineas)); }
        catch (Exception ex) when (ex is JSException or InvalidOperationException) { /* sin almacenamiento: el carrito vale solo durante la visita */ }
        Cambiado?.Invoke(this, EventArgs.Empty);
    }
}
