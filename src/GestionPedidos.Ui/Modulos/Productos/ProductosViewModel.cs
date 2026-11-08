using System.Globalization;
using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun;
using GestionPedidos.Ui.Comun.Eventos;

namespace GestionPedidos.Ui.Modulos.Productos;

/// <summary>Acción que espera confirmación del usuario para productos.</summary>
public enum AccionProducto
{
    /// <summary>Ninguna acción pendiente.</summary>
    Ninguna,
    /// <summary>Dar de baja lógica al producto.</summary>
    DarDeBaja,
    /// <summary>Reactivar un producto inactivo.</summary>
    Reactivar,
}

/// <summary>
/// Estado y lógica de la pantalla de productos: catálogo con filtros, indicador de stock bajo,
/// panel lateral de alta y edición, clasificación (subcategorías y filtros del Bloque K) y baja suave.
/// </summary>
public sealed class ProductosViewModel : ViewModelBase
{
    public const int UmbralStockBajo = 5;
    private readonly ProductosService _servicio;
    private int _pagina = 1;

    /// <summary>Crea el ViewModel y se suscribe al evento de cambio de entidades para refrescar.</summary>
    /// <param name="servicio">Servicio de productos.</param>
    public ProductosViewModel(ProductosService servicio)
    {
        _servicio = servicio;
        _servicio.EntidadCambiada += Productos_EntidadCambiada;
    }

   /// <summary>Indica si el producto tiene pocas existencias.</summary>
    public bool EsStockBajo(ProductoDto p) => p.CantidadStock < UmbralStockBajo;

    public string TextoBusqueda { get; set; } = "";
    public int? FiltroCategoriaId { get; set; }
    public int? FiltroSubcategoriaId { get; set; }
    public bool? FiltroActivo { get; set; } = true;

    public PaginaDto<ProductoDto>? Pagina { get; private set; }
    public IReadOnlyList<CategoriaDto> Categorias { get; private set; } = Array.Empty<CategoriaDto>();

    /// <summary>Opciones del selector de categorías formateadas para la UI.</summary>
    public IEnumerable<(int Id, string Nombre)> CategoriasOpciones =>
        Categorias.Select(c => (c.Id, c.Nombre));

    /// <summary>Opciones del selector "Subcategoría" del filtro, según la categoría elegida.</summary>
    public IEnumerable<(int Id, string Nombre)> SubcategoriasFiltroOpciones =>
        FiltroCategoriaId is { } catId
            ? Categorias.FirstOrDefault(c => c.Id == catId)?.Subcategorias.Select(s => (s.Id, s.Nombre)) ?? Enumerable.Empty<(int, string)>()
            : Categorias.SelectMany(c => c.Subcategorias.Select(s => (s.Id, $"{s.Nombre} ({c.Nombre})")));

    /// <summary>Opciones del selector "Subcategoría" en el formulario de alta/edición.</summary>
    public IEnumerable<(int Id, string Nombre)> SubcategoriasFormularioOpciones =>
        Categorias.SelectMany(c => c.Subcategorias.Where(s => s.Activo).Select(s => (s.Id, $"{c.Nombre} › {s.Nombre}")));

    public bool Editando { get; private set; }
    public ProductoDto? Original { get; private set; }

    private string _nombre = "";
    public string Nombre
    {
        get => _nombre;
        set { _nombre = value; ErroresCampos.Remove(nameof(Nombre)); }
    }

    private string _descripcion = "";
    public string Descripcion
    {
        get => _descripcion;
        set { _descripcion = value; ErroresCampos.Remove(nameof(Descripcion)); }
    }

    private string _precioTexto = "0.00";
    public string PrecioTexto
    {
        get => _precioTexto;
        set { _precioTexto = value; ErroresCampos.Remove(nameof(PrecioTexto)); }
    }

    private int _cantidadStock;
    public int CantidadStock
    {
        get => _cantidadStock;
        set { _cantidadStock = value; ErroresCampos.Remove(nameof(CantidadStock)); }
    }

    private DateOnly _fechaIngreso = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly FechaIngreso
    {
        get => _fechaIngreso;
        set { _fechaIngreso = value; ErroresCampos.Remove(nameof(FechaIngreso)); }
    }

