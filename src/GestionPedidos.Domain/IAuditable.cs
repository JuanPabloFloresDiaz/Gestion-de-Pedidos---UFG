namespace GestionPedidos.Domain;

/// <summary>Entidad que guarda cuándo se creó y se modificó (la base de contexto lo mantiene).</summary>
public interface IAuditable
{
    /// <summary>Creación (UTC).</summary>
    DateTimeOffset CreadoEn { get; set; }
    /// <summary>Última modificación (UTC).</summary>
    DateTimeOffset ActualizadoEn { get; set; }
}
