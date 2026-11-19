namespace GestionPedidos.Contracts;

/// <summary>Una opción para elegir en un reporte (por ejemplo, un usuario o una categoría).</summary>
/// <param name="Id">Identificador que se envía como filtro del reporte.</param>
/// <param name="Texto">Texto que ve la persona en la lista.</param>
public sealed record OpcionReporteDto(int Id, string Texto);
