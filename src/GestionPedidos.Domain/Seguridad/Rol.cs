namespace GestionPedidos.Domain.Seguridad;

/// <summary>Rol de un usuario; agrupa permisos.</summary>
public class Rol
{
    /// <summary>Identificador.</summary>
    public int Id { get; set; }
    /// <summary>Nombre único del rol.</summary>
    public string Nombre { get; set; } = "";
    /// <summary>Descripción opcional.</summary>
    public string? Descripcion { get; set; }
    /// <summary>Permisos del rol.</summary>
    public List<RolPermiso> Permisos { get; set; } = new();

    /// <summary>
    /// Deja al rol con exactamente los permisos indicados: quita los que sobran y añade los que faltan,
    /// sin tocar los que ya tenía (así solo se escriben los cambios).
    /// </summary>
    /// <param name="permisoIds">Identificadores de los permisos que debe tener el rol.</param>
    public void EstablecerPermisos(IReadOnlyCollection<int> permisoIds)
    {
        Permisos.RemoveAll(p => !permisoIds.Contains(p.PermisoId));
        var actuales = Permisos.Select(p => p.PermisoId).ToHashSet();
        foreach (var id in permisoIds.Where(id => !actuales.Contains(id)))
            Permisos.Add(new RolPermiso { RolId = Id, PermisoId = id });
    }
}

/// <summary>Permiso individual (por ejemplo <c>clientes.crear</c>).</summary>
public class Permiso
{
    /// <summary>Identificador.</summary>
    public int Id { get; set; }
    /// <summary>Código único.</summary>
    public string Codigo { get; set; } = "";
    /// <summary>Descripción legible.</summary>
    public string Descripcion { get; set; } = "";
}

/// <summary>Relación entre un rol y un permiso.</summary>
public class RolPermiso
{
    /// <summary>Rol.</summary>
    public int RolId { get; set; }
    /// <summary>Navegación al rol.</summary>
    public Rol? Rol { get; set; }
    /// <summary>Permiso.</summary>
    public int PermisoId { get; set; }
    /// <summary>Navegación al permiso.</summary>
    public Permiso? Permiso { get; set; }
}
