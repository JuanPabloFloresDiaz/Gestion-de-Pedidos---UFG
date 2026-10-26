using GestionPedidos.Ui.Comun;

namespace GestionPedidos.Ui.Modulos.Pedidos;

/// <summary>
/// ESQUELETO del ViewModel de Pedidos (bloque D de Tareas.md). Aquí va el estado de la pantalla (filtros, página de resultados, panel de edición,
/// errores de validación) y su lógica, sin referencias a Blazor; modelo a copiar: <c>UsuariosViewModel</c>. Spec: <c>docs/Especificaciones/Pedidos.md</c>.
/// </summary>
public sealed class PedidosViewModel : ViewModelBase
{
    private readonly PedidosService _servicio;

    /// <summary>Crea el ViewModel.</summary>
    public PedidosViewModel(PedidosService servicio) => _servicio = servicio;

    // TODO (bloque D): propiedades de estado, IniciarAsync(), BuscarAsync(), Nuevo(), Editar(), GuardarAsync(), CambiarEstado…
    // Use EjecutarAsync(...) de ViewModelBase para mostrar la carga y traducir los errores de la API a mensajes.
}
