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

    /// <summary>
    /// Si es <c>true</c>, las fotos (<c>/imagenes/...</c>) se descargan de la API desde C# y se muestran como <c>data:</c>.
    /// La web lo deja en <c>false</c>: su servidor las entrega bajo su propio origen. La aplicación nativa no tiene ese servidor, y
    /// pedirlas desde la página al puerto de la API (otro origen, <c>http</c> bajo una página <c>https</c>) las bloquea el WebView.
    /// </summary>
    public bool ImagenesComoDatos { get; set; }
}
