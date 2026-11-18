namespace GestionPedidos.Application.Correo;

/// <summary>Un correo ya armado, listo para enviarse.</summary>
/// <param name="Para">Dirección del destinatario.</param>
/// <param name="Asunto">Asunto.</param>
/// <param name="CuerpoHtml">Cuerpo con formato (los datos del usuario ya vienen escapados).</param>
/// <param name="CuerpoTexto">Versión de texto plano, para lectores de correo sin HTML.</param>
public sealed record CorreoMensaje(string Para, string Asunto, string CuerpoHtml, string CuerpoTexto);

/// <summary>Direcciones del sitio web que los correos enlazan (se arman con la URL pública configurada).</summary>
/// <param name="MisPedidos">Página «Mis pedidos» de la tienda.</param>
public sealed record UrlsDelSitio(string MisPedidos);

/// <summary>
/// Puerto de salida de correos. Los casos de uso y suscriptores solo <b>encolan</b>: el envío real ocurre en segundo
/// plano, así que un servidor de correo caído nunca hace fallar ni demora la operación del usuario.
/// </summary>
public interface IColaDeCorreos
{
    /// <summary>Encola un correo para su envío.</summary>
    /// <param name="mensaje">Correo a enviar.</param>
    void Encolar(CorreoMensaje mensaje);
}

/// <summary>Quien envía de verdad un correo (SMTP en producción y en desarrollo, MailHog).</summary>
public interface IEnviadorDeCorreo
{
    /// <summary>Envía un correo.</summary>
    /// <param name="mensaje">Correo a enviar.</param>
    /// <param name="ct">Cancelación.</param>
    Task EnviarAsync(CorreoMensaje mensaje, CancellationToken ct);
}
