using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Contracts;

namespace GestionPedidos.Application.Catalogo;

/// <summary>Piezas compartidas por los casos de uso de la clasificación del catálogo.</summary>
internal static class ClasificacionComun
{
    /// <summary>Errores de siempre, con el nombre de lo que no se encontró o está inactivo.</summary>
    public static ErrorApp NoExiste(string que) => new(TipoError.NoEncontrado, CodigosError.NoEncontrado, $"{que} no existe.");
    public static ErrorApp Inactivo(string que) => new(TipoError.Regla, CodigosError.Regla, $"{que} está inactivo: reactívelo antes de editarlo.");
    public static ErrorApp Repetido(string que) => new(TipoError.Conflicto, CodigosError.Conflicto, que);
    public static ErrorApp Regla(string mensaje) => new(TipoError.Regla, CodigosError.Regla, mensaje);
    public static ErrorApp Version(string que) => new(TipoError.Conflicto, CodigosError.Conflicto,
        $"{que} fue modificado por otra persona. Recargue e intente de nuevo.");

    /// <summary>Texto opcional con máximo de longitud; <c>null</c> si es válido.</summary>
    public static string? Opcional(string? valor, string campo, int maximo) =>
        valor is { } v && v.Trim().Length > maximo ? $"{campo} no puede superar {maximo} caracteres." : null;

    /// <summary>Publica el evento de aplicación (solo debe llamarse tras confirmar la operación).</summary>
    public static void Publicar(EventosAplicacion eventos, string entidad, int id, string tipo, int actorId, string detalle) =>
        eventos.OnEntidadCambiada(new EntidadCambiadaServidorEventArgs
        {
            Entidad = entidad, EntidadId = id, Tipo = tipo, UsuarioId = actorId, Detalle = detalle,
        });

    /// <summary>Texto vacío → <c>null</c>.</summary>
    public static string? Limpio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
