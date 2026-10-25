using System.Text.Json;
using GestionPedidos.Domain.Geografia;

namespace GestionPedidos.Infrastructure.Persistence.Seed;

/// <summary>Lee el catálogo geográfico embebido (<c>seed/el-salvador-geo.json</c>) con identificadores estables.</summary>
public static class GeoSeed
{
    private static readonly Lazy<(IReadOnlyList<Departamento>, IReadOnlyList<Ciudad>)> Datos = new(Cargar);

    /// <summary>Los 14 departamentos (Id 1..14 según el orden del archivo).</summary>
    public static IReadOnlyList<Departamento> Departamentos => Datos.Value.Item1;

    /// <summary>Los 262 distritos (Id 1..262 según el orden del archivo).</summary>
    public static IReadOnlyList<Ciudad> Ciudades => Datos.Value.Item2;

    private static (IReadOnlyList<Departamento>, IReadOnlyList<Ciudad>) Cargar()
    {
        using var flujo = typeof(GeoSeed).Assembly.GetManifestResourceStream("el-salvador-geo.json")
            ?? throw new InvalidOperationException("No se encontró el recurso el-salvador-geo.json.");
        using var doc = JsonDocument.Parse(flujo);
        var departamentos = new List<Departamento>();
        var ciudades = new List<Ciudad>();
        foreach (var d in doc.RootElement.GetProperty("departamentos").EnumerateArray())
        {
            var dep = new Departamento
            {
                Id = departamentos.Count + 1,
                Codigo = d.GetProperty("codigo").GetString()!,
                Nombre = d.GetProperty("nombre").GetString()!,
            };
            departamentos.Add(dep);
            foreach (var c in d.GetProperty("ciudades").EnumerateArray())
                ciudades.Add(new Ciudad
                {
                    Id = ciudades.Count + 1,
                    DepartamentoId = dep.Id,
                    Municipio = c.GetProperty("municipio").GetString()!,
                    Nombre = c.GetProperty("nombre").GetString()!,
                });
        }
        return (departamentos, ciudades);
    }
}
