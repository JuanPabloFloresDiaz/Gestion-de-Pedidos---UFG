using GestionPedidos.Ui.Comun.Api;

namespace GestionPedidos.Ui.Comun;

/// <summary>Base de los ViewModels: evento de cambio, indicador de carga y manejo uniforme de errores.</summary>
public abstract class ViewModelBase : IDisposable
{
    private CancellationTokenSource? _cts;

    /// <summary>Se produce cuando el estado cambió y la página debe repintarse.</summary>
    public event EventHandler? Cambiado;

    /// <summary>Indica si hay una operación en curso (deshabilita botones y muestra el indicador).</summary>
    public bool Cargando { get; private set; }

    /// <summary>Último error entendible por el usuario, o <c>null</c>.</summary>
    public string? Error { get; protected set; }

    /// <summary>Avisa a los suscriptores de que el estado cambió.</summary>
    protected void OnCambiado() => Cambiado?.Invoke(this, EventArgs.Empty);

    /// <summary>Ejecuta <paramref name="operacion"/> mostrando carga y traduciendo errores a mensajes.</summary>
    /// <param name="operacion">Método a ejecutar (se recibe como delegado).</param>
    /// <param name="cancelarAnterior">Si es <c>true</c>, cancela la operación previa (útil en búsquedas).</param>
    /// <returns><c>true</c> si terminó sin error.</returns>
    protected async Task<bool> EjecutarAsync(Func<CancellationToken, Task> operacion, bool cancelarAnterior = true)
    {
        if (cancelarAnterior) _cts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;
        Cargando = true; Error = null; OnCambiado();
        try { await operacion(cts.Token); return true; }
        catch (OperationCanceledException) { return false; }   // otra operación reemplazó a esta
        catch (ApiException ex) { if (!InterpretarError(ex)) Error = ex.MensajeParaUsuario; return false; }
        finally
        {
            if (_cts == cts) { Cargando = false; OnCambiado(); }
        }
    }

    /// <summary>
    /// Gancho para que un ViewModel reaccione a un código de error de negocio concreto (por ejemplo, pedir una confirmación).
    /// Devuelve <c>true</c> si ya lo manejó y dejó él mismo el mensaje en <see cref="Error"/>.
    /// </summary>
    /// <param name="ex">Error de la API.</param>
    protected virtual bool InterpretarError(ApiException ex) => false;

    /// <summary>Cancela lo pendiente al cerrar la pantalla.</summary>
    public virtual void Dispose() => _cts?.Cancel();
}
