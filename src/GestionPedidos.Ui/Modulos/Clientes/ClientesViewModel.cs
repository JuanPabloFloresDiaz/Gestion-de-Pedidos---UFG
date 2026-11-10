using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun;
using GestionPedidos.Ui.Comun.Eventos;

namespace GestionPedidos.Ui.Modulos.Clientes;

/// <summary>Acción sobre un cliente que espera confirmación.</summary>
public enum AccionCliente
{
    /// <summary>Ninguna acción pendiente.</summary>
    Ninguna,
    /// <summary>Dar de baja lógica.</summary>
    DarDeBaja,
    /// <summary>Reactivar cliente inactivo.</summary>
    Reactivar,
}

/// <summary>Estado y lógica de la pantalla de clientes: listado, filtros dependientes, alta, edición y baja lógica ([Clientes.md §6]).</summary>
public sealed class ClientesViewModel : ViewModelBase
{
    private readonly ClientesService _clientes;

    /// <summary>Crea el ViewModel y se suscribe al evento de cambios de clientes.</summary>
    public ClientesViewModel(ClientesService clientes)
    {
        _clientes = clientes;
        _clientes.EntidadCambiada += Clientes_EntidadCambiada;
    }

    /// <summary>Texto de búsqueda (nombres, apellidos, teléfono o correo).</summary>
    public string TextoBusqueda { get; set; } = "";

    /// <summary>Filtro por departamento.</summary>
    public int? FiltroDepartamentoId { get; set; }

    /// <summary>Filtro por ciudad (dependiente de departamento).</summary>
    public int? FiltroCiudadId { get; set; }

    /// <summary>Filtro por estado: <c>null</c> todos, <c>true</c> activos, <c>false</c> inactivos.</summary>
    public bool? FiltroActivo { get; set; } = true;

    /// <summary>Página de resultados actual.</summary>
    public PaginaDto<ClienteDto>? Pagina { get; private set; }

    /// <summary>Departamentos de El Salvador.</summary>
    public IReadOnlyList<DepartamentoDto> Departamentos { get; private set; } = Array.Empty<DepartamentoDto>();

    /// <summary>Ciudades disponibles para el filtro de búsqueda.</summary>
    public IReadOnlyList<CiudadDto> CiudadesFiltro { get; private set; } = Array.Empty<CiudadDto>();

    /// <summary>Ciudades disponibles en el formulario de edición.</summary>
    public IReadOnlyList<CiudadDto> CiudadesFormulario { get; private set; } = Array.Empty<CiudadDto>();

    /// <summary>Indica si el panel de edición está abierto.</summary>
    public bool Editando { get; private set; }

    /// <summary>Cliente original en edición, o <c>null</c> si es un alta nueva.</summary>
    public ClienteDto? Original { get; private set; }

    private string _nombres = "";
    /// <summary>Campo: nombres.</summary>
    public string Nombres
    {
        get => _nombres;
        set { _nombres = value; ErroresCampos.Remove(nameof(Nombres)); }
    }

    private string _apellidos = "";
    /// <summary>Campo: apellidos.</summary>
    public string Apellidos
    {
        get => _apellidos;
        set { _apellidos = value; ErroresCampos.Remove(nameof(Apellidos)); }
    }

    private string _telefono = "";
    /// <summary>Campo: teléfono.</summary>
    public string Telefono
    {
        get => _telefono;
        set { _telefono = value; ErroresCampos.Remove(nameof(Telefono)); }
    }

    private string _direccion = "";
    /// <summary>Campo: dirección.</summary>
    public string Direccion
    {
        get => _direccion;
        set { _direccion = value; ErroresCampos.Remove(nameof(Direccion)); }
    }

    private string? _correo;
    /// <summary>Campo: correo opcional.</summary>
    public string? Correo
    {
        get => _correo;
        set { _correo = value; ErroresCampos.Remove(nameof(Correo)); }
    }

    private int? _departamentoId;
    /// <summary>Departamento seleccionado en el formulario.</summary>
    public int? DepartamentoId
    {
        get => _departamentoId;
        set { _departamentoId = value; ErroresCampos.Remove(nameof(DepartamentoId)); }
    }

    private int? _ciudadId;
    /// <summary>Ciudad seleccionada en el formulario.</summary>
    public int? CiudadId
    {
        get => _ciudadId;
        set { _ciudadId = value; ErroresCampos.Remove(nameof(CiudadId)); }
    }

