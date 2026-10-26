using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Catalogo;

namespace GestionPedidos.Application.Catalogo;

/// <summary>
/// Casos de uso de las categorías: crear, editar, dar de baja (suave) o reactivar y eliminar definitivamente.
/// Cada operación publica su evento solo después de confirmar la base de datos.
/// </summary>
public sealed class GestionarCategorias
{
    private readonly IClasificacionRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly EventosAplicacion _eventos;
    private readonly TimeProvider _reloj;

    /// <summary>Crea el caso de uso.</summary>
    public GestionarCategorias(IClasificacionRepository repo, IUnitOfWork uow, EventosAplicacion eventos, TimeProvider reloj)
    {
        _repo = repo; _uow = uow; _eventos = eventos; _reloj = reloj;
    }

    private static CategoriaDto ADto(Categoria c) => new(c.Id, c.Nombre, c.Descripcion, c.Activo, c.EliminadoEn,
        Convert.ToBase64String(c.RowVersion), Array.Empty<SubcategoriaDto>());

    private static string? Validar(GuardarClasificacionSolicitud s) =>
        Validaciones.Texto(s.Nombre, "El nombre", 100) ?? ClasificacionComun.Opcional(s.Descripcion, "La descripción", 300);

    /// <summary>Crea una categoría.</summary>
    public async Task<Resultado<CategoriaDto>> CrearAsync(GuardarClasificacionSolicitud s, int actorId, CancellationToken ct)
    {
        if (Validar(s) is { } error) return Resultado<CategoriaDto>.Falla(TipoError.Validacion, CodigosError.Validacion, error);
        var nombre = s.Nombre.Trim();
        if (await _repo.ExisteCategoriaAsync(nombre, null, ct))
            return Resultado<CategoriaDto>.Falla(ClasificacionComun.Repetido("Ya existe una categoría con ese nombre."));

        var categoria = new Categoria { Nombre = nombre, Descripcion = ClasificacionComun.Limpio(s.Descripcion) };
        _repo.Agregar(categoria);
        await _uow.GuardarAsync(ct);
        ClasificacionComun.Publicar(_eventos, "Categoria", categoria.Id, TipoCambioServidor.Registrada, actorId, $"Categoría «{nombre}» creada");
        return Resultado<CategoriaDto>.Ok(ADto(categoria));
    }

    /// <summary>Edita una categoría activa.</summary>
    public async Task<Resultado<CategoriaDto>> ActualizarAsync(int id, GuardarClasificacionSolicitud s, int actorId, CancellationToken ct)
    {
        if (Validar(s) is { } error) return Resultado<CategoriaDto>.Falla(TipoError.Validacion, CodigosError.Validacion, error);
        var categoria = await _repo.ObtenerAsync<Categoria>(id, ct);
        if (categoria is null) return Resultado<CategoriaDto>.Falla(ClasificacionComun.NoExiste("La categoría"));
        if (!categoria.Activo) return Resultado<CategoriaDto>.Falla(ClasificacionComun.Inactivo("La categoría"));
        var nombre = s.Nombre.Trim();
        if (await _repo.ExisteCategoriaAsync(nombre, id, ct))
            return Resultado<CategoriaDto>.Falla(ClasificacionComun.Repetido("Ya existe una categoría con ese nombre."));

        var anterior = categoria.Nombre;
        if (!string.IsNullOrEmpty(s.RowVersion)) _repo.FijarVersionOriginal(categoria, Convert.FromBase64String(s.RowVersion));
        categoria.Nombre = nombre;
        categoria.Descripcion = ClasificacionComun.Limpio(s.Descripcion);
        try { await _uow.GuardarAsync(ct); }
        catch (ConcurrenciaException) { return Resultado<CategoriaDto>.Falla(ClasificacionComun.Version("La categoría")); }

        ClasificacionComun.Publicar(_eventos, "Categoria", id, TipoCambioServidor.Actualizada, actorId,
            anterior == nombre ? $"Categoría «{nombre}» actualizada" : $"Categoría «{anterior}» actualizada; ahora se llama «{nombre}»");
        return Resultado<CategoriaDto>.Ok(ADto(categoria));
    }

    /// <summary>Da de baja (no si tiene subcategorías activas) o reactiva.</summary>
    public async Task<Resultado<CategoriaDto>> CambiarEstadoAsync(int id, bool activo, int actorId, CancellationToken ct)
    {
        var categoria = await _repo.ObtenerAsync<Categoria>(id, ct);
        if (categoria is null) return Resultado<CategoriaDto>.Falla(ClasificacionComun.NoExiste("La categoría"));
        if (!activo && categoria.Activo && await _repo.ContarSubcategoriasAsync(id, soloActivas: true, ct) > 0)
            return Resultado<CategoriaDto>.Falla(ClasificacionComun.Regla(
                "La categoría tiene subcategorías activas: dé de baja primero sus subcategorías."));

        if (categoria.Activo != activo)
        {
            categoria.CambiarEstado(activo, _reloj.GetUtcNow());
            await _uow.GuardarAsync(ct);
            ClasificacionComun.Publicar(_eventos, "Categoria", id, TipoCambioServidor.EstadoCambiado, actorId,
                $"Categoría «{categoria.Nombre}»: {(activo ? "reactivada" : "dada de baja")}");
        }
        return Resultado<CategoriaDto>.Ok(ADto(categoria));
    }

    /// <summary>Elimina definitivamente una categoría inactiva y sin subcategorías.</summary>
    public async Task<Resultado> EliminarAsync(int id, int actorId, CancellationToken ct)
    {
        var categoria = await _repo.ObtenerAsync<Categoria>(id, ct);
        if (categoria is null) return Resultado.Falla(ClasificacionComun.NoExiste("La categoría").Tipo, CodigosError.NoEncontrado, "La categoría no existe.");
        if (categoria.Activo)
            return Resultado.Falla(TipoError.Regla, CodigosError.Regla, "Primero dé de baja la categoría; solo se eliminan las inactivas.");
        if (await _repo.ContarSubcategoriasAsync(id, soloActivas: false, ct) > 0)
            return Resultado.Falla(TipoError.Conflicto, CodigosError.Conflicto, "La categoría todavía tiene subcategorías: elimínelas primero o déjela dada de baja.");

        var nombre = categoria.Nombre;
        _repo.Eliminar(categoria);
        await _uow.GuardarAsync(ct);
        ClasificacionComun.Publicar(_eventos, "Categoria", id, TipoCambioServidor.Eliminada, actorId, $"Categoría «{nombre}» eliminada definitivamente");
        return Resultado.Ok();
    }
}
