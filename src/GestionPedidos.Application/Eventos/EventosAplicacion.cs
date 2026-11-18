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
    /// <summary>El cliente confirmó la recepción de su pedido.</summary>
    public const string Confirmada = "ConfirmarRecepcion";
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

/// <summary>Datos del evento <see cref="EventosAplicacion.UsuarioDadoDeBaja"/>.</summary>
public class UsuarioDadoDeBajaEventArgs : EventArgs
{
    /// <summary>Usuario dado de baja.</summary>
    public required int UsuarioId { get; init; }
    /// <summary>Nombre completo del usuario (para saludarlo).</summary>
    public required string NombreCompleto { get; init; }
    /// <summary>Correo al que se le avisa.</summary>
    public required string Correo { get; init; }
    /// <summary>Justificación escrita por quien dio la baja.</summary>
    public required string Motivo { get; init; }
    /// <summary>Quien dio la baja.</summary>
    public int? ActorId { get; init; }
}

/// <summary>Una línea del comprobante de un pedido.</summary>
/// <param name="Producto">Nombre del producto.</param>
/// <param name="Cantidad">Unidades.</param>
/// <param name="PrecioUnitario">Precio por unidad al hacer el pedido.</param>
public sealed record LineaDeComprobante(string Producto, int Cantidad, decimal PrecioUnitario);

/// <summary>Datos del evento <see cref="EventosAplicacion.PedidoEntregado"/>.</summary>
public class PedidoEntregadoEventArgs : EventArgs
{
    /// <summary>Número del pedido.</summary>
    public required int PedidoId { get; init; }
    /// <summary>Nombre del cliente.</summary>
    public required string Cliente { get; init; }
    /// <summary>Correo del cliente (el comprobante solo se envía si lo tiene).</summary>
    public required string Correo { get; init; }
    /// <summary>Fecha en que se hizo el pedido.</summary>
    public required DateTimeOffset FechaPedido { get; init; }
    /// <summary>Total del pedido.</summary>
    public required decimal Total { get; init; }
    /// <summary>Líneas del pedido.</summary>
    public required IReadOnlyList<LineaDeComprobante> Lineas { get; init; }
}

/// <summary>Firma de los manejadores de <see cref="EventosAplicacion.PedidoEntregado"/>.</summary>
/// <param name="sender">Publicadora.</param>
/// <param name="e">Datos del pedido entregado.</param>
public delegate void PedidoEntregadoEventHandler(object sender, PedidoEntregadoEventArgs e);

/// <summary>Firma de los manejadores de <see cref="EventosAplicacion.UsuarioDadoDeBaja"/>.</summary>
/// <param name="sender">Publicadora.</param>
/// <param name="e">Datos de la baja.</param>
public delegate void UsuarioDadoDeBajaEventHandler(object sender, UsuarioDadoDeBajaEventArgs e);

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

    /// <summary>Se produce tras confirmar la baja de un usuario (el correo de aviso es un suscriptor).</summary>
    public event UsuarioDadoDeBajaEventHandler? UsuarioDadoDeBaja;

    /// <summary>Se produce tras marcar un pedido como entregado (el comprobante por correo es un suscriptor).</summary>
    public event PedidoEntregadoEventHandler? PedidoEntregado;

    /// <summary>Dispara <see cref="PedidoEntregado"/> si hay suscriptores.</summary>
    public void OnPedidoEntregado(PedidoEntregadoEventArgs e) => PedidoEntregado?.Invoke(this, e);

    /// <summary>Dispara <see cref="UsuarioDadoDeBaja"/> si hay suscriptores.</summary>
    public void OnUsuarioDadoDeBaja(UsuarioDadoDeBajaEventArgs e) => UsuarioDadoDeBaja?.Invoke(this, e);

    /// <summary>Dispara <see cref="EntidadCambiada"/> si hay suscriptores.</summary>
    public void OnEntidadCambiada(EntidadCambiadaServidorEventArgs e) => EntidadCambiada?.Invoke(this, e);

    /// <summary>Dispara <see cref="AccesoRegistrado"/> si hay suscriptores.</summary>
    public void OnAccesoRegistrado(AccesoRegistradoEventArgs e) => AccesoRegistrado?.Invoke(this, e);
}
