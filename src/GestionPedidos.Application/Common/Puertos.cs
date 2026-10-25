namespace GestionPedidos.Application.Common;

/// <summary>Unidad de trabajo: confirma todos los cambios pendientes en una sola transacción.</summary>
public interface IUnitOfWork
{
    /// <summary>Guarda los cambios.</summary>
    /// <exception cref="ConcurrenciaException">Otro usuario modificó el registro.</exception>
    Task GuardarAsync(CancellationToken ct);
}

/// <summary>Se lanza cuando el registro cambió desde que se leyó (concurrencia optimista).</summary>
public sealed class ConcurrenciaException : Exception
{
    /// <summary>Crea la excepción.</summary>
    public ConcurrenciaException(Exception? interna = null)
        : base("El registro fue modificado por otra persona. Recargue e intente de nuevo.", interna) { }
}
