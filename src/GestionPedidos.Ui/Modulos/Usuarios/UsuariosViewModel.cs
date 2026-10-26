using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun;
using GestionPedidos.Ui.Comun.Eventos;

namespace GestionPedidos.Ui.Modulos.Usuarios;

/// <summary>Acción que espera confirmación de la persona.</summary>
public enum AccionUsuario
{
    /// <summary>Nada pendiente.</summary>
    Ninguna,
    /// <summary>Dar de baja (eliminado suave).</summary>
    DarDeBaja,
    /// <summary>Reactivar.</summary>
    Reactivar,
    /// <summary>Eliminar definitivamente.</summary>
    Eliminar,
}

/// <summary>Estado y lógica de la pantalla de usuarios: listado con filtros, alta, edición y baja lógica.</summary>
public sealed class UsuariosViewModel : ViewModelBase
{
    private readonly UsuariosService _usuarios;

    /// <summary>Crea el ViewModel y se suscribe (<c>+=</c>) a los cambios de usuarios para refrescar el listado.</summary>
    public UsuariosViewModel(UsuariosService usuarios)
    {
        _usuarios = usuarios;
        _usuarios.EntidadCambiada += Usuarios_EntidadCambiada;
    }

    /// <summary>Texto de búsqueda.</summary>
    public string TextoBusqueda { get; set; } = "";
    /// <summary>Filtro por rol (<c>null</c> = todos).</summary>
    public int? FiltroRolId { get; set; }
    /// <summary>Filtro por estado: <c>null</c> todos, <c>true</c> activos, <c>false</c> inactivos.</summary>
    public bool? FiltroActivo { get; set; } = true;
    /// <summary>Página de resultados.</summary>
    public PaginaDto<UsuarioDto>? Pagina { get; private set; }
    /// <summary>Roles disponibles.</summary>
    public IReadOnlyList<RolDto> Roles { get; private set; } = Array.Empty<RolDto>();

    /// <summary>Indica si el panel de edición está abierto.</summary>
    public bool Editando { get; private set; }
    /// <summary>Usuario en edición, o <c>null</c> si es uno nuevo.</summary>
    public UsuarioDto? Original { get; private set; }
    private string _nombreUsuario = "";
    /// <summary>Campo: nombre de usuario. Al cambiarlo se quita su mensaje de error.</summary>
    public string NombreUsuario
    {
        get => _nombreUsuario;
        set { _nombreUsuario = value; ErroresCampos.Remove(nameof(NombreUsuario)); }
    }
    private string _nombreCompleto = "";
    /// <summary>Campo: nombre completo. Al cambiarlo se quita su mensaje de error.</summary>
    public string NombreCompleto
    {
        get => _nombreCompleto;
        set { _nombreCompleto = value; ErroresCampos.Remove(nameof(NombreCompleto)); }
    }
    private string _correo = "";
    /// <summary>Campo: correo. Al cambiarlo se quita su mensaje de error.</summary>
    public string Correo
    {
        get => _correo;
        set { _correo = value; ErroresCampos.Remove(nameof(Correo)); }
    }
    private int? _rolId = null;
    /// <summary>Campo: rol. Al cambiarlo se quita su mensaje de error.</summary>
    public int? RolId
    {
        get => _rolId;
        set { _rolId = value; ErroresCampos.Remove(nameof(RolId)); }
    }
    private string _claveInicial = "";
    /// <summary>Campo: contraseña inicial (solo al crear). Al cambiarlo se quita su mensaje de error.</summary>
    public string ClaveInicial
    {
        get => _claveInicial;
        set { _claveInicial = value; ErroresCampos.Remove(nameof(ClaveInicial)); }
    }
    /// <summary>Errores de validación por campo.</summary>
    public Dictionary<string, string> ErroresCampos { get; } = new();

    private int _pagina = 1;

    /// <summary>Carga los roles y la primera página.</summary>
    public async Task IniciarAsync()
    {
        await EjecutarAsync(async ct => Roles = await _usuarios.RolesAsync(ct));
        await BuscarAsync();
    }

    /// <summary>Busca con los filtros actuales, desde la primera página.</summary>
    public Task BuscarAsync() { _pagina = 1; return CargarAsync(); }

    /// <summary>Limpia los filtros y vuelve a buscar.</summary>
    public Task LimpiarAsync()
    {
        TextoBusqueda = ""; FiltroRolId = null; FiltroActivo = true;
        return BuscarAsync();
    }

    /// <summary>Va a la página indicada.</summary>
    public Task IrAPaginaAsync(int pagina) { _pagina = pagina; return CargarAsync(); }

    private Task CargarAsync() => EjecutarAsync(async ct =>
        Pagina = await _usuarios.BuscarAsync(TextoBusqueda, FiltroRolId, FiltroActivo, _pagina, 10, ct));

    /// <summary>Abre el panel para un usuario nuevo.</summary>
    public void Nuevo()
    {
        Original = null; Editando = true; Error = null; ErroresCampos.Clear();
        NombreUsuario = NombreCompleto = Correo = ClaveInicial = "";
        RolId = Roles.FirstOrDefault(r => r.Nombre == "Operador")?.Id;
        OnCambiado();
    }

    /// <summary>Abre el panel para editar un usuario existente.</summary>
    public void Editar(UsuarioDto usuario)
    {
        Original = usuario; Editando = true; Error = null; ErroresCampos.Clear();
        NombreUsuario = usuario.NombreUsuario; NombreCompleto = usuario.NombreCompleto;
        Correo = usuario.Correo; RolId = usuario.RolId; ClaveInicial = "";
        OnCambiado();
    }

    /// <summary>Cierra el panel de edición sin guardar.</summary>
    public void Cancelar() { Editando = false; Original = null; Error = null; ErroresCampos.Clear(); OnCambiado(); }

