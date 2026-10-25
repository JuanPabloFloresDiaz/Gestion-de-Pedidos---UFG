namespace GestionPedidos.Ui.Comun.Api;

/// <summary>Error devuelto por la API (o de comunicación), con un mensaje apto para el usuario.</summary>
public sealed class ApiException : Exception
{
    /// <summary>Crea la excepción.</summary>
    /// <param name="mensajeParaUsuario">Mensaje en español, sin detalles técnicos.</param>
    /// <param name="estado">Código HTTP (0 si no hubo respuesta).</param>
    /// <param name="codigo">Código de negocio (<c>CodigosError</c>), si lo hay.</param>
    public ApiException(string mensajeParaUsuario, int estado = 0, string? codigo = null) : base(mensajeParaUsuario)
    {
        Estado = estado;
        Codigo = codigo;
    }

    /// <summary>Mensaje en español para mostrar a la persona.</summary>
    public string MensajeParaUsuario => Message;
    /// <summary>Código HTTP; 0 si no hubo respuesta del servidor.</summary>
    public int Estado { get; }
    /// <summary>Código de negocio estable, si la API lo envió.</summary>
    public string? Codigo { get; }
}
