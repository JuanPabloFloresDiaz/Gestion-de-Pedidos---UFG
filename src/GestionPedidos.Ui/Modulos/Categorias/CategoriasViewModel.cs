using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun;

namespace GestionPedidos.Ui.Modulos.Categorias;

/// <summary>Los cuatro niveles de la clasificación del catálogo.</summary>
public enum ElementoClasificacion
{
    /// <summary>Categoría.</summary>
    Categoria,
    /// <summary>Subcategoría.</summary>
    Subcategoria,
    /// <summary>Filtro.</summary>
    Filtro,
    /// <summary>Opción de un filtro.</summary>
    Opcion,
}

/// <summary>Acción que espera confirmación de la persona.</summary>
public enum AccionClasificacion
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

/// <summary>Elemento sobre el que se va a actuar (con lo mínimo necesario para el servicio y el mensaje).</summary>
/// <param name="Tipo">Nivel.</param>
/// <param name="Id">Identificador.</param>
/// <param name="Nombre">Nombre o valor visible.</param>
/// <param name="Activo">Estado actual.</param>
public sealed record Objetivo(ElementoClasificacion Tipo, int Id, string Nombre, bool Activo);

/// <summary>
/// Estado y lógica de la pantalla "Categorías y filtros": árbol de categorías/subcategorías, filtros con sus opciones,
/// un editor (alta y edición de cualquier nivel) y confirmaciones para dar de baja, reactivar y eliminar.
/// </summary>
public sealed class CategoriasViewModel : ViewModelBase
{
    private readonly CategoriasService _servicio;
    private int? _editandoId;
    private int? _padreId;
    private string? _rowVersion;

    /// <summary>Crea el ViewModel.</summary>
    public CategoriasViewModel(CategoriasService servicio) => _servicio = servicio;

    // ---------------------------------------------------------------- árbol y selección
    /// <summary>Árbol cargado.</summary>
    public IReadOnlyList<CategoriaDto> Arbol { get; private set; } = Array.Empty<CategoriaDto>();
    /// <summary>Si se muestran también los elementos dados de baja.</summary>
    public bool IncluirInactivas { get; private set; }
    /// <summary>Categoría seleccionada (o la de la subcategoría seleccionada).</summary>
    public CategoriaDto? CategoriaSel { get; private set; }
    /// <summary>Subcategoría seleccionada, o <c>null</c> si solo hay una categoría seleccionada.</summary>
    public SubcategoriaDto? SubcategoriaSel { get; private set; }
    /// <summary>Filtros de la subcategoría seleccionada.</summary>
    public IReadOnlyList<FiltroDto> Filtros { get; private set; } = Array.Empty<FiltroDto>();

    /// <summary>Carga el árbol.</summary>
    public Task IniciarAsync() => RecargarAsync();

    /// <summary>Muestra u oculta los elementos dados de baja.</summary>
    public Task AlternarInactivasAsync(bool incluir) { IncluirInactivas = incluir; return RecargarAsync(); }

    /// <summary>Selecciona una categoría.</summary>
    public void SeleccionarCategoria(CategoriaDto categoria)
    {
        CategoriaSel = categoria; SubcategoriaSel = null; Filtros = Array.Empty<FiltroDto>();
        OnCambiado();
    }

    /// <summary>Selecciona una subcategoría y carga sus filtros.</summary>
    public async Task SeleccionarSubcategoriaAsync(CategoriaDto categoria, SubcategoriaDto sub)
    {
        CategoriaSel = categoria; SubcategoriaSel = sub;
        await CargarFiltrosAsync();
    }

