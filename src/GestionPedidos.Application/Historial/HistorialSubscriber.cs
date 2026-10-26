using GestionPedidos.Application.Eventos;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Historial;

namespace GestionPedidos.Application.Historial;

/// <summary>Puerto para guardar una actividad (la implementación decide cómo y cuándo se escribe).</summary>
public interface IRegistroActividades
{
    /// <summary>Encola el registro de una actividad.</summary>
    void Registrar(Actividad actividad);
}

/// <summary>Consulta del historial de actividades (solo lectura).</summary>
public interface IActividadRepository
{
    /// <summary>Busca actividades, de la más reciente a la más antigua.</summary>
    Task<(IReadOnlyList<ActividadDto> Elementos, int Total)> BuscarAsync(int? usuarioId, string? q, string? entidad, string? accion,
        DateTimeOffset? desde, DateTimeOffset? hasta, int pagina, int tamano, CancellationToken ct);
    /// <summary>Entidades y acciones distintas que existen en el historial (para llenar los filtros).</summary>
    Task<(IReadOnlyList<string> Entidades, IReadOnlyList<string> Acciones)> ValoresDeFiltroAsync(CancellationToken ct);
}

/// <summary>
/// Suscriptor del historial de actividades ([HistorialActividades.md §2]): escucha los eventos de aplicación
/// y registra cada acción, sin que los casos de uso sepan que existe.
/// </summary>
public sealed class HistorialSubscriber : IDisposable
{
    private readonly EventosAplicacion _eventos;
    private readonly IRegistroActividades _registro;

    /// <summary>Se suscribe a los eventos (<c>+=</c>); se da de baja en <see cref="Dispose"/>.</summary>
    public HistorialSubscriber(EventosAplicacion eventos, IRegistroActividades registro)
    {
        _eventos = eventos;
        _registro = registro;
        _eventos.EntidadCambiada += Eventos_EntidadCambiada;
        _eventos.AccesoRegistrado += Eventos_AccesoRegistrado;
    }

    private void Eventos_EntidadCambiada(object sender, EntidadCambiadaServidorEventArgs e) =>
        _registro.Registrar(new Actividad
        {
            UsuarioId = e.UsuarioId, Accion = e.Tipo, Entidad = e.Entidad, EntidadId = e.EntidadId, Detalle = Recortar(e.Detalle),
        });

    private void Eventos_AccesoRegistrado(object sender, AccesoRegistradoEventArgs e) =>
        _registro.Registrar(new Actividad
        {
            UsuarioId = e.UsuarioId, Accion = e.Exitoso ? "Login" : "LoginFallido", Entidad = "Usuario",
            EntidadId = e.UsuarioId, Detalle = Recortar($"Usuario intentado: {e.Identificador}"),   // nunca la contraseña
        });

    private static string? Recortar(string? texto) => texto is { Length: > 500 } ? texto[..500] : texto;

    /// <summary>Baja de las suscripciones (par obligatorio del <c>+=</c>).</summary>
    public void Dispose()
    {
        _eventos.EntidadCambiada -= Eventos_EntidadCambiada;
        _eventos.AccesoRegistrado -= Eventos_AccesoRegistrado;
    }
}
