using GestionPedidos.Ui.Comun.Api;
using GestionPedidos.Ui.Comun.Eventos;

namespace GestionPedidos.Ui.Modulos.Clientes;

/// <summary>
/// ESQUELETO del servicio de Clientes (bloque C de Tareas.md): llama a la API y <b>publica <c>EntidadCambiada</c> solo después de que la API confirme</b>
/// (así el <c>Notificador</c> avisa y los listados abiertos se refrescan). Modelo a copiar: <c>UsuariosService</c>. Spec: <c>docs/Especificaciones/Clientes.md</c>.
/// </summary>
public sealed class ClientesService : IPublicadorDeCambios
{
    private readonly ApiClient _api;

    /// <summary>Crea el servicio.</summary>
    public ClientesService(ApiClient api) => _api = api;

    /// <summary>Se produce tras crear, editar o cambiar el estado (clientes). Disparar con <c>?.Invoke</c> solo tras la respuesta exitosa.</summary>
#pragma warning disable CS0067 // esqueleto: el evento se dispara cuando se implementen las operaciones (ver TODO)
    public event EntidadCambiadaEventHandler? EntidadCambiada;
#pragma warning restore CS0067

    // TODO (bloque C): BuscarAsync, ObtenerAsync, CrearAsync, ActualizarAsync, CambiarEstadoAsync…
    // Al terminar cada operación: EntidadCambiada?.Invoke(this, new EntidadCambiadaEventArgs { Entidad = TipoEntidad.Cliente, Id = …, Tipo = TipoCambio.Registrada, Descripcion = …, Activo = … });
}