    /// <summary>Vuelve a consultar el árbol y los filtros conservando la selección (si sigue existiendo).</summary>
    public async Task RecargarAsync()
    {
        var categoriaId = CategoriaSel?.Id; var subId = SubcategoriaSel?.Id;
        var ok = await EjecutarAsync(async ct => Arbol = await _servicio.ArbolAsync(IncluirInactivas, ct));
        if (!ok) return;

        CategoriaSel = Arbol.FirstOrDefault(c => c.Id == categoriaId);
        SubcategoriaSel = CategoriaSel?.Subcategorias.FirstOrDefault(s => s.Id == subId);
        if (CategoriaSel is null && Arbol.Count > 0) CategoriaSel = Arbol[0];   // primera vez: algo que mostrar
        await CargarFiltrosAsync();
    }

    private async Task CargarFiltrosAsync()
    {
        if (SubcategoriaSel is null) { Filtros = Array.Empty<FiltroDto>(); OnCambiado(); return; }
        var id = SubcategoriaSel.Id;
        await EjecutarAsync(async ct => Filtros = await _servicio.FiltrosAsync(id, IncluirInactivas, ct), cancelarAnterior: false);
    }

    // ---------------------------------------------------------------- editor (alta y edición)
    /// <summary>Indica si el editor está abierto.</summary>
    public bool EditorAbierto { get; private set; }
    /// <summary>Nivel que se edita.</summary>
    public ElementoClasificacion EditorElemento { get; private set; }
    /// <summary>Indica si es un alta (si no, edición).</summary>
    public bool EditorEsNuevo => _editandoId is null;
    /// <summary>Nombre (o valor, en las opciones).</summary>
    public string Nombre { get; set; } = "";
    /// <summary>Descripción (solo categorías y subcategorías).</summary>
    public string Descripcion { get; set; } = "";
    /// <summary>Error de validación del editor, o <c>null</c>.</summary>
    public string? ErrorEditor { get; private set; }
    /// <summary>Indica si el nivel tiene descripción.</summary>
    public bool EditorTieneDescripcion => EditorElemento is ElementoClasificacion.Categoria or ElementoClasificacion.Subcategoria;

    /// <summary>Título del editor.</summary>
    public string TituloEditor
    {
        get
        {
            var que = EditorElemento switch
            {
                ElementoClasificacion.Categoria => "categoría", ElementoClasificacion.Subcategoria => "subcategoría",
                ElementoClasificacion.Filtro => "filtro", _ => "opción",
            };
            var f = EditorElemento is ElementoClasificacion.Categoria or ElementoClasificacion.Subcategoria or ElementoClasificacion.Opcion;
            return EditorEsNuevo ? $"{(f ? "Nueva" : "Nuevo")} {que}" : $"Editar {que}";
        }
    }

    /// <summary>Etiqueta del campo principal.</summary>
    public string EtiquetaNombre => EditorElemento == ElementoClasificacion.Opcion ? "Valor" : "Nombre";

    /// <summary>Abre el editor para crear una categoría.</summary>
    public void NuevaCategoria() => Abrir(ElementoClasificacion.Categoria, null, null, "", "", null);
    /// <summary>Abre el editor para crear una subcategoría en la categoría seleccionada.</summary>
    public void NuevaSubcategoria() { if (CategoriaSel is { } c) Abrir(ElementoClasificacion.Subcategoria, null, c.Id, "", "", null); }
    /// <summary>Abre el editor para crear un filtro en la subcategoría seleccionada.</summary>
    public void NuevoFiltro() { if (SubcategoriaSel is { } s) Abrir(ElementoClasificacion.Filtro, null, s.Id, "", "", null); }
    /// <summary>Abre el editor para crear una opción en un filtro.</summary>
    public void NuevaOpcion(FiltroDto filtro) => Abrir(ElementoClasificacion.Opcion, null, filtro.Id, "", "", null);

