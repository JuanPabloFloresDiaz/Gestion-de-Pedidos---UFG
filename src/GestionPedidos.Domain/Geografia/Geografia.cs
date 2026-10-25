namespace GestionPedidos.Domain.Geografia;

/// <summary>Departamento de El Salvador (catálogo).</summary>
public class Departamento
{
    /// <summary>Identificador.</summary>
    public int Id { get; set; }
    /// <summary>Código ISO 3166-2 (por ejemplo <c>SS</c>).</summary>
    public string Codigo { get; set; } = "";
    /// <summary>Nombre.</summary>
    public string Nombre { get; set; } = "";
    /// <summary>Ciudades (distritos) del departamento.</summary>
    public List<Ciudad> Ciudades { get; set; } = new();
}

/// <summary>Ciudad = distrito municipal de El Salvador (catálogo).</summary>
public class Ciudad
{
    /// <summary>Identificador.</summary>
    public int Id { get; set; }
    /// <summary>Departamento al que pertenece.</summary>
    public int DepartamentoId { get; set; }
    /// <summary>Navegación al departamento.</summary>
    public Departamento? Departamento { get; set; }
    /// <summary>Municipio vigente (uno de los 44) al que pertenece el distrito.</summary>
    public string Municipio { get; set; } = "";
    /// <summary>Nombre del distrito.</summary>
    public string Nombre { get; set; } = "";
}