    /// <summary>Errores de validación por campo.</summary>
    public Dictionary<string, string> ErroresCampos { get; } = new();

    private int _pagina = 1;

    /// <summary>Inicializa el ViewModel cargando catálogos y la primera página.</summary>
    public async Task IniciarAsync()
    {
        await EjecutarAsync(async ct => Departamentos = await _clientes.DepartamentosAsync(ct));
        await BuscarAsync();
    }

    /// <summary>Busca con los filtros actuales desde la primera página.</summary>
    public Task BuscarAsync() { _pagina = 1; return CargarAsync(); }

    /// <summary>Limpia filtros y vuelve a buscar.</summary>
    public async Task LimpiarAsync()
    {
        TextoBusqueda = "";
        FiltroDepartamentoId = null;
        FiltroCiudadId = null;
        FiltroActivo = true;
        CiudadesFiltro = Array.Empty<CiudadDto>();
        await BuscarAsync();
    }

    /// <summary>Va a la página indicada.</summary>
    public Task IrAPaginaAsync(int pagina) { _pagina = pagina; return CargarAsync(); }

    private Task CargarAsync() => EjecutarAsync(async ct =>
        Pagina = await _clientes.BuscarAsync(TextoBusqueda, FiltroDepartamentoId, FiltroCiudadId, FiltroActivo, _pagina, 10, ct));

    /// <summary>Se invoca al cambiar el departamento del filtro de búsqueda.</summary>
    public async Task AlCambiarDepartamentoFiltroAsync(int? deptoId)
    {
        FiltroDepartamentoId = deptoId;
        FiltroCiudadId = null;
        if (deptoId is { } id)
            await EjecutarAsync(async ct => CiudadesFiltro = await _clientes.CiudadesAsync(id, ct));
        else
            CiudadesFiltro = Array.Empty<CiudadDto>();
        await BuscarAsync();
    }

    /// <summary>Se invoca al cambiar el departamento en el panel de formulario.</summary>
    public async Task AlCambiarDepartamentoFormularioAsync(int? deptoId)
    {
        DepartamentoId = deptoId;
        CiudadId = null;
        ErroresCampos.Remove(nameof(CiudadId));
        if (deptoId is { } id)
            await EjecutarAsync(async ct => CiudadesFormulario = await _clientes.CiudadesAsync(id, ct));
        else
            CiudadesFormulario = Array.Empty<CiudadDto>();
        OnCambiado();
    }

    /// <summary>Abre el panel para un nuevo cliente.</summary>
    public void Nuevo()
    {
        Original = null;
        Editando = true;
        Error = null;
        ErroresCampos.Clear();
        Nombres = Apellidos = Telefono = Direccion = "";
        Correo = null;
        DepartamentoId = null;
        CiudadId = null;
        CiudadesFormulario = Array.Empty<CiudadDto>();
        OnCambiado();
    }

    /// <summary>Abre el panel para editar un cliente existente.</summary>
    public async Task EditarAsync(ClienteDto cliente)
    {
        Original = cliente;
        Editando = true;
        Error = null;
        ErroresCampos.Clear();
        Nombres = cliente.Nombres;
        Apellidos = cliente.Apellidos;
        Telefono = cliente.Telefono;
        Direccion = cliente.Direccion;
        Correo = cliente.Correo;
        DepartamentoId = cliente.DepartamentoId;
        CiudadId = cliente.CiudadId;

        if (cliente.DepartamentoId > 0)
            await EjecutarAsync(async ct => CiudadesFormulario = await _clientes.CiudadesAsync(cliente.DepartamentoId, ct));
        else
            CiudadesFormulario = Array.Empty<CiudadDto>();

        OnCambiado();
    }

    /// <summary>Cierra el panel sin guardar cambios.</summary>
    public void Cancelar()
    {
        Editando = false;
        Original = null;
        Error = null;
        ErroresCampos.Clear();
        OnCambiado();
    }

    /// <summary>Valida y guarda el cliente (alta o actualización).</summary>
    public async Task GuardarAsync()
    {
        if (!Validar()) { OnCambiado(); return; }

        var ok = await EjecutarAsync(async ct =>
        {
            if (Original is null)
            {
                await _clientes.CrearAsync(new CrearClienteSolicitud(
                    Nombres.Trim(), Apellidos.Trim(), Telefono.Trim(), Direccion.Trim(),
                    string.IsNullOrWhiteSpace(Correo) ? null : Correo.Trim(), CiudadId!.Value), ct);
            }
            else
            {
                await _clientes.ActualizarAsync(Original.Id, new ActualizarClienteSolicitud(
                    Nombres.Trim(), Apellidos.Trim(), Telefono.Trim(), Direccion.Trim(),
                    string.IsNullOrWhiteSpace(Correo) ? null : Correo.Trim(), CiudadId!.Value, Original.RowVersion), ct);
            }
        }, cancelarAnterior: false);

        if (ok)
        {
            Editando = false;
            Original = null;
            OnCambiado();
        }
    }