    /// <summary>Abre el editor para editar la categoría.</summary>
    public void Editar(CategoriaDto c) => Abrir(ElementoClasificacion.Categoria, c.Id, null, c.Nombre, c.Descripcion ?? "", c.RowVersion);
    /// <summary>Abre el editor para editar la subcategoría.</summary>
    public void Editar(SubcategoriaDto s) => Abrir(ElementoClasificacion.Subcategoria, s.Id, s.CategoriaId, s.Nombre, s.Descripcion ?? "", s.RowVersion);
    /// <summary>Abre el editor para editar el filtro.</summary>
    public void Editar(FiltroDto f) => Abrir(ElementoClasificacion.Filtro, f.Id, f.SubcategoriaId, f.Nombre, "", f.RowVersion);
    /// <summary>Abre el editor para editar la opción.</summary>
    public void Editar(OpcionFiltroDto o) => Abrir(ElementoClasificacion.Opcion, o.Id, o.FiltroId, o.Valor, "", o.RowVersion);

    private void Abrir(ElementoClasificacion elemento, int? id, int? padreId, string nombre, string descripcion, string? rowVersion)
    {
        EditorElemento = elemento; _editandoId = id; _padreId = padreId; _rowVersion = rowVersion;
        Nombre = nombre; Descripcion = descripcion; ErrorEditor = null; EditorAbierto = true;
        OnCambiado();
    }

    /// <summary>Cierra el editor sin guardar.</summary>
    public void CancelarEditor() { EditorAbierto = false; ErrorEditor = null; OnCambiado(); }

    /// <summary>Valida en la interfaz y guarda; si la API rechaza, el editor sigue abierto con el motivo.</summary>
    public async Task GuardarEditorAsync()
    {
        var nombre = Nombre?.Trim() ?? "";
        var descripcion = Descripcion?.Trim() ?? "";
        ErrorEditor = nombre.Length == 0 ? $"{EtiquetaNombre} es obligatorio."
            : nombre.Length > 100 ? $"{EtiquetaNombre} no puede superar 100 caracteres."
            : EditorTieneDescripcion && descripcion.Length > 300 ? "La descripción no puede superar 300 caracteres." : null;
        if (ErrorEditor is not null) { OnCambiado(); return; }

        var clasif = new GuardarClasificacionSolicitud(nombre, descripcion.Length == 0 ? null : descripcion, _rowVersion);
        var filtro = new GuardarFiltroSolicitud(nombre, _rowVersion);
        var opcion = new GuardarOpcionSolicitud(nombre, _rowVersion);
        var (id, padre) = (_editandoId, _padreId);

        var ok = await EjecutarAsync(async ct =>
        {
            switch (EditorElemento)
            {
                case ElementoClasificacion.Categoria:
                    if (id is null) await _servicio.CrearCategoriaAsync(clasif, ct); else await _servicio.ActualizarCategoriaAsync(id.Value, clasif, ct);
                    break;
                case ElementoClasificacion.Subcategoria:
                    if (id is null) await _servicio.CrearSubcategoriaAsync(padre!.Value, clasif, ct); else await _servicio.ActualizarSubcategoriaAsync(id.Value, clasif, ct);
                    break;
                case ElementoClasificacion.Filtro:
                    if (id is null) await _servicio.CrearFiltroAsync(padre!.Value, filtro, ct); else await _servicio.ActualizarFiltroAsync(id.Value, filtro, ct);
                    break;
                default:
                    if (id is null) await _servicio.CrearOpcionAsync(padre!.Value, opcion, ct); else await _servicio.ActualizarOpcionAsync(id.Value, opcion, ct);
                    break;
            }
        }, cancelarAnterior: false);

        if (!ok)
        {
            ErrorEditor = Error;   // el motivo se muestra dentro del editor, que sigue abierto
            Error = null;
            OnCambiado();
            return;
        }
        EditorAbierto = false;
        await RecargarAsync();
    }

