using GestionPedidos.Application.Common;
using GestionPedidos.Contracts;

namespace GestionPedidos.Application.Catalogos;

/// <summary>Acceso de solo lectura a los catálogos geográficos.</summary>
public interface ICatalogoGeoRepository
{
    /// <summary>Todos los departamentos con su cantidad de ciudades.</summary>
    Task<IReadOnlyList<DepartamentoDto>> ListarDepartamentosAsync(CancellationToken ct);
    /// <summary>Indica si existe el departamento.</summary>
    Task<bool> ExisteDepartamentoAsync(int id, CancellationToken ct);
    /// <summary>Ciudades con filtros y paginación.</summary>
    Task<(IReadOnlyList<CiudadDto> Elementos, int Total)> BuscarCiudadesAsync(int? departamentoId, string? q, int pagina,
        int tamano, CancellationToken ct);
}

/// <summary>Caso de uso: listar los 14 departamentos.</summary>
public sealed class ListarDepartamentos
{
    private readonly ICatalogoGeoRepository _geo;

    /// <summary>Crea el caso de uso.</summary>
    public ListarDepartamentos(ICatalogoGeoRepository geo) => _geo = geo;

    /// <summary>Devuelve los departamentos.</summary>
    public Task<IReadOnlyList<DepartamentoDto>> EjecutarAsync(CancellationToken ct) => _geo.ListarDepartamentosAsync(ct);
}

/// <summary>Caso de uso: buscar ciudades (distritos) con filtros y paginación.</summary>
public sealed class ListarCiudades
{
    private readonly ICatalogoGeoRepository _geo;

    /// <summary>Crea el caso de uso.</summary>
    public ListarCiudades(ICatalogoGeoRepository geo) => _geo = geo;

    /// <summary>Devuelve una página de ciudades.</summary>
    /// <param name="departamentoId">Filtra por departamento; <c>null</c> = todos.</param>
    /// <param name="q">Texto buscado en el nombre del distrito o del municipio.</param>
    /// <param name="pagina">Página (desde 1).</param>
    /// <param name="tamano">Elementos por página (máximo 100).</param>
    /// <param name="ct">Cancelación.</param>
    public async Task<Resultado<PaginaDto<CiudadDto>>> EjecutarAsync(int? departamentoId, string? q, int? pagina, int? tamano,
        CancellationToken ct)
    {
        if (departamentoId is not null && !await _geo.ExisteDepartamentoAsync(departamentoId.Value, ct))
            return Resultado<PaginaDto<CiudadDto>>.Falla(TipoError.NoEncontrado, CodigosError.NoEncontrado, "El departamento no existe.");
        var (p, t) = Paginacion.Normalizar(pagina, tamano);
        var (elementos, total) = await _geo.BuscarCiudadesAsync(departamentoId, q?.Trim(), p, t, ct);
        return Resultado<PaginaDto<CiudadDto>>.Ok(new PaginaDto<CiudadDto>(elementos, total, p, t));
    }
}
