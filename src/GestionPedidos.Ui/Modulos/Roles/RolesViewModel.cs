using GestionPedidos.Contracts;
using GestionPedidos.Ui.Comun;

namespace GestionPedidos.Ui.Modulos.Roles;

/// <summary>Un grupo de permisos de la pantalla (por ejemplo, "Clientes").</summary>
/// <param name="Titulo">Título del grupo.</param>
/// <param name="Permisos">Permisos del grupo.</param>
public sealed record GrupoPermisos(string Titulo, IReadOnlyList<PermisoDto> Permisos);

/// <summary>Estado y lógica de la pantalla de roles y permisos: elegir un rol, marcar sus permisos y guardar.</summary>
public sealed class RolesViewModel : ViewModelBase
{
    private static readonly Dictionary<string, string> Titulos = new()
    {
        ["admin"] = "Acceso", ["clientes"] = "Clientes", ["productos"] = "Productos", ["pedidos"] = "Pedidos",
        ["usuarios"] = "Usuarios", ["roles"] = "Roles y permisos", ["historial"] = "Historial",
        ["catalogos"] = "Catálogos geográficos", ["catalogo"] = "Tienda · catálogo", ["mispedidos"] = "Tienda · mis pedidos",
        ["perfil"] = "Tienda · perfil",
    };

    private readonly RolesService _roles;
    private HashSet<string> _guardados = new();

    /// <summary>Crea el ViewModel.</summary>
    public RolesViewModel(RolesService roles) => _roles = roles;

    /// <summary>Roles disponibles.</summary>
    public IReadOnlyList<RolDto> Roles { get; private set; } = Array.Empty<RolDto>();
    /// <summary>Catálogo de permisos.</summary>
    public IReadOnlyList<PermisoDto> Permisos { get; private set; } = Array.Empty<PermisoDto>();
    /// <summary>Rol elegido.</summary>
    public RolDto? Seleccionado { get; private set; }
    /// <summary>Códigos marcados actualmente en pantalla.</summary>
    public HashSet<string> Marcados { get; private set; } = new();
    /// <summary>Permisos del catálogo agrupados por área, en el orden del catálogo.</summary>
    public IReadOnlyList<GrupoPermisos> Grupos => Permisos
        .GroupBy(p => p.Codigo.Split('.')[0])
        .Select(g => new GrupoPermisos(Titulos.GetValueOrDefault(g.Key, g.Key), g.ToList()))
        .ToList();

    /// <summary>Indica si hay cambios sin guardar.</summary>
    public bool HayCambios => !Marcados.SetEquals(_guardados);
    /// <summary>Cantidad de permisos que se añadirían al guardar.</summary>
    public int Anadidos => Marcados.Except(_guardados).Count();
    /// <summary>Cantidad de permisos que se quitarían al guardar.</summary>
    public int Quitados => _guardados.Except(Marcados).Count();

    /// <summary>Carga roles y permisos y elige el primer rol.</summary>
    public async Task IniciarAsync()
    {
        await EjecutarAsync(async ct =>
        {
            Roles = await _roles.RolesAsync(ct);
            Permisos = await _roles.PermisosAsync(ct);
        });
        if (Roles.Count > 0 && Seleccionado is null) Seleccionar(Roles[0]);
    }

    /// <summary>Elige un rol y carga sus permisos (descarta lo no guardado del anterior).</summary>
    public void Seleccionar(RolDto rol)
    {
        Seleccionado = rol;
        _guardados = rol.Permisos.ToHashSet();
        Marcados = rol.Permisos.ToHashSet();
        Error = null;
        OnCambiado();
    }

    /// <summary>Indica si el permiso no se le puede quitar al rol elegido.</summary>
    public bool EsObligatorio(string codigo) => Seleccionado?.Obligatorios?.Contains(codigo) == true;
    /// <summary>Indica si el permiso no se le puede dar al rol elegido.</summary>
    public bool EsProhibido(string codigo) => Seleccionado?.Prohibidos?.Contains(codigo) == true;
    /// <summary>Indica si la casilla del permiso está bloqueada.</summary>
    public bool EstaBloqueado(string codigo) => EsObligatorio(codigo) || EsProhibido(codigo);

    /// <summary>Explica por qué un permiso está bloqueado.</summary>
    public string? NotaDeBloqueo(string codigo) =>
        EsObligatorio(codigo) ? $"El rol {Seleccionado!.Nombre} no puede quedarse sin este permiso."
        : EsProhibido(codigo) ? $"Este permiso no se puede asignar al rol {Seleccionado!.Nombre}." : null;

    /// <summary>Marca o desmarca un permiso (ignora los bloqueados).</summary>
    public void Alternar(string codigo, bool marcado)
    {
        if (EstaBloqueado(codigo)) return;
        if (marcado) Marcados.Add(codigo); else Marcados.Remove(codigo);
        OnCambiado();
    }

    /// <summary>Vuelve a los permisos guardados.</summary>
    public void Descartar()
    {
        if (Seleccionado is null) return;
        Marcados = _guardados.ToHashSet();
        Error = null;
        OnCambiado();
    }

    /// <summary>Guarda los permisos del rol elegido.</summary>
    public async Task GuardarAsync()
    {
        if (Seleccionado is null || !HayCambios) return;
        var rolId = Seleccionado.Id;
        RolDto? guardado = null;
        var ok = await EjecutarAsync(async ct => guardado = await _roles.GuardarPermisosAsync(rolId, Marcados.OrderBy(c => c).ToList(), ct), cancelarAnterior: false);
        if (!ok || guardado is null) return;

        // La respuesta no repite las reglas: se conservan las del rol y se reemplazan sus permisos.
        var actualizado = Seleccionado with { Permisos = guardado.Permisos };
        Roles = Roles.Select(r => r.Id == rolId ? actualizado : r).ToList();
        Seleccionar(actualizado);
    }
}
