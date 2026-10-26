using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Catalogo;

namespace GestionPedidos.Application.Catalogo;

/// <summary>Casos de uso de las opciones de un filtro (por ejemplo, Negro en Color).</summary>
public sealed class GestionarOpciones
{
    private readonly IClasificacionRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly EventosAplicacion _eventos;
    private readonly TimeProvider _reloj;

    /// <summary>Crea el caso de uso.</summary>
    public GestionarOpciones(IClasificacionRepository repo, IUnitOfWork uow, EventosAplicacion eventos, TimeProvider reloj)
    {
        _repo = repo; _uow = uow; _eventos = eventos; _reloj = reloj;
    }

    private static OpcionFiltroDto ADto(OpcionFiltro o) => new(o.Id, o.FiltroId, o.Valor, o.Activo, o.EliminadoEn, Convert.ToBase64String(o.RowVersion));

    /// <summary>Crea una opción dentro de un filtro activo.</summary>
    public async Task<Resultado<OpcionFiltroDto>> CrearAsync(int filtroId, GuardarOpcionSolicitud s, int actorId, CancellationToken ct)
    {
        if (Validaciones.Texto(s.Valor, "El valor", 100) is { } error)
            return Resultado<OpcionFiltroDto>.Falla(TipoError.Validacion, CodigosError.Validacion, error);
        var filtro = await _repo.ObtenerAsync<Filtro>(filtroId, ct);
        if (filtro is null) return Resultado<OpcionFiltroDto>.Falla(ClasificacionComun.NoExiste("El filtro"));
        if (!filtro.Activo) return Resultado<OpcionFiltroDto>.Falla(ClasificacionComun.Regla("El filtro está inactivo: reactívelo para añadirle opciones."));
        var valor = s.Valor.Trim();
        if (await _repo.ExisteOpcionAsync(filtroId, valor, null, ct))
            return Resultado<OpcionFiltroDto>.Falla(ClasificacionComun.Repetido("El filtro ya tiene esa opción."));

        var opcion = new OpcionFiltro { FiltroId = filtroId, Valor = valor };
        _repo.Agregar(opcion);
        await _uow.GuardarAsync(ct);
        ClasificacionComun.Publicar(_eventos, "OpcionFiltro", opcion.Id, TipoCambioServidor.Registrada, actorId,
            $"Opción «{valor}» añadida al filtro «{filtro.Nombre}»");
        return Resultado<OpcionFiltroDto>.Ok(ADto(opcion));
    }

    /// <summary>Cambia el valor de una opción activa.</summary>
    public async Task<Resultado<OpcionFiltroDto>> ActualizarAsync(int id, GuardarOpcionSolicitud s, int actorId, CancellationToken ct)
    {
        if (Validaciones.Texto(s.Valor, "El valor", 100) is { } error)
            return Resultado<OpcionFiltroDto>.Falla(TipoError.Validacion, CodigosError.Validacion, error);
        var opcion = await _repo.ObtenerAsync<OpcionFiltro>(id, ct);
        if (opcion is null) return Resultado<OpcionFiltroDto>.Falla(ClasificacionComun.NoExiste("La opción"));
        if (!opcion.Activo) return Resultado<OpcionFiltroDto>.Falla(ClasificacionComun.Inactivo("La opción"));
        var valor = s.Valor.Trim();
        if (await _repo.ExisteOpcionAsync(opcion.FiltroId, valor, id, ct))
            return Resultado<OpcionFiltroDto>.Falla(ClasificacionComun.Repetido("El filtro ya tiene esa opción."));

        var anterior = opcion.Valor;
        if (!string.IsNullOrEmpty(s.RowVersion)) _repo.FijarVersionOriginal(opcion, Convert.FromBase64String(s.RowVersion));
        opcion.Valor = valor;
        try { await _uow.GuardarAsync(ct); }
        catch (ConcurrenciaException) { return Resultado<OpcionFiltroDto>.Falla(ClasificacionComun.Version("La opción")); }

        ClasificacionComun.Publicar(_eventos, "OpcionFiltro", id, TipoCambioServidor.Actualizada, actorId,
            anterior == valor ? $"Opción «{valor}» actualizada" : $"Opción «{anterior}» actualizada; ahora es «{valor}»");
        return Resultado<OpcionFiltroDto>.Ok(ADto(opcion));
    }

    /// <summary>Da de baja (siempre se puede) o reactiva (solo si su filtro está activo).</summary>
    public async Task<Resultado<OpcionFiltroDto>> CambiarEstadoAsync(int id, bool activo, int actorId, CancellationToken ct)
    {
        var opcion = await _repo.ObtenerAsync<OpcionFiltro>(id, ct);
        if (opcion is null) return Resultado<OpcionFiltroDto>.Falla(ClasificacionComun.NoExiste("La opción"));
        if (activo && !opcion.Activo && opcion.Filtro is { Activo: false })
            return Resultado<OpcionFiltroDto>.Falla(ClasificacionComun.Regla("Su filtro está inactivo: reactívelo primero."));

        if (opcion.Activo != activo)
        {
            opcion.CambiarEstado(activo, _reloj.GetUtcNow());
            await _uow.GuardarAsync(ct);
            ClasificacionComun.Publicar(_eventos, "OpcionFiltro", id, TipoCambioServidor.EstadoCambiado, actorId,
                $"Opción «{opcion.Valor}»: {(activo ? "reactivada" : "dada de baja")}");
        }
        return Resultado<OpcionFiltroDto>.Ok(ADto(opcion));
    }

    /// <summary>Elimina definitivamente una opción inactiva que ningún producto use.</summary>
    public async Task<Resultado> EliminarAsync(int id, int actorId, CancellationToken ct)
    {
        var opcion = await _repo.ObtenerAsync<OpcionFiltro>(id, ct);
        if (opcion is null) return Resultado.Falla(TipoError.NoEncontrado, CodigosError.NoEncontrado, "La opción no existe.");
        if (opcion.Activo)
            return Resultado.Falla(TipoError.Regla, CodigosError.Regla, "Primero dé de baja la opción; solo se eliminan las inactivas.");
        if (await _repo.ContarUsosDeOpcionAsync(id, ct) > 0)
            return Resultado.Falla(TipoError.Conflicto, CodigosError.Conflicto, "Hay productos que usan esta opción: no se puede eliminar, déjela dada de baja.");

        var valor = opcion.Valor;
        _repo.Eliminar(opcion);
        await _uow.GuardarAsync(ct);
        ClasificacionComun.Publicar(_eventos, "OpcionFiltro", id, TipoCambioServidor.Eliminada, actorId, $"Opción «{valor}» eliminada definitivamente");
        return Resultado.Ok();
    }
}
