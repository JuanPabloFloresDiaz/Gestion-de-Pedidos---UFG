using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun;

namespace GestionPedidos.Ui.Modulos.Historial;

/// <summary>Estado y lógica de la consulta del historial de actividades (solo lectura).</summary>
public sealed class HistorialViewModel : ViewModelBase
{
    private readonly HistorialService _historial;
    private int _pagina = 1;

    /// <summary>Crea el ViewModel.</summary>
    public HistorialViewModel(HistorialService historial) => _historial = historial;

    /// <summary>Texto buscado en el detalle.</summary>
    public string Texto { get; set; } = "";
    /// <summary>Entidad elegida (vacío = todas).</summary>
    public string Entidad { get; set; } = "";
    /// <summary>Acción elegida (vacío = todas).</summary>
    public string Accion { get; set; } = "";
    /// <summary>Fecha inicial.</summary>
    public DateOnly? Desde { get; set; }
    /// <summary>Fecha final.</summary>
    public DateOnly? Hasta { get; set; }
    /// <summary>Entidades disponibles para el filtro.</summary>
    public IReadOnlyList<string> Entidades { get; private set; } = Array.Empty<string>();
    /// <summary>Acciones disponibles para el filtro.</summary>
    public IReadOnlyList<string> Acciones { get; private set; } = Array.Empty<string>();
    /// <summary>Página de resultados.</summary>
    public PaginaDto<ActividadDto>? Pagina { get; private set; }

    /// <summary>Error de validación de las fechas, o <c>null</c>.</summary>
    public string? ErrorFechas => Desde is not null && Hasta is not null && Desde > Hasta
        ? "La fecha inicial no puede ser posterior a la final." : null;

    /// <summary>Carga los filtros y la primera página.</summary>
    public async Task IniciarAsync()
    {
        await EjecutarAsync(async ct =>
        {
            var f = await _historial.FiltrosAsync(ct);
            Entidades = f.Entidades; Acciones = f.Acciones;
        });
        await BuscarAsync();
    }

    /// <summary>Busca con los filtros actuales desde la primera página.</summary>
    public Task BuscarAsync()
    {
        if (ErrorFechas is not null) { OnCambiado(); return Task.CompletedTask; }
        _pagina = 1;
        return CargarAsync();
    }

    /// <summary>Va a la página indicada.</summary>
    public Task IrAPaginaAsync(int pagina) { _pagina = pagina; return CargarAsync(); }

    /// <summary>Limpia los filtros.</summary>
    public Task LimpiarAsync()
    {
        Texto = Entidad = Accion = "";
        Desde = Hasta = null;
        return BuscarAsync();
    }

    /// <summary>Vuelve a consultar la página actual (para ver las acciones más recientes).</summary>
    public Task RecargarAsync() => CargarAsync();

    private Task CargarAsync() => EjecutarAsync(async ct =>
        Pagina = await _historial.BuscarAsync(Texto, Entidad, Accion, Desde, Hasta, _pagina, 20, ct));
}
