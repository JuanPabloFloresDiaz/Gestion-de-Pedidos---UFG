using GestionPedidos.Ui.Comun.Eventos;

namespace GestionPedidos.Ui.Comun.Notificaciones;

/// <summary>Tipos de aviso en pantalla.</summary>
public enum TipoAviso
{
    /// <summary>Operación correcta.</summary>
    Exito,
    /// <summary>Información neutra.</summary>
    Info,
    /// <summary>Advertencia.</summary>
    Advertencia,
    /// <summary>Error.</summary>
    Error,
}

/// <summary>Un aviso no bloqueante (<i>snackbar</i>).</summary>
/// <param name="Id">Identificador único.</param>
/// <param name="Tipo">Clase de aviso.</param>
/// <param name="Texto">Mensaje.</param>
public sealed record Aviso(Guid Id, TipoAviso Tipo, string Texto);

/// <summary>Muestra avisos en pantalla; el componente <c>AvisosHost</c> se suscribe y los pinta.</summary>
public interface IAvisos
{
    /// <summary>Muestra un aviso de éxito.</summary>
    void Exito(string texto);
    /// <summary>Muestra un aviso informativo.</summary>
    void Info(string texto);
    /// <summary>Muestra una advertencia.</summary>
    void Advertencia(string texto);
    /// <summary>Muestra un error.</summary>
    void Error(string texto);
}

/// <summary>Servicio de avisos: publica el evento <see cref="Mostrado"/> para quien pinta la pantalla.</summary>
public sealed class AvisosService : IAvisos
{
    /// <summary>Se produce cuando hay un aviso nuevo.</summary>
    public event EventHandler<Aviso>? Mostrado;

    /// <summary>Último mensaje mostrado (lo usa la barra de estado).</summary>
    public Aviso? Ultimo { get; private set; }

    /// <inheritdoc/>
    public void Exito(string texto) => Publicar(TipoAviso.Exito, texto);
    /// <inheritdoc/>
    public void Info(string texto) => Publicar(TipoAviso.Info, texto);
    /// <inheritdoc/>
    public void Advertencia(string texto) => Publicar(TipoAviso.Advertencia, texto);
    /// <inheritdoc/>
    public void Error(string texto) => Publicar(TipoAviso.Error, texto);

    private void Publicar(TipoAviso tipo, string texto)
    {
        Ultimo = new Aviso(Guid.NewGuid(), tipo, texto);
        Mostrado?.Invoke(this, Ultimo);
    }
}

/// <summary>Textos de usuario centralizados (tono uniforme).</summary>
public static class Mensajes
{
    /// <summary>Mensaje de una notificación de cambio (alta, edición o cambio de estado).</summary>
    public static string Cambio(EntidadCambiadaEventArgs e)
    {
        var entidad = e.Entidad switch
        {
            TipoEntidad.Cliente => "Cliente", TipoEntidad.Producto => "Producto",
            TipoEntidad.Pedido => "Pedido", _ => "Usuario",
        };
        var accion = e.Tipo switch
        {
            TipoCambio.Registrada => "registrado",
            TipoCambio.Actualizada => "actualizado",
            _ => e.Activo ? "reactivado" : "dado de baja",
        };
        return $"{entidad} {e.Descripcion} {accion}.";
    }
}

/// <summary>Suscriptor que muestra un aviso por cada cambio que notifican los servicios de módulo.</summary>
public sealed class Notificador : IDisposable
{
    private readonly IReadOnlyList<IPublicadorDeCambios> _publicadores;
    private readonly IAvisos _avisos;

    /// <summary>Se suscribe (<c>+=</c>) a todos los publicadores registrados.</summary>
    public Notificador(IEnumerable<IPublicadorDeCambios> publicadores, IAvisos avisos)
    {
        _avisos = avisos;
        _publicadores = publicadores.ToList();
        foreach (var p in _publicadores) p.EntidadCambiada += Publicador_EntidadCambiada;
    }

    private void Publicador_EntidadCambiada(object sender, EntidadCambiadaEventArgs e) => _avisos.Exito(Mensajes.Cambio(e));

    /// <summary>Se da de baja de todos los eventos (par obligatorio del <c>+=</c>).</summary>
    public void Dispose()
    {
        foreach (var p in _publicadores) p.EntidadCambiada -= Publicador_EntidadCambiada;
    }
}
