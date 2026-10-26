namespace GestionPedidos.Application.Eventos;

/// <summary>Qué le ocurrió a una entidad.</summary>
public static class TipoCambioServidor
{
    /// <summary>Alta.</summary>
    public const string Registrada = "Registrar";
    /// <summary>Edición.</summary>
    public const string Actualizada = "Actualizar";
    /// <summary>Baja lógica o reactivación.</summary>
    public const string EstadoCambiado = "CambiarEstado";
    /// <summary>Eliminación definitiva (el registro ya estaba dado de baja).</summary>
    public const string Eliminada = "Eliminar";
    /// <summary>Cambio de contraseña.</summary>
    public const string ClaveCambiada = "CambiarClave";
    /// <summary>Enlace de una cuenta web con un cliente existente.</summary>
    public const string CuentaEnlazada = "EnlazarCuenta";
}

/// <summary>Datos del evento <see cref="EventosAplicacion.EntidadCambiada"/>.</summary>
public class EntidadCambiadaServidorEventArgs : EventArgs
{
    /// <summary>Entidad afectada (<c>Usuario</c>, <c>Cliente</c>…).</summary>
    public required string Entidad { get; init; }
    /// <summary>Identificador de la entidad.</summary>
    public int? EntidadId { get; init; }
    /// <summary>Tipo de cambio (ver <see cref="TipoCambioServidor"/>).</summary>
    public required string Tipo { get; init; }
    /// <summary>Usuario que lo realizó, si se conoce.</summary>
    public int? UsuarioId { get; init; }
    /// <summary>Detalle legible, sin datos sensibles.</summary>
    public string? Detalle { get; init; }
}

/// <summary>Datos del evento <see cref="EventosAplicacion.AccesoRegistrado"/>.</summary>
public class AccesoRegistradoEventArgs : EventArgs
{
    /// <summary>Identificador usado (usuario o correo) en el intento.</summary>
    public required string Identificador { get; init; }
    /// <summary>Usuario, si existe.</summary>
    public int? UsuarioId { get; init; }
    /// <summary>Si el acceso fue correcto.</summary>
    public required bool Exitoso { get; init; }
}

/// <summary>Firma de los manejadores de <see cref="EventosAplicacion.EntidadCambiada"/>.</summary>
/// <param name="sender">Publicadora.</param>
/// <param name="e">Datos del cambio.</param>
public delegate void EntidadCambiadaServidorEventHandler(object sender, EntidadCambiadaServidorEventArgs e);

/// <summary>Firma de los manejadores de <see cref="EventosAplicacion.AccesoRegistrado"/>.</summary>
/// <param name="sender">Publicadora.</param>
/// <param name="e">Datos del intento.</param>
public delegate void AccesoRegistradoEventHandler(object sender, AccesoRegistradoEventArgs e);

/// <summary>
/// Publicadora de los eventos de aplicación (patrón delegado + evento + EventArgs de la materia).
/// Los casos de uso la invocan <b>después</b> de confirmar la operación; el historial es un suscriptor.
/// </summary>
public sealed class EventosAplicacion
{
    /// <summary>Se produce tras confirmar un alta, edición o cambio de estado.</summary>
    public event EntidadCambiadaServidorEventHandler? EntidadCambiada;

    /// <summary>Se produce en cada intento de acceso (correcto o fallido).</summary>
    public event AccesoRegistradoEventHandler? AccesoRegistrado;

    /// <summary>Dispara <see cref="EntidadCambiada"/> si hay suscriptores.</summary>
    public void OnEntidadCambiada(EntidadCambiadaServidorEventArgs e) => EntidadCambiada?.Invoke(this, e);

    /// <summary>Dispara <see cref="AccesoRegistrado"/> si hay suscriptores.</summary>
    public void OnAccesoRegistrado(AccesoRegistradoEventArgs e) => AccesoRegistrado?.Invoke(this, e);
}
