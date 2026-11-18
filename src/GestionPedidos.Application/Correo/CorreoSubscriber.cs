using GestionPedidos.Application.Eventos;

namespace GestionPedidos.Application.Correo;

/// <summary>
/// Suscriptor de correos: escucha los eventos de aplicación que deben avisarse por correo y encola el mensaje,
/// sin que los casos de uso sepan que existe (igual que el <c>HistorialSubscriber</c>).
/// </summary>
public sealed class CorreoSubscriber : IDisposable
{
    private readonly EventosAplicacion _eventos;
    private readonly IColaDeCorreos _cola;
    private readonly UrlsDelSitio _urls;

    /// <summary>Se suscribe a los eventos (<c>+=</c>); se da de baja en <see cref="Dispose"/>.</summary>
    /// <param name="eventos">Publicadora de eventos.</param>
    /// <param name="cola">Cola donde se dejan los correos.</param>
    /// <param name="urls">Direcciones del sitio que se enlazan en los correos (por defecto, el sitio local de desarrollo).</param>
    public CorreoSubscriber(EventosAplicacion eventos, IColaDeCorreos cola, UrlsDelSitio? urls = null)
    {
        _eventos = eventos;
        _cola = cola;
        _urls = urls ?? new UrlsDelSitio("http://localhost:4392/mis-pedidos");
        _eventos.UsuarioDadoDeBaja += Eventos_UsuarioDadoDeBaja;
        _eventos.PedidoEntregado += Eventos_PedidoEntregado;
    }

    private void Eventos_PedidoEntregado(object sender, PedidoEntregadoEventArgs e) =>
        _cola.Encolar(PlantillasCorreo.ComprobanteDePedido(e, _urls.MisPedidos));

    private void Eventos_UsuarioDadoDeBaja(object sender, UsuarioDadoDeBajaEventArgs e) =>
        _cola.Encolar(PlantillasCorreo.BajaDeUsuario(e.Correo, e.NombreCompleto, e.Motivo));

    /// <summary>Baja de las suscripciones (par obligatorio del <c>+=</c>).</summary>
    public void Dispose()
    {
        _eventos.UsuarioDadoDeBaja -= Eventos_UsuarioDadoDeBaja;
        _eventos.PedidoEntregado -= Eventos_PedidoEntregado;
    }
}