    // ---------------------------------------------------------------- confirmaciones
    /// <summary>Elemento sobre el que se pide confirmación, o <c>null</c>.</summary>
    public Objetivo? Pendiente { get; private set; }
    /// <summary>Acción pendiente de confirmar.</summary>
    public AccionClasificacion Accion { get; private set; }
    /// <summary>Indica si hay un diálogo de confirmación abierto.</summary>
    public bool HayConfirmacion => Pendiente is not null && Accion != AccionClasificacion.Ninguna;
    /// <summary>Título del diálogo.</summary>
    public string TituloConfirmacion => Accion switch
    {
        AccionClasificacion.DarDeBaja => "¿Dar de baja?",
        AccionClasificacion.Reactivar => "¿Reactivar?",
        AccionClasificacion.Eliminar => "¿Eliminar definitivamente?",
        _ => "",
    };
    /// <summary>Texto del botón de confirmar.</summary>
    public string TextoConfirmar => Accion switch
    {
        AccionClasificacion.DarDeBaja => "Dar de baja", AccionClasificacion.Reactivar => "Reactivar",
        AccionClasificacion.Eliminar => "Eliminar definitivamente", _ => "Confirmar",
    };
    /// <summary>Indica si la acción es destructiva (botón rojo).</summary>
    public bool EsPeligro => Accion is AccionClasificacion.DarDeBaja or AccionClasificacion.Eliminar;
    /// <summary>Texto que hay que escribir para eliminar una categoría o subcategoría; <c>null</c> en los demás casos.</summary>
    public string? TextoAConfirmar => Accion == AccionClasificacion.Eliminar
        && Pendiente?.Tipo is ElementoClasificacion.Categoria or ElementoClasificacion.Subcategoria ? Pendiente.Nombre : null;

    /// <summary>Pide confirmar la baja o la reactivación (según el estado actual).</summary>
    public void SolicitarCambioEstado(Objetivo objetivo)
    {
        Pendiente = objetivo; Accion = objetivo.Activo ? AccionClasificacion.DarDeBaja : AccionClasificacion.Reactivar; OnCambiado();
    }

    /// <summary>Pide confirmar la eliminación definitiva (solo de elementos dados de baja).</summary>
    public void SolicitarEliminacion(Objetivo objetivo) { Pendiente = objetivo; Accion = AccionClasificacion.Eliminar; OnCambiado(); }

    /// <summary>Cierra el diálogo sin hacer nada.</summary>
    public void CancelarSolicitud() { Pendiente = null; Accion = AccionClasificacion.Ninguna; OnCambiado(); }

    /// <summary>Ejecuta la acción confirmada; el diálogo se cierra y un error (por ejemplo, una regla) se muestra en pantalla.</summary>
    public async Task ConfirmarAsync()
    {
        var o = Pendiente; var accion = Accion;
        if (o is null || accion == AccionClasificacion.Ninguna) return;
        var activar = accion == AccionClasificacion.Reactivar;

        var ok = await EjecutarAsync(ct => (o.Tipo, accion) switch
        {
            (ElementoClasificacion.Categoria, AccionClasificacion.Eliminar) => _servicio.EliminarCategoriaAsync(o.Id, o.Nombre, ct),
            (ElementoClasificacion.Subcategoria, AccionClasificacion.Eliminar) => _servicio.EliminarSubcategoriaAsync(o.Id, o.Nombre, ct),
            (ElementoClasificacion.Filtro, AccionClasificacion.Eliminar) => _servicio.EliminarFiltroAsync(o.Id, o.Nombre, ct),
            (ElementoClasificacion.Opcion, AccionClasificacion.Eliminar) => _servicio.EliminarOpcionAsync(o.Id, o.Nombre, ct),
            (ElementoClasificacion.Categoria, _) => _servicio.CambiarEstadoCategoriaAsync(o.Id, activar, ct),
            (ElementoClasificacion.Subcategoria, _) => _servicio.CambiarEstadoSubcategoriaAsync(o.Id, activar, ct),
            (ElementoClasificacion.Filtro, _) => _servicio.CambiarEstadoFiltroAsync(o.Id, activar, ct),
            _ => _servicio.CambiarEstadoOpcionAsync(o.Id, activar, ct),
        }, cancelarAnterior: false);

        Pendiente = null; Accion = AccionClasificacion.Ninguna;
        var mensaje = Error;
        if (ok) await RecargarAsync();
        else { Error = mensaje; OnCambiado(); }
    }
}
