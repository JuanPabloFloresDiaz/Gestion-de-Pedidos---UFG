using System.Net;
using GestionPedidos.Application.Eventos;

namespace GestionPedidos.Application.Correo;

/// <summary>
/// Plantillas de los correos de la aplicación. Todo dato que escribe una persona (nombre, motivo) se escapa para HTML
/// antes de incrustarlo, de modo que no se pueda inyectar contenido en el correo.
/// </summary>
public static class PlantillasCorreo
{
    /// <summary>Nombre de la aplicación que firma los correos.</summary>
    public const string NombreApp = "NoviXtore";

    /// <summary>Aviso a un usuario de que su cuenta fue dada de baja, con la justificación que escribió quien la dio.</summary>
    /// <param name="correo">Correo del usuario.</param>
    /// <param name="nombreCompleto">Nombre del usuario.</param>
    /// <param name="motivo">Justificación de la baja.</param>
    public static CorreoMensaje BajaDeUsuario(string correo, string nombreCompleto, string motivo)
    {
        var html = Marco($"""
            <p>Hola {Esc(nombreCompleto)},</p>
            <p>Tu cuenta de {NombreApp} fue <strong>dada de baja</strong> y ya no podrás iniciar sesión.</p>
            <p><strong>Motivo:</strong></p>
            <blockquote>{Esc(motivo)}</blockquote>
            <p>Si crees que se trata de un error, comunícate con el administrador del sistema.</p>
            """);
        var texto = $"Hola {nombreCompleto},\n\nTu cuenta de {NombreApp} fue dada de baja y ya no podrás iniciar sesión.\n\n" +
                    $"Motivo: {motivo}\n\nSi crees que se trata de un error, comunícate con el administrador del sistema.";
        return new CorreoMensaje(correo, $"{NombreApp}: tu cuenta fue dada de baja", html, texto);
    }

    /// <summary>
    /// Comprobante de un pedido entregado: detalle y total, más la invitación a entrar al sitio y confirmar que lo recibió.
    /// </summary>
    /// <param name="e">Datos del pedido entregado.</param>
    /// <param name="urlMisPedidos">Enlace a «Mis pedidos» en el sitio.</param>
    public static CorreoMensaje ComprobanteDePedido(PedidoEntregadoEventArgs e, string urlMisPedidos)
    {
        var cultura = System.Globalization.CultureInfo.GetCultureInfo("es-SV");
        var filasHtml = string.Concat(e.Lineas.Select(l =>
            $"<tr><td style=\"padding:4px 8px\">{Esc(l.Producto)}</td><td style=\"padding:4px 8px;text-align:right\">{l.Cantidad}</td>" +
            $"<td style=\"padding:4px 8px;text-align:right\">{Dinero(l.PrecioUnitario, cultura)}</td>" +
            $"<td style=\"padding:4px 8px;text-align:right\">{Dinero(l.PrecioUnitario * l.Cantidad, cultura)}</td></tr>"));
        var fecha = e.FechaPedido.ToOffset(TimeSpan.FromHours(-6)).ToString("dd/MM/yyyy", cultura);   // fecha de El Salvador (UTC−6)
        var html = Marco($"""
            <p>Hola {Esc(e.Cliente)},</p>
            <p>Tu pedido <strong>#{e.PedidoId}</strong>, hecho el {fecha}, fue <strong>entregado</strong>. Este es tu comprobante:</p>
            <table style="border-collapse:collapse;width:100%;border:1px solid #ddd">
              <tr style="background:#f3f3f3"><th style="padding:4px 8px;text-align:left">Producto</th><th style="padding:4px 8px;text-align:right">Cant.</th><th style="padding:4px 8px;text-align:right">Precio</th><th style="padding:4px 8px;text-align:right">Subtotal</th></tr>
              {filasHtml}
              <tr><td colspan="3" style="padding:6px 8px;text-align:right"><strong>Total</strong></td><td style="padding:6px 8px;text-align:right"><strong>{Dinero(e.Total, cultura)}</strong></td></tr>
            </table>
            <p>Entra al sitio y <strong>confirma que recibiste tu pedido</strong>:</p>
            <p><a href="{Esc(urlMisPedidos)}">Ir a Mis pedidos</a></p>
            """);
        var lineasTexto = string.Join("\n", e.Lineas.Select(l => $"  - {l.Producto} x{l.Cantidad}: {Dinero(l.PrecioUnitario * l.Cantidad, cultura)}"));
        var texto = $"Hola {e.Cliente},\n\nTu pedido #{e.PedidoId}, hecho el {fecha}, fue entregado. Comprobante:\n{lineasTexto}\n" +
                    $"Total: {Dinero(e.Total, cultura)}\n\nEntra al sitio y confirma que recibiste tu pedido: {urlMisPedidos}";
        return new CorreoMensaje(e.Correo, $"{NombreApp}: comprobante de tu pedido #{e.PedidoId}", html, texto);
    }

    private static string Dinero(decimal valor, System.Globalization.CultureInfo cultura) => "$" + valor.ToString("N2", cultura);

    private static string Esc(string texto) => WebUtility.HtmlEncode(texto);

    private static string Marco(string contenido) =>
        $"<div style=\"font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:auto;color:#1a1a1a\">{contenido}" +
        $"<hr style=\"border:none;border-top:1px solid #ddd\"><p style=\"color:#666;font-size:12px\">{NombreApp} · mensaje automático, no respondas a este correo.</p></div>";
}
