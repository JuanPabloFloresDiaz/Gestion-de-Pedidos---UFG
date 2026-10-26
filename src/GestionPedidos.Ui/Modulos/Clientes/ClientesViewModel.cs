using GestionPedidos.Ui.Comun;

namespace GestionPedidos.Ui.Modulos.Clientes;

/// <summary>
/// ESQUELETO del ViewModel de Clientes (bloque C de Tareas.md). Aquí va el estado de la pantalla (filtros, página de resultados, panel de edición,
/// errores de validación) y su lógica, sin referencias a Blazor; modelo a copiar: <c>UsuariosViewModel</c>. Spec: <c>docs/Especificaciones/Clientes.md</c>.
/// </summary>
public sealed class ClientesViewModel : ViewModelBase
{
    private readonly ClientesService _servicio;

    /// <summary>Crea el ViewModel.</summary>
    public ClientesViewModel(ClientesService servicio) => _servicio = servicio;

    // TODO (bloque C): propiedades de estado, IniciarAsync(), BuscarAsync(), Nuevo(), Editar(), GuardarAsync(), CambiarEstado…
    // Use EjecutarAsync(...) de ViewModelBase para mostrar la carga y traducir los errores de la API a mensajes.
}