    /// <summary>Valida y guarda (alta o edición).</summary>
    public async Task GuardarAsync()
    {
        if (!Validar()) { OnCambiado(); return; }
        var ok = await EjecutarAsync(async ct =>
        {
            if (Original is null)
                await _usuarios.CrearAsync(new CrearUsuarioSolicitud(NombreUsuario.Trim(), NombreCompleto.Trim(),
                    Correo.Trim(), RolId!.Value, ClaveInicial), ct);
            else
                await _usuarios.ActualizarAsync(Original.Id, new ActualizarUsuarioSolicitud(NombreCompleto.Trim(),
                    Correo.Trim(), RolId!.Value, Original.RowVersion), ct);
        }, cancelarAnterior: false);
        if (ok) { Editando = false; Original = null; OnCambiado(); }
    }

    // ---- Cambios de estado con confirmación (el chip y los botones llevan al mismo diálogo) ----

    /// <summary>Usuario sobre el que se pide confirmación, o <c>null</c>.</summary>
    public UsuarioDto? Pendiente { get; private set; }
    /// <summary>Acción pendiente de confirmar.</summary>
    public AccionUsuario Accion { get; private set; }
    /// <summary>Indica si hay un diálogo de confirmación abierto.</summary>
    public bool HayConfirmacion => Pendiente is not null && Accion != AccionUsuario.Ninguna;
    /// <summary>Título del diálogo.</summary>
    public string TituloConfirmacion => Accion switch
    {
        AccionUsuario.DarDeBaja => "¿Dar de baja al usuario?",
        AccionUsuario.Reactivar => "¿Reactivar al usuario?",
        AccionUsuario.Eliminar => "¿Eliminar definitivamente?",
        _ => "",
    };
    /// <summary>Texto del botón de confirmar.</summary>
    public string TextoConfirmar => Accion switch
    {
        AccionUsuario.DarDeBaja => "Dar de baja",
        AccionUsuario.Reactivar => "Reactivar",
        AccionUsuario.Eliminar => "Eliminar definitivamente",
        _ => "Confirmar",
    };
    /// <summary>Indica si la acción es destructiva (el botón se pinta en rojo).</summary>
    public bool EsPeligro => Accion is AccionUsuario.DarDeBaja or AccionUsuario.Eliminar;
    /// <summary>Texto que hay que escribir para confirmar una eliminación definitiva (el nombre de usuario).</summary>
    public string? TextoAConfirmar => Accion == AccionUsuario.Eliminar ? Pendiente?.NombreUsuario : null;

    /// <summary>Pide confirmar la baja o la reactivación (según el estado actual del usuario).</summary>
    public void SolicitarCambioEstado(UsuarioDto usuario)
    {
        Pendiente = usuario;
        Accion = usuario.Activo ? AccionUsuario.DarDeBaja : AccionUsuario.Reactivar;
        OnCambiado();
    }

    /// <summary>Pide confirmar la eliminación definitiva (solo de usuarios ya dados de baja).</summary>
    public void SolicitarEliminacion(UsuarioDto usuario)
    {
        Pendiente = usuario;
        Accion = AccionUsuario.Eliminar;
        OnCambiado();
    }

    /// <summary>Cierra el diálogo sin hacer nada.</summary>
    public void CancelarSolicitud()
    {
        Pendiente = null;
        Accion = AccionUsuario.Ninguna;
        OnCambiado();
    }

    /// <summary>Ejecuta la acción confirmada; en cualquier caso el diálogo se cierra (un error se muestra en la pantalla).</summary>
    public async Task ConfirmarAsync()
    {
        var usuario = Pendiente;
        var accion = Accion;
        if (usuario is null || accion == AccionUsuario.Ninguna) return;
        await EjecutarAsync(ct => accion == AccionUsuario.Eliminar
            ? _usuarios.EliminarAsync(usuario, ct)
            : _usuarios.CambiarEstadoAsync(usuario.Id, accion == AccionUsuario.Reactivar, ct), cancelarAnterior: false);
        Pendiente = null;
        Accion = AccionUsuario.Ninguna;
        OnCambiado();
    }

    // Validación en la interfaz con reglas componibles; la API vuelve a validar siempre.
    private bool Validar()
    {
        ErroresCampos.Clear();
        void Regla(string campo, string? error) { if (error is not null) ErroresCampos[campo] = error; }
        if (Original is null) Regla(nameof(NombreUsuario), Validacion.Primero(NombreUsuario, Validacion.Texto("El usuario", 50, 3)));
        Regla(nameof(NombreCompleto), Validacion.Primero(NombreCompleto, Validacion.Texto("El nombre completo", 150)));
        Regla(nameof(Correo), Validacion.Primero(Correo, Validacion.Correo()));
        if (RolId is null) ErroresCampos[nameof(RolId)] = "Elija un rol.";
        if (Original is null) Regla(nameof(ClaveInicial), Validacion.Primero(ClaveInicial, Validacion.Clave()));
        return ErroresCampos.Count == 0;
    }

    /// <summary>Error de un campo, o <c>null</c>.</summary>
    public string? ErrorDe(string campo) => ErroresCampos.GetValueOrDefault(campo);

    // Un cambio confirmado de usuarios (hecho aquí o en otra pestaña) refresca el listado.
    // async void SOLO porque es un manejador de evento de C#; los errores los captura EjecutarAsync.
    private async void Usuarios_EntidadCambiada(object sender, EntidadCambiadaEventArgs e) => await CargarAsync();

    /// <summary>Baja de las suscripciones (par obligatorio del <c>+=</c>).</summary>
    public override void Dispose()
    {
        _usuarios.EntidadCambiada -= Usuarios_EntidadCambiada;
        base.Dispose();
    }
}
