using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Catalogo;

namespace GestionPedidos.Application.Catalogo;

/// <summary>Casos de uso de las subcategorías (mismas reglas de baja suave y eliminación que las categorías).</summary>
public sealed class GestionarSubcategorias
{
    private readonly IClasificacionRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly EventosAplicacion _eventos;
    private readonly TimeProvider _reloj;

    /// <summary>Crea el caso de uso.</summary>
    public GestionarSubcategorias(IClasificacionRepository repo, IUnitOfWork uow, EventosAplicacion eventos, TimeProvider reloj)
    {
        _repo = repo; _uow = uow; _eventos = eventos; _reloj = reloj;
    }

    private async Task<SubcategoriaDto> ADtoAsync(Subcategoria s, CancellationToken ct) => new(s.Id, s.CategoriaId, s.Nombre,
        s.Descripcion, s.Activo, s.EliminadoEn, Convert.ToBase64String(s.RowVersion),
        await _repo.ContarFiltrosAsync(s.Id, ct), await _repo.ContarProductosAsync(s.Id, soloActivos: true, ct));

    private static string? Validar(GuardarClasificacionSolicitud s) =>
        Validaciones.Texto(s.Nombre, "El nombre", 100) ?? ClasificacionComun.Opcional(s.Descripcion, "La descripción", 300);

    /// <summary>Crea una subcategoría dentro de una categoría activa.</summary>
    public async Task<Resultado<SubcategoriaDto>> CrearAsync(int categoriaId, GuardarClasificacionSolicitud s, int actorId, CancellationToken ct)
    {
        if (Validar(s) is { } error) return Resultado<SubcategoriaDto>.Falla(TipoError.Validacion, CodigosError.Validacion, error);
        var categoria = await _repo.ObtenerAsync<Categoria>(categoriaId, ct);
        if (categoria is null) return Resultado<SubcategoriaDto>.Falla(ClasificacionComun.NoExiste("La categoría"));
        if (!categoria.Activo) return Resultado<SubcategoriaDto>.Falla(ClasificacionComun.Regla("La categoría está inactiva: reactívela para añadirle subcategorías."));
        var nombre = s.Nombre.Trim();
        if (await _repo.ExisteSubcategoriaAsync(categoriaId, nombre, null, ct))
            return Resultado<SubcategoriaDto>.Falla(ClasificacionComun.Repetido("La categoría ya tiene una subcategoría con ese nombre."));

        var sub = new Subcategoria { CategoriaId = categoriaId, Nombre = nombre, Descripcion = ClasificacionComun.Limpio(s.Descripcion) };
        _repo.Agregar(sub);
        await _uow.GuardarAsync(ct);
        ClasificacionComun.Publicar(_eventos, "Subcategoria", sub.Id, TipoCambioServidor.Registrada, actorId,
            $"Subcategoría «{nombre}» creada en la categoría «{categoria.Nombre}»");
        return Resultado<SubcategoriaDto>.Ok(await ADtoAsync(sub, ct));
    }

    /// <summary>Edita una subcategoría activa.</summary>
    public async Task<Resultado<SubcategoriaDto>> ActualizarAsync(int id, GuardarClasificacionSolicitud s, int actorId, CancellationToken ct)
    {
        if (Validar(s) is { } error) return Resultado<SubcategoriaDto>.Falla(TipoError.Validacion, CodigosError.Validacion, error);
        var sub = await _repo.ObtenerAsync<Subcategoria>(id, ct);
        if (sub is null) return Resultado<SubcategoriaDto>.Falla(ClasificacionComun.NoExiste("La subcategoría"));
        if (!sub.Activo) return Resultado<SubcategoriaDto>.Falla(ClasificacionComun.Inactivo("La subcategoría"));
        var nombre = s.Nombre.Trim();
        if (await _repo.ExisteSubcategoriaAsync(sub.CategoriaId, nombre, id, ct))
            return Resultado<SubcategoriaDto>.Falla(ClasificacionComun.Repetido("La categoría ya tiene una subcategoría con ese nombre."));

        var anterior = sub.Nombre;
        if (!string.IsNullOrEmpty(s.RowVersion)) _repo.FijarVersionOriginal(sub, Convert.FromBase64String(s.RowVersion));
        sub.Nombre = nombre;
        sub.Descripcion = ClasificacionComun.Limpio(s.Descripcion);
        try { await _uow.GuardarAsync(ct); }
        catch (ConcurrenciaException) { return Resultado<SubcategoriaDto>.Falla(ClasificacionComun.Version("La subcategoría")); }

        ClasificacionComun.Publicar(_eventos, "Subcategoria", id, TipoCambioServidor.Actualizada, actorId,
            anterior == nombre ? $"Subcategoría «{nombre}» actualizada" : $"Subcategoría «{anterior}» actualizada; ahora se llama «{nombre}»");
        return Resultado<SubcategoriaDto>.Ok(await ADtoAsync(sub, ct));
    }

