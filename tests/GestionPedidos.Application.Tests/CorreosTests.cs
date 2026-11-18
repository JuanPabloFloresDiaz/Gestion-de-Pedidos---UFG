using GestionPedidos.Application.Correo;
using GestionPedidos.Application.Eventos;

namespace GestionPedidos.Application.Tests;

public class CorreosTests
{
    private sealed class ColaFake : IColaDeCorreos
    {
        public List<CorreoMensaje> Mensajes { get; } = new();
        public void Encolar(CorreoMensaje mensaje) => Mensajes.Add(mensaje);
    }

    private static UsuarioDadoDeBajaEventArgs Baja(string motivo = "Dejó de trabajar aquí") => new()
    {
        UsuarioId = 7, NombreCompleto = "Ana Pérez", Correo = "ana@ejemplo.com", Motivo = motivo, ActorId = 1,
    };

    [Fact]
    public void BajaDeUsuario_SeEncolaAlCorreoDelUsuario_ConElMotivo()
    {
        var eventos = new EventosAplicacion();
        var cola = new ColaFake();
        using var suscriptor = new CorreoSubscriber(eventos, cola, new UrlsDelSitio("http://sitio.test/mis-pedidos"));

        eventos.OnUsuarioDadoDeBaja(Baja());

        var correo = Assert.Single(cola.Mensajes);
        Assert.Equal("ana@ejemplo.com", correo.Para);
        Assert.Contains("dada de baja", correo.Asunto);
        Assert.Contains("Ana Pérez", correo.CuerpoTexto);   // el HTML escapa los acentos como entidades (se ven igual en el correo)
        Assert.Contains("Dejó de trabajar aquí", correo.CuerpoTexto);
    }

    [Fact]
    public void BajaDeUsuario_EscapaElHtmlDelMotivoYDelNombre()
    {
        var correo = PlantillasCorreo.BajaDeUsuario("ana@ejemplo.com", "<b>Ana</b>", "<script>alert(1)</script>");

        Assert.DoesNotContain("<script>", correo.CuerpoHtml);
        Assert.DoesNotContain("<b>Ana</b>", correo.CuerpoHtml);
        Assert.Contains("&lt;script&gt;", correo.CuerpoHtml);
    }

    private static PedidoEntregadoEventArgs Entrega(string cliente = "Carlos López", string producto = "Teclado") => new()
    {
        PedidoId = 42, Cliente = cliente, Correo = "carlos@ejemplo.com", FechaPedido = new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero),
        Total = 100m, Lineas = new[] { new LineaDeComprobante(producto, 2, 50m) },
    };

    [Fact]
    public void ComprobanteDePedido_SeEncolaAlCorreoDelCliente_ConDetalleTotalYEnlace()
    {
        var eventos = new EventosAplicacion();
        var cola = new ColaFake();
        using var suscriptor = new CorreoSubscriber(eventos, cola, new UrlsDelSitio("http://sitio.test/mis-pedidos"));

        eventos.OnPedidoEntregado(Entrega());

        var correo = Assert.Single(cola.Mensajes);
        Assert.Equal("carlos@ejemplo.com", correo.Para);
        Assert.Contains("#42", correo.Asunto);
        Assert.Contains("Teclado x2", correo.CuerpoTexto);
        Assert.Contains("$100.00", correo.CuerpoTexto);
        Assert.Contains("confirma que recibiste", correo.CuerpoTexto);
        Assert.Contains("http://sitio.test/mis-pedidos", correo.CuerpoTexto);
        Assert.Contains("href=\"http://sitio.test/mis-pedidos\"", correo.CuerpoHtml);
        Assert.Contains("08/11/2026", correo.CuerpoTexto);   // fecha de El Salvador
    }

    [Fact]
    public void ComprobanteDePedido_EscapaElHtmlDelNombreYDelProducto()
    {
        var correo = PlantillasCorreo.ComprobanteDePedido(Entrega("<i>Carlos</i>", "<script>x</script>"), "http://sitio.test/mis-pedidos");

        Assert.DoesNotContain("<script>", correo.CuerpoHtml);
        Assert.DoesNotContain("<i>Carlos</i>", correo.CuerpoHtml);
    }

    [Fact]
    public void AlHacerDispose_DejaDeEscucharLosEventos()
    {
        var eventos = new EventosAplicacion();
        var cola = new ColaFake();
        var suscriptor = new CorreoSubscriber(eventos, cola, new UrlsDelSitio("http://sitio.test/mis-pedidos"));
        suscriptor.Dispose();

        eventos.OnUsuarioDadoDeBaja(Baja());
        eventos.OnPedidoEntregado(Entrega());

        Assert.Empty(cola.Mensajes);
    }
}
