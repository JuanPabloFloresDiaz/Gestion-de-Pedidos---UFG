using System.Net;
using System.Net.Mail;
using System.Threading.Channels;
using GestionPedidos.Application.Correo;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestionPedidos.Infrastructure.Correo;

/// <summary>
/// Configuración del correo (sección <c>Correo</c>). En desarrollo apunta a MailHog (<c>Host=mailhog</c>, puerto 1025, sin
/// SSL ni credenciales); en producción basta cambiar estas variables por las de un SMTP real, sin tocar código.
/// </summary>
public sealed class OpcionesCorreo
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string Seccion = "Correo";
    /// <summary>Si es <c>false</c>, los correos se descartan (queda un aviso en el registro).</summary>
    public bool Habilitado { get; set; } = true;
    /// <summary>Servidor SMTP.</summary>
    public string Host { get; set; } = "localhost";
    /// <summary>Puerto SMTP (1025 en MailHog; 587 o 465 en un proveedor real).</summary>
    public int Puerto { get; set; } = 1025;
    /// <summary>Usuario SMTP; vacío si el servidor no pide autenticación (MailHog).</summary>
    public string? Usuario { get; set; }
    /// <summary>Clave SMTP.</summary>
    public string? Clave { get; set; }
    /// <summary>Si se usa SSL/TLS (activar con un proveedor real).</summary>
    public bool UsarSsl { get; set; }
    /// <summary>Dirección remitente.</summary>
    public string Remitente { get; set; } = "no-responder@novixtore.local";
    /// <summary>Nombre que ve el destinatario como remitente.</summary>
    public string NombreRemitente { get; set; } = "NoviXtore";
    /// <summary>Dirección pública del sitio web (sin barra final); los correos enlazan a ella.</summary>
    public string UrlSitio { get; set; } = "http://localhost:4392";
}

/// <summary>Envía correos por SMTP con <see cref="SmtpClient"/>.</summary>
public sealed class EnviadorSmtp : IEnviadorDeCorreo
{
    private readonly OpcionesCorreo _opciones;

    /// <summary>Crea el enviador con la configuración del correo.</summary>
    public EnviadorSmtp(IOptions<OpcionesCorreo> opciones) => _opciones = opciones.Value;

    /// <inheritdoc/>
    public async Task EnviarAsync(CorreoMensaje mensaje, CancellationToken ct)
    {
        using var correo = new MailMessage
        {
            From = new MailAddress(_opciones.Remitente, _opciones.NombreRemitente),
            Subject = mensaje.Asunto,
            Body = mensaje.CuerpoTexto,
            IsBodyHtml = false,
        };
        correo.To.Add(mensaje.Para);
        // Versión HTML como alternativa: los lectores que la soportan la muestran, el resto ve el texto.
        correo.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(mensaje.CuerpoHtml, null, "text/html"));

        using var smtp = new SmtpClient(_opciones.Host, _opciones.Puerto) { EnableSsl = _opciones.UsarSsl };
        if (!string.IsNullOrEmpty(_opciones.Usuario))
            smtp.Credentials = new NetworkCredential(_opciones.Usuario, _opciones.Clave);
        await smtp.SendMailAsync(correo, ct);
    }
}

/// <summary>
/// Cola de correos en segundo plano: quien publica un evento solo encola; este servicio los envía uno a uno. Si el
/// envío falla se registra y se sigue con el siguiente: un correo caído jamás revierte la operación que lo originó.
/// </summary>
public sealed class ColaDeCorreosEnSegundoPlano : BackgroundService, IColaDeCorreos
{
    private readonly Channel<CorreoMensaje> _cola = Channel.CreateUnbounded<CorreoMensaje>();
    private readonly IEnviadorDeCorreo _enviador;
    private readonly OpcionesCorreo _opciones;
    private readonly ILogger<ColaDeCorreosEnSegundoPlano> _log;

    /// <summary>Crea el servicio.</summary>
    public ColaDeCorreosEnSegundoPlano(IEnviadorDeCorreo enviador, IOptions<OpcionesCorreo> opciones,
        ILogger<ColaDeCorreosEnSegundoPlano> log)
    {
        _enviador = enviador; _opciones = opciones.Value; _log = log;
    }

    /// <inheritdoc/>
    public void Encolar(CorreoMensaje mensaje) => _cola.Writer.TryWrite(mensaje);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var mensaje in _cola.Reader.ReadAllAsync(stoppingToken))
            {
                if (!_opciones.Habilitado)
                {
                    _log.LogWarning("Correo descartado (Correo:Habilitado=false): «{Asunto}».", mensaje.Asunto);
                    continue;
                }
                try
                {
                    await _enviador.EnviarAsync(mensaje, stoppingToken);
                    _log.LogInformation("Correo enviado: «{Asunto}».", mensaje.Asunto);   // nunca se registra el destinatario ni el cuerpo
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log.LogError(ex, "No se pudo enviar el correo «{Asunto}».", mensaje.Asunto);
                }
            }
        }
        catch (OperationCanceledException) { /* apagado normal */ }
    }
}