    public bool MostrarStock { get; set; }

    private int? _subcategoriaId;
    public int? SubcategoriaId
    {
        get => _subcategoriaId;
        set { _subcategoriaId = value; ErroresCampos.Remove(nameof(SubcategoriaId)); }
    }

    public IReadOnlyList<FiltroDto> FiltrosSubcategoria { get; private set; } = Array.Empty<FiltroDto>();
    public Dictionary<int, int?> OpcionesPorFiltro { get; } = new();
    public Dictionary<string, string> ErroresCampos { get; } = new();

    public async Task IniciarAsync()
    {
        await EjecutarAsync(async ct => Categorias = await _servicio.ObtenerCategoriasAsync(ct));
        await BuscarAsync();
    }

    public Task BuscarAsync()
    {
        _pagina = 1;
        return CargarAsync();
    }

    public Task LimpiarAsync()
    {
        TextoBusqueda = "";
        FiltroCategoriaId = null;
        FiltroSubcategoriaId = null;
        FiltroActivo = true;
        return BuscarAsync();
    }

    public Task IrAPaginaAsync(int pagina)
    {
        _pagina = pagina;
        return CargarAsync();
    }

    private Task CargarAsync() => EjecutarAsync(async ct =>
        Pagina = await _servicio.BuscarAsync(TextoBusqueda, FiltroCategoriaId, FiltroSubcategoriaId, FiltroActivo, _pagina, 10, ct));

    public void Nuevo()
    {
        Original = null;
        Editando = true;
        Error = null;
        ErroresCampos.Clear();
        Nombre = "";
        Descripcion = "";
        PrecioTexto = "0.00";
        CantidadStock = 0;
        FechaIngreso = DateOnly.FromDateTime(DateTime.Today);
        MostrarStock = false;
        SubcategoriaId = null;
        FiltrosSubcategoria = Array.Empty<FiltroDto>();
        OpcionesPorFiltro.Clear();
        OnCambiado();
    }

    public async Task EditarAsync(ProductoDto producto)
    {
        Original = producto;
        Editando = true;
        Error = null;
        ErroresCampos.Clear();
        Nombre = producto.Nombre;
        Descripcion = producto.Descripcion;
        PrecioTexto = producto.PrecioVenta.ToString("F2", CultureInfo.InvariantCulture);
        CantidadStock = producto.CantidadStock;
        FechaIngreso = producto.FechaIngreso;
        MostrarStock = producto.MostrarStock;
        SubcategoriaId = producto.SubcategoriaId;
        OpcionesPorFiltro.Clear();

        if (producto.SubcategoriaId is { } subId)
        {
            await EjecutarAsync(async ct =>
            {
                FiltrosSubcategoria = await _servicio.ObtenerFiltrosAsync(subId, ct);
                if (producto.Detalles is not null)
                {
                    foreach (var d in producto.Detalles)
                        OpcionesPorFiltro[d.FiltroId] = d.OpcionId;
                }
            });
        }
        else
        {
            FiltrosSubcategoria = Array.Empty<FiltroDto>();
        }

        OnCambiado();
    }

    public void Cancelar()
    {
        Editando = false;
        Original = null;
        Error = null;
        ErroresCampos.Clear();
        OnCambiado();
    }

    public async Task AlCambiarSubcategoriaFormularioAsync(int? subcategoriaId)
    {
        SubcategoriaId = subcategoriaId;
        OpcionesPorFiltro.Clear();

        if (subcategoriaId is { } id)
        {
            await EjecutarAsync(async ct =>
            {
                FiltrosSubcategoria = await _servicio.ObtenerFiltrosAsync(id, ct);
                foreach (var f in FiltrosSubcategoria)
                    OpcionesPorFiltro[f.Id] = null;
            });
        }
        else
        {
            FiltrosSubcategoria = Array.Empty<FiltroDto>();
        }

        OnCambiado();
    }

    public void EstablecerOpcionFiltro(int filtroId, int? opcionId)
    {
        OpcionesPorFiltro[filtroId] = opcionId;
        OnCambiado();
    }

