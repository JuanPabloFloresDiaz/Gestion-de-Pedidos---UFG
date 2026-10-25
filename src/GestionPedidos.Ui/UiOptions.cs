namespace GestionPedidos.Ui;

/// <summary>Opciones que cada hospedaje (web o escritorio) entrega a la interfaz compartida.</summary>
public sealed class UiOptions
{
    /// <summary>URL base de la API (por ejemplo <c>http://localhost:4390</c>).</summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:4390";

    /// <summary>
    /// En el escritorio solo entra el personal: se rechaza al usuario sin el permiso <c>admin.acceso</c>
    /// (por ejemplo, un cliente de la tienda) con el mensaje genérico de credenciales incorrectas.
    /// </summary>
    public bool RequiereAdminAcceso { get; set; }

    /// <summary>Indica si el hospedaje es la aplicación de escritorio (cambia la pantalla de inicio).</summary>
    public bool EsEscritorio { get; set; }
}