    /// <summary>Da de baja (no si tiene productos activos) o reactiva (solo si su categoría está activa).</summary>
    public async Task<Resultado<SubcategoriaDto>> CambiarEstadoAsync(int id, bool activo, int actorId, CancellationToken ct)
    {
        var sub = await _repo.ObtenerAsync<Subcategoria>(id, ct);
        if (sub is null) return Resultado<SubcategoriaDto>.Falla(ClasificacionComun.NoExiste("La subcategoría"));
        if (!activo && sub.Activo && await _repo.ContarProductosAsync(id, soloActivos: true, ct) > 0)
            return Resultado<SubcategoriaDto>.Falla(ClasificacionComun.Regla("La subcategoría tiene productos activos: cámbieles la clasificación o désels de baja primero."));
        if (activo && !sub.Activo && sub.Categoria is { Activo: false })
            return Resultado<SubcategoriaDto>.Falla(ClasificacionComun.Regla("Su categoría está inactiva: reactívela primero."));

        if (sub.Activo != activo)
        {
            sub.CambiarEstado(activo, _reloj.GetUtcNow());
            await _uow.GuardarAsync(ct);
            ClasificacionComun.Publicar(_eventos, "Subcategoria", id, TipoCambioServidor.EstadoCambiado, actorId,
                $"Subcategoría «{sub.Nombre}»: {(activo ? "reactivada" : "dada de baja")}");
        }
        return Resultado<SubcategoriaDto>.Ok(await ADtoAsync(sub, ct));
    }

    /// <summary>Elimina definitivamente una subcategoría inactiva, sin filtros ni productos.</summary>
    public async Task<Resultado> EliminarAsync(int id, int actorId, CancellationToken ct)
    {
        var sub = await _repo.ObtenerAsync<Subcategoria>(id, ct);
        if (sub is null) return Resultado.Falla(TipoError.NoEncontrado, CodigosError.NoEncontrado, "La subcategoría no existe.");
        if (sub.Activo)
            return Resultado.Falla(TipoError.Regla, CodigosError.Regla, "Primero dé de baja la subcategoría; solo se eliminan las inactivas.");
        if (await _repo.ContarProductosAsync(id, soloActivos: false, ct) > 0)
            return Resultado.Falla(TipoError.Conflicto, CodigosError.Conflicto, "Hay productos clasificados en esta subcategoría: no se puede eliminar, déjela dada de baja.");
        if (await _repo.ContarFiltrosAsync(id, ct) > 0)
            return Resultado.Falla(TipoError.Conflicto, CodigosError.Conflicto, "La subcategoría todavía tiene filtros: elimínelos primero o déjela dada de baja.");

        var nombre = sub.Nombre;
        _repo.Eliminar(sub);
        await _uow.GuardarAsync(ct);
        ClasificacionComun.Publicar(_eventos, "Subcategoria", id, TipoCambioServidor.Eliminada, actorId, $"Subcategoría «{nombre}» eliminada definitivamente");
        return Resultado.Ok();
    }
}