    public async Task GuardarAsync()
    {
        if (!Validar()) { OnCambiado(); return; }

        decimal precio = decimal.Parse(PrecioTexto, CultureInfo.InvariantCulture);
        var opcionIds = OpcionesPorFiltro.Values.Where(v => v.HasValue).Select(v => v!.Value).ToList();

        var ok = await EjecutarAsync(async ct =>
        {
            if (Original is null)
            {
                var solicitud = new CrearProductoSolicitud(
                    Nombre.Trim(),
                    Descripcion.Trim(),
                    precio,
                    CantidadStock,
                    FechaIngreso,
                    SubcategoriaId,
                    MostrarStock,
                    opcionIds);
                await _servicio.CrearAsync(solicitud, ct);
            }
            else
            {
                var solicitud = new ActualizarProductoSolicitud(
                    Nombre.Trim(),
                    Descripcion.Trim(),
                    precio,
                    CantidadStock,
                    FechaIngreso,
                    SubcategoriaId,
                    MostrarStock,
                    Original.RowVersion,
                    opcionIds);
                await _servicio.ActualizarAsync(Original.Id, solicitud, ct);
            }
        }, cancelarAnterior: false);

        if (ok)
        {
            Editando = false;
            Original = null;
            OnCambiado();
        }
    }

    public ProductoDto? Pendiente { get; private set; }
    public AccionProducto Accion { get; private set; }
    public bool HayConfirmacion => Pendiente is not null && Accion != AccionProducto.Ninguna;

    public string TituloConfirmacion => Accion switch
    {
        AccionProducto.DarDeBaja => "¿Dar de baja al producto?",
        AccionProducto.Reactivar => "¿Reactivar el producto?",
        _ => "",
    };

    public string TextoConfirmar => Accion switch
    {
        AccionProducto.DarDeBaja => "Dar de baja",
        AccionProducto.Reactivar => "Reactivar",
        _ => "Confirmar",
    };

    public bool EsPeligro => Accion == AccionProducto.DarDeBaja;

    public void SolicitarCambioEstado(ProductoDto producto)
    {
        Pendiente = producto;
        Accion = producto.Activo ? AccionProducto.DarDeBaja : AccionProducto.Reactivar;
        OnCambiado();
    }

    public void CancelarSolicitud()
    {
        Pendiente = null;
        Accion = AccionProducto.Ninguna;
        OnCambiado();
    }

    public async Task ConfirmarAsync()
    {
        var producto = Pendiente;
        var accion = Accion;
        if (producto is null || accion == AccionProducto.Ninguna) return;

        await EjecutarAsync(ct =>
            _servicio.CambiarEstadoAsync(producto.Id, accion == AccionProducto.Reactivar, ct),
            cancelarAnterior: false);

        Pendiente = null;
        Accion = AccionProducto.Ninguna;
        OnCambiado();
    }

    private bool Validar()
    {
        ErroresCampos.Clear();
        void Regla(string campo, string? err) { if (err is not null) ErroresCampos[campo] = err; }

        Regla(nameof(Nombre), Validacion.Primero(Nombre, Validacion.Texto("El nombre", 150)));
        Regla(nameof(Descripcion), Validacion.Primero(Descripcion, Validacion.Texto("La descripción", 500)));

        if (!decimal.TryParse(PrecioTexto, CultureInfo.InvariantCulture, out var p) || p < 0)
            ErroresCampos[nameof(PrecioTexto)] = "Ingrese un precio válido (mayor o igual a cero).";

        if (CantidadStock < 0)
            ErroresCampos[nameof(CantidadStock)] = "El stock no puede ser negativo.";

        if (FechaIngreso > DateOnly.FromDateTime(DateTime.Today))
            ErroresCampos[nameof(FechaIngreso)] = "La fecha de ingreso no puede ser futura.";

        return ErroresCampos.Count == 0;
    }

    public string? ErrorDe(string campo) => ErroresCampos.GetValueOrDefault(campo);

    private async void Productos_EntidadCambiada(object sender, EntidadCambiadaEventArgs e) =>
        await CargarAsync();

    public override void Dispose()
    {
        _servicio.EntidadCambiada -= Productos_EntidadCambiada;
        base.Dispose();
    }
}
