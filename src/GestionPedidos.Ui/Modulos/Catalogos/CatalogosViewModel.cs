using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun;

namespace GestionPedidos.Ui.Modulos.Catalogos;

/// <summary>Estado y lógica de la consulta de departamentos y ciudades (solo lectura).</summary>
public sealed class CatalogosViewModel : ViewModelBase
{
    private readonly CatalogosService _catalogos;
    private int _pagina = 1;

    /// <summary>Crea el ViewModel.</summary>
    public CatalogosViewModel(CatalogosService catalogos) => _catalogos = catalogos;

    /// <summary>Departamentos.</summary>
    public IReadOnlyList<DepartamentoDto> Departamentos { get; private set; } = Array.Empty<DepartamentoDto>();
    /// <summary>Departamento elegido para filtrar (<c>null</c> = todos).</summary>
    public int? DepartamentoId { get; private set; }
    /// <summary>Texto buscado en el distrito o el municipio.</summary>
    public string Texto { get; set; } = "";
    /// <summary>Página de ciudades.</summary>
    public PaginaDto<CiudadDto>? Pagina { get; private set; }
    /// <summary>Total de ciudades del catálogo.</summary>
    public int TotalCiudades => Departamentos.Sum(d => d.TotalCiudades);

    /// <summary>Carga los departamentos y la primera página.</summary>
    public async Task IniciarAsync()
    {
        await EjecutarAsync(async ct => Departamentos = await _catalogos.DepartamentosAsync(ct));
        await BuscarAsync();
    }

    /// <summary>Elige un departamento (o todos) y busca desde la primera página.</summary>
    public Task ElegirDepartamentoAsync(int? id) { DepartamentoId = id; return BuscarAsync(); }

    /// <summary>Busca con los filtros actuales desde la primera página.</summary>
    public Task BuscarAsync() { _pagina = 1; return CargarAsync(); }

    /// <summary>Va a la página indicada.</summary>
    public Task IrAPaginaAsync(int pagina) { _pagina = pagina; return CargarAsync(); }

    /// <summary>Limpia los filtros.</summary>
    public Task LimpiarAsync() { Texto = ""; DepartamentoId = null; return BuscarAsync(); }

    private Task CargarAsync() => EjecutarAsync(async ct => Pagina = await _catalogos.CiudadesAsync(DepartamentoId, Texto, _pagina, 15, ct));
}
