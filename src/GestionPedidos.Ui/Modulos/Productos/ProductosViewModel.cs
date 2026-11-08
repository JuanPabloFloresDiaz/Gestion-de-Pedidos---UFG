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
    private readonly ProductosService _servicio;
    private int _pagina = 1;

    /// <summary>Crea el ViewModel y se suscribe al evento de cambio de entidades para refrescar.</summary>
    /// <param name="servicio">Servicio de productos.</param>
    public ProductosViewModel(ProductosService servicio)
    {
        _servicio = servicio;
        _servicio.EntidadCambiada += Productos_EntidadCambiada;
    }

    /// <summary>Texto de búsqueda por nombre, descripción o código.</summary>
    public string TextoBusqueda { get; set; } = "";

    /// <summary>Filtro por categoría en el listado.</summary>
    public int? FiltroCategoriaId { get; set; }

    /// <summary>Filtro por subcategoría en el listado.</summary>
    public int? FiltroSubcategoriaId { get; set; }

    /// <summary>Filtro por estado: <c>null</c> todos, <c>true</c> activos, <c>false</c> inactivos.</summary>
    public bool? FiltroActivo { get; set; } = true;

    /// <summary>Página de resultados actual.</summary>
    public PaginaDto<ProductoDto>? Pagina { get; private set; }

    /// <summary>Árbol de categorías disponibles en el sistema.</summary>
    public IReadOnlyList<CategoriaDto> Categorias { get; private set; } = Array.Empty<CategoriaDto>();

    /// <summary>Indica si el panel de alta/edición está abierto.</summary>
    public bool Editando { get; private set; }

    /// <summary>Producto en edición, o <c>null</c> si es uno nuevo.</summary>
    public ProductoDto? Original { get; private set; }

    private string _nombre = "";
    /// <summary>Campo: nombre del producto.</summary>
    public string Nombre
    {
        get => _nombre;
        set { _nombre = value; ErroresCampos.Remove(nameof(Nombre)); }
    }

    private string _descripcion = "";
    /// <summary>Campo: descripción del producto.</summary>
    public string Descripcion
    {
        get => _descripcion;
        set { _descripcion = value; ErroresCampos.Remove(nameof(Descripcion)); }
    }

    private string _precioTexto = "0.00";
    /// <summary>Campo: precio de venta en formato texto para edición.</summary>
    public string PrecioTexto
    {
        get => _precioTexto;
        set { _precioTexto = value; ErroresCampos.Remove(nameof(PrecioTexto)); }
    }

    private int _cantidadStock;
    /// <summary>Campo: cantidad en existencias.</summary>
    public int CantidadStock
    {
        get => _cantidadStock;
        set { _cantidadStock = value; ErroresCampos.Remove(nameof(CantidadStock)); }
    }

    private DateOnly _fechaIngreso = DateOnly.FromDateTime(DateTime.Today);
    /// <summary>Campo: fecha de ingreso al inventario.</summary>
    public DateOnly FechaIngreso
    {
        get => _fechaIngreso;
        set { _fechaIngreso = value; ErroresCampos.Remove(nameof(FechaIngreso)); }
    }

    /// <summary>Campo: interruptor para mostrar existencias exactas en la tienda web.</summary>
    public bool MostrarStock { get; set; }

    private int? _subcategoriaId;
    /// <summary>Campo: subcategoría elegida (Bloque K).</summary>
    public int? SubcategoriaId
    {
        get => _subcategoriaId;
        set { _subcategoriaId = value; ErroresCampos.Remove(nameof(SubcategoriaId)); }
    }

    /// <summary>Filtros asociados a la subcategoría seleccionada en el formulario.</summary>
    public IReadOnlyList<FiltroDto> FiltrosSubcategoria { get; private set; } = Array.Empty<FiltroDto>();

    /// <summary>Opciones elegidas por cada filtro (clave: FiltroId, valor: OpcionId).</summary>
    public Dictionary<int, int?> OpcionesPorFiltro { get; } = new();

    /// <summary>Errores de validación por campo para la interfaz.</summary>
    public Dictionary<string, string> ErroresCampos { get; } = new();

    /// <summary>Carga inicial de categorías y de la primera página de productos.</summary>
    public async Task IniciarAsync()
    {
        await EjecutarAsync(async ct => Categorias = await _servicio.ObtenerCategoriasAsync(ct));
        await BuscarAsync();
    }

    /// <summary>Busca productos con los filtros aplicados desde la primera página.</summary>
    public Task BuscarAsync()
    {
        _pagina = 1;
        return CargarAsync();
    }

    /// <summary>Limpia los filtros de búsqueda y recarga el listado.</summary>
    public Task LimpiarAsync()
    {
        TextoBusqueda = "";
        FiltroCategoriaId = null;
        FiltroSubcategoriaId = null;
        FiltroActivo = true;
        return BuscarAsync();
    }

    /// <summary>Navega a una página específica.</summary>
    /// <param name="pagina">Número de página.</param>
    public Task IrAPaginaAsync(int pagina)
    {
        _pagina = pagina;
        return CargarAsync();
    }

    private Task CargarAsync() => EjecutarAsync(async ct =>
        Pagina = await _servicio.BuscarAsync(TextoBusqueda, FiltroCategoriaId, FiltroSubcategoriaId, FiltroActivo, _pagina, 10, ct));

    /// <summary>Abre el panel lateral para dar de alta un producto nuevo.</summary>
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

    /// <summary>Abre el panel lateral para editar un producto existente.</summary>
    /// <param name="producto">Producto seleccionado.</param>
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

    /// <summary>Cierra el panel lateral sin guardar cambios.</summary>
    public void Cancelar()
    {
        Editando = false;
        Original = null;
        Error = null;
        ErroresCampos.Clear();
        OnCambiado();
    }

    /// <summary>Se ejecuta al cambiar la subcategoría en el formulario para cargar sus filtros dinámicos.</summary>
    /// <param name="subcategoriaId">Identificador de la subcategoría elegida.</param>
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

    /// <summary>Actualiza la opción seleccionada para un filtro específico.</summary>
    /// <param name="filtroId">Identificador del filtro.</param>
    /// <param name="opcionId">Identificador de la opción seleccionada.</param>
    public void EstablecerOpcionFiltro(int filtroId, int? opcionId)
    {
        OpcionesPorFiltro[filtroId] = opcionId;
        OnCambiado();
    }

    /// <summary>Valida los datos y persiste el producto (alta o modificación).</summary>
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

    // ---- Confirmaciones de baja lógica / reactivación ----

    /// <summary>Producto sobre el que se solicita confirmación.</summary>
    public ProductoDto? Pendiente { get; private set; }

    /// <summary>Acción que espera confirmación.</summary>
    public AccionProducto Accion { get; private set; }

    /// <summary>Indica si el diálogo de confirmación está abierto.</summary>
    public bool HayConfirmacion => Pendiente is not null && Accion != AccionProducto.Ninguna;

    /// <summary>Título del diálogo de confirmación.</summary>
    public string TituloConfirmacion => Accion switch
    {
        AccionProducto.DarDeBaja => "¿Dar de baja al producto?",
        AccionProducto.Reactivar => "¿Reactivar el producto?",
        _ => "",
    };

    /// <summary>Texto del botón de confirmación.</summary>
    public string TextoConfirmar => Accion switch
    {
        AccionProducto.DarDeBaja => "Dar de baja",
        AccionProducto.Reactivar => "Reactivar",
        _ => "Confirmar",
    };

    /// <summary>Indica si la acción es destructiva o de advertencia.</summary>
    public bool EsPeligro => Accion == AccionProducto.DarDeBaja;

    /// <summary>Solicita confirmación para dar de baja o reactivar un producto.</summary>
    /// <param name="producto">Producto seleccionado.</param>
    public void SolicitarCambioEstado(ProductoDto producto)
    {
        Pendiente = producto;
        Accion = producto.Activo ? AccionProducto.DarDeBaja : AccionProducto.Reactivar;
        OnCambiado();
    }

    /// <summary>Cierra el diálogo de confirmación sin ejecutar la acción.</summary>
    public void CancelarSolicitud()
    {
        Pendiente = null;
        Accion = AccionProducto.Ninguna;
        OnCambiado();
    }

    /// <summary>Ejecuta la acción de estado confirmada.</summary>
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

    /// <summary>Obtiene el error asociado a un campo del formulario.</summary>
    /// <param name="campo">Nombre de la propiedad.</param>
    /// <returns>Mensaje de error o <c>null</c>.</returns>
    public string? ErrorDe(string campo) => ErroresCampos.GetValueOrDefault(campo);

    private async void Productos_EntidadCambiada(object sender, EntidadCambiadaEventArgs e) =>
        await CargarAsync();

    /// <summary>Libera recursos y desuscribe eventos.</summary>
    public override void Dispose()
    {
        _servicio.EntidadCambiada -= Productos_EntidadCambiada;
        base.Dispose();
    }
}
