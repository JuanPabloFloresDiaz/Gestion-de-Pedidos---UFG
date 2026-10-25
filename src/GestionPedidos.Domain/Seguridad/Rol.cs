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
