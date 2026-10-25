namespace GestionPedidos.Api.Seguridad;

/// <summary>Opciones del JWT, leídas de la sección <c>Jwt</c> (la clave llega por variable de entorno).</summary>
public sealed class OpcionesJwt
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string Seccion = "Jwt";
    /// <summary>Clave simétrica de firma (mínimo 32 caracteres = 256 bits).</summary>
    public string SigningKey { get; set; } = "";
    /// <summary>Emisor del token.</summary>
    public string Issuer { get; set; } = "GestionPedidos";
    /// <summary>Audiencia del token.</summary>
    public string Audience { get; set; } = "GestionPedidos.Clientes";
    /// <summary>Minutos de vida del token (60 por decisión del equipo).</summary>
    public int MinutosDeVida { get; set; } = 60;
}
