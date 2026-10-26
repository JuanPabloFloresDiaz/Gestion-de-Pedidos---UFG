using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Catalogo;

namespace GestionPedidos.Application.Catalogo;

/// <summary>Casos de uso de los filtros de una subcategoría (por ejemplo, Color o Talla).</summary>
public sealed class GestionarFiltros
{
    private readonly IClasificacionRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly EventosAplicacion _eventos;
    private readonly TimeProvider _reloj;

    /// <summary>Crea el caso de uso.</summary>
    public GestionarFiltros(IClasificacionRepository repo, IUnitOfWork uow, EventosAplicacion eventos, TimeProvider reloj)
    {
        _repo = repo; _uow = uow; _eventos = eventos; _reloj = reloj;
    }

    private static FiltroDto ADto(Filtro f) => new(f.Id, f.SubcategoriaId, f.Nombre, f.Activo, f.EliminadoEn,
        Convert.ToBase64String(f.RowVersion), Array.Empty<OpcionFiltroDto>());

    /// <summary>Crea un filtro dentro de una subcategoría activa.</summary>
    public async Task<Resultado<FiltroDto>> CrearAsync(int subcategoriaId, GuardarFiltroSolicitud s, int actorId, CancellationToken ct)
    {
        if (Validaciones.Texto(s.Nombre, "El nombre", 100) is { } error)
            return Resultado<FiltroDto>.Falla(TipoError.Validacion, CodigosError.Validacion, error);
        var sub = await _repo.ObtenerAsync<Subcategoria>(subcategoriaId, ct);
        if (sub is null) return Resultado<FiltroDto>.Falla(ClasificacionComun.NoExiste("La subcategoría"));
        if (!sub.Activo) return Resultado<FiltroDto>.Falla(ClasificacionComun.Regla("La subcategoría está inactiva: reactívela para añadirle filtros."));
        var nombre = s.Nombre.Trim();
        if (await _repo.ExisteFiltroAsync(subcategoriaId, nombre, null, ct))
            return Resultado<FiltroDto>.Falla(ClasificacionComun.Repetido("La subcategoría ya tiene un filtro con ese nombre."));

        var filtro = new Filtro { SubcategoriaId = subcategoriaId, Nombre = nombre };
        _repo.Agregar(filtro);
        await _uow.GuardarAsync(ct);
        ClasificacionComun.Publicar(_eventos, "Filtro", filtro.Id, TipoCambioServidor.Registrada, actorId,
            $"Filtro «{nombre}» creado en la subcategoría «{sub.Nombre}»");
        return Resultado<FiltroDto>.Ok(ADto(filtro));
    }

    /// <summary>Renombra un filtro activo.</summary>
    public async Task<Resultado<FiltroDto>> ActualizarAsync(int id, GuardarFiltroSolicitud s, int actorId, CancellationToken ct)
    {
        if (Validaciones.Texto(s.Nombre, "El nombre", 100) is { } error)
            return Resultado<FiltroDto>.Falla(TipoError.Validacion, CodigosError.Validacion, error);
        var filtro = await _repo.ObtenerAsync<Filtro>(id, ct);
        if (filtro is null) return Resultado<FiltroDto>.Falla(ClasificacionComun.NoExiste("El filtro"));
        if (!filtro.Activo) return Resultado<FiltroDto>.Falla(ClasificacionComun.Inactivo("El filtro"));
        var nombre = s.Nombre.Trim();
        if (await _repo.ExisteFiltroAsync(filtro.SubcategoriaId, nombre, id, ct))
            return Resultado<FiltroDto>.Falla(ClasificacionComun.Repetido("La subcategoría ya tiene un filtro con ese nombre."));

        var anterior = filtro.Nombre;
        if (!string.IsNullOrEmpty(s.RowVersion)) _repo.FijarVersionOriginal(filtro, Convert.FromBase64String(s.RowVersion));
        filtro.Nombre = nombre;
        try { await _uow.GuardarAsync(ct); }
        catch (ConcurrenciaException) { return Resultado<FiltroDto>.Falla(ClasificacionComun.Version("El filtro")); }

        ClasificacionComun.Publicar(_eventos, "Filtro", id, TipoCambioServidor.Actualizada, actorId,
            anterior == nombre ? $"Filtro «{nombre}» actualizado" : $"Filtro «{anterior}» actualizado; ahora se llama «{nombre}»");
        return Resultado<FiltroDto>.Ok(ADto(filtro));
    }

    /// <summary>Da de baja (siempre se puede) o reactiva (solo si su subcategoría está activa).</summary>
    public async Task<Resultado<FiltroDto>> CambiarEstadoAsync(int id, bool activo, int actorId, CancellationToken ct)
    {
        var filtro = await _repo.ObtenerAsync<Filtro>(id, ct);
        if (filtro is null) return Resultado<FiltroDto>.Falla(ClasificacionComun.NoExiste("El filtro"));
        if (activo && !filtro.Activo && filtro.Subcategoria is { Activo: false })
            return Resultado<FiltroDto>.Falla(ClasificacionComun.Regla("Su subcategoría está inactiva: reactívela primero."));

        if (filtro.Activo != activo)
        {
            filtro.CambiarEstado(activo, _reloj.GetUtcNow());
            await _uow.GuardarAsync(ct);
            ClasificacionComun.Publicar(_eventos, "Filtro", id, TipoCambioServidor.EstadoCambiado, actorId,
                $"Filtro «{filtro.Nombre}»: {(activo ? "reactivado" : "dado de baja")}");
        }
        return Resultado<FiltroDto>.Ok(ADto(filtro));
    }

    /// <summary>Elimina definitivamente un filtro inactivo y sin opciones.</summary>
    public async Task<Resultado> EliminarAsync(int id, int actorId, CancellationToken ct)
    {
        var filtro = await _repo.ObtenerAsync<Filtro>(id, ct);
        if (filtro is null) return Resultado.Falla(TipoError.NoEncontrado, CodigosError.NoEncontrado, "El filtro no existe.");
        if (filtro.Activo)
            return Resultado.Falla(TipoError.Regla, CodigosError.Regla, "Primero dé de baja el filtro; solo se eliminan los inactivos.");
        if (await _repo.ContarOpcionesAsync(id, ct) > 0)
            return Resultado.Falla(TipoError.Conflicto, CodigosError.Conflicto, "El filtro todavía tiene opciones: elimínelas primero o déjelo dado de baja.");

        var nombre = filtro.Nombre;
        _repo.Eliminar(filtro);
        await _uow.GuardarAsync(ct);
        ClasificacionComun.Publicar(_eventos, "Filtro", id, TipoCambioServidor.Eliminada, actorId, $"Filtro «{nombre}» eliminado definitivamente");
        return Resultado.Ok();
    }
}