    // ---- Confirmaciones de baja suave y reactivación ----

    /// <summary>Cliente pendiente de confirmación de acción.</summary>
    public ClienteDto? Pendiente { get; private set; }

    /// <summary>Acción pendiente.</summary>
    public AccionCliente Accion { get; private set; }

    /// <summary>Indica si hay diálogo de confirmación abierto.</summary>
    public bool HayConfirmacion => Pendiente is not null && Accion != AccionCliente.Ninguna;

    /// <summary>Título del diálogo de confirmación.</summary>
    public string TituloConfirmacion => Accion switch
    {
        AccionCliente.DarDeBaja => "¿Dar de baja al cliente?",
        AccionCliente.Reactivar => "¿Reactivar al cliente?",
        _ => "",
    };

    /// <summary>Texto del botón de confirmación.</summary>
    public string TextoConfirmar => Accion == AccionCliente.DarDeBaja ? "Dar de baja" : "Reactivar";

    /// <summary>Indica si la acción es destructiva.</summary>
    public bool EsPeligro => Accion == AccionCliente.DarDeBaja;

    /// <summary>Pide confirmar baja o reactivación según el estado del cliente.</summary>
    public void SolicitarCambioEstado(ClienteDto cliente)
    {
        Pendiente = cliente;
        Accion = cliente.Activo ? AccionCliente.DarDeBaja : AccionCliente.Reactivar;
        OnCambiado();
    }

    /// <summary>Cancela el diálogo de confirmación.</summary>
    public void CancelarSolicitud()
    {
        Pendiente = null;
        Accion = AccionCliente.Ninguna;
        OnCambiado();
    }

    /// <summary>Ejecuta el cambio de estado confirmado.</summary>
    public async Task ConfirmarAsync()
    {
        var cliente = Pendiente;
        var accion = Accion;
        if (cliente is null || accion == AccionCliente.Ninguna) return;

        await EjecutarAsync(async ct =>
            await _clientes.CambiarEstadoAsync(cliente.Id, accion == AccionCliente.Reactivar, ct), cancelarAnterior: false);

        Pendiente = null;
        Accion = AccionCliente.Ninguna;
        OnCambiado();
    }

    private bool Validar()
    {
        ErroresCampos.Clear();
        void Regla(string campo, string? error) { if (error is not null) ErroresCampos[campo] = error; }

        Regla(nameof(Nombres), Validacion.Primero(Nombres, Validacion.Texto("Los nombres", 100)));
        Regla(nameof(Apellidos), Validacion.Primero(Apellidos, Validacion.Texto("Los apellidos", 100)));

        if (!EsTelefonoValido(Telefono))
            ErroresCampos[nameof(Telefono)] = "Teléfono de 8 a 20 caracteres (números, espacios, + o -).";

        Regla(nameof(Direccion), Validacion.Primero(Direccion, Validacion.Texto("La dirección", 250)));

        if (!string.IsNullOrWhiteSpace(Correo))
            Regla(nameof(Correo), Validacion.Primero(Correo, Validacion.Correo()));

        if (CiudadId is null)
            ErroresCampos[nameof(CiudadId)] = "Debe elegir una ciudad.";

        return ErroresCampos.Count == 0;
    }

    private static bool EsTelefonoValido(string? telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono)) return false;
        var t = telefono.Trim();
        if (t.Length is < 8 or > 20) return false;
        return t.All(c => char.IsDigit(c) || c is ' ' or '+' or '-');
    }

    /// <summary>Devuelve el error asociado a un campo, o <c>null</c>.</summary>
    public string? ErrorDe(string campo) => ErroresCampos.GetValueOrDefault(campo);

    private async void Clientes_EntidadCambiada(object sender, EntidadCambiadaEventArgs e) => await CargarAsync();

    /// <summary>Libera suscripciones.</summary>
    public override void Dispose()
    {
        _clientes.EntidadCambiada -= Clientes_EntidadCambiada;
        base.Dispose();
    }
}
