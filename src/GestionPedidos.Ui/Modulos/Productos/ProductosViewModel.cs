using GestionPedidos.Ui.Comun;

namespace GestionPedidos.Ui.Modulos.Productos;

/// <summary>
/// ESQUELETO del ViewModel de Productos (bloque P de Tareas.md). Aquí va el estado de la pantalla (filtros, página de resultados, panel de edición,
/// errores de validación) y su lógica, sin referencias a Blazor; modelo a copiar: <c>UsuariosViewModel</c>. Spec: <c>docs/Especificaciones/Productos.md</c>.
/// </summary>
public sealed class ProductosViewModel : ViewModelBase
{
    private readonly ProductosService _servicio;

    /// <summary>Crea el ViewModel.</summary>
    public ProductosViewModel(ProductosService servicio) => _servicio = servicio;

    // TODO (bloque P): propiedades de estado, IniciarAsync(), BuscarAsync(), Nuevo(), Editar(), GuardarAsync(), CambiarEstado…
    // Use EjecutarAsync(...) de ViewModelBase para mostrar la carga y traducir los errores de la API a mensajes.
}
