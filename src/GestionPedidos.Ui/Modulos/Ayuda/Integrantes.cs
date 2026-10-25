namespace GestionPedidos.Ui.Modulos.Ayuda;

/// <summary>Integrante del equipo (RF-09: carnet y nombre completo, tal como figura en el enunciado).</summary>
/// <param name="Carnet">Carnet.</param>
/// <param name="Nombre">Nombre completo.</param>
public sealed record Integrante(string Carnet, string Nombre);

/// <summary>Datos de la materia y del equipo; viven en un solo lugar para corregirlos sin tocar el diseño.</summary>
public static class DatosEquipo
{
    /// <summary>Asignatura.</summary>
    public const string Asignatura = "Programación Orientada a Eventos";
    /// <summary>Docente.</summary>
    public const string Docente = "Ing. Luis Gavidia";
    /// <summary>Ciclo.</summary>
    public const string Ciclo = "02-2026";
    /// <summary>Universidad y facultad.</summary>
    public const string Institucion = "Universidad Francisco Gavidia — Facultad de Ingeniería y Sistemas";

    /// <summary>Integrantes del equipo.</summary>
    public static readonly IReadOnlyList<Integrante> Integrantes = new[]
    {
        new Integrante("FD100125", "Juan Pablo Flores Díaz"),
        new Integrante("FC100426", "Anderson Rene Figueroa Coto"),
        new Integrante("MJ100326", "Adiel Enrique Montepeque Juarez"),
        new Integrante("AC101325", "Daniel Alexander Álvarez Chávez"),
    };
}
