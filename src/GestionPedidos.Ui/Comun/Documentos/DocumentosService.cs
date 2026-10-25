using GestionPedidos.Ui.Comun.Eventos;

namespace GestionPedidos.Ui.Comun.Documentos;

/// <summary>Un documento abierto en una pestaña del shell.</summary>
/// <param name="Clave">Clave única: abrir otra vez el mismo módulo solo activa su pestaña.</param>
/// <param name="Titulo">Título visible.</param>
/// <param name="Tipo">Componente Razor que se muestra.</param>
public sealed record Documento(string Clave, string Titulo, Type Tipo);

/// <summary>
/// Interfaz de múltiples documentos: lista de pestañas abiertas y la activa. Abrir un módulo ya abierto
/// activa su pestaña; cada pestaña conserva su estado mientras siga abierta.
/// </summary>
public sealed class DocumentosService
{
    private readonly List<Documento> _abiertos = new();

    /// <summary>Se produce al abrir una pestaña nueva.</summary>
    public event DocumentoEventHandler? DocumentoAbierto;
    /// <summary>Se produce al cerrar una pestaña.</summary>
    public event DocumentoEventHandler? DocumentoCerrado;
    /// <summary>Se produce al cambiar de pestaña.</summary>
    public event DocumentoEventHandler? DocumentoActivado;

    /// <summary>Pestañas abiertas, en orden.</summary>
    public IReadOnlyList<Documento> Abiertos => _abiertos;
    /// <summary>Pestaña activa, o <c>null</c>.</summary>
    public Documento? Activo { get; private set; }

    /// <summary>Abre el documento, o activa su pestaña si ya estaba abierto.</summary>
    public void Abrir(Documento documento)
    {
        var existente = _abiertos.FirstOrDefault(d => d.Clave == documento.Clave);
        if (existente is null)
        {
            _abiertos.Add(documento);
            existente = documento;
            DocumentoAbierto?.Invoke(this, Datos(documento));
        }
        Activar(existente.Clave);
    }

    /// <summary>Activa la pestaña con esa clave.</summary>
    public void Activar(string clave)
    {
        var doc = _abiertos.FirstOrDefault(d => d.Clave == clave);
        if (doc is null || Activo == doc) return;
        Activo = doc;
        DocumentoActivado?.Invoke(this, Datos(doc));
    }

    /// <summary>Cierra la pestaña y activa la vecina.</summary>
    public void Cerrar(string clave)
    {
        var indice = _abiertos.FindIndex(d => d.Clave == clave);
        if (indice < 0) return;
        var doc = _abiertos[indice];
        _abiertos.RemoveAt(indice);
        DocumentoCerrado?.Invoke(this, Datos(doc));
        if (Activo?.Clave == clave)
        {
            Activo = null;
            if (_abiertos.Count > 0) Activar(_abiertos[Math.Min(indice, _abiertos.Count - 1)].Clave);
            else DocumentoActivado?.Invoke(this, Datos(doc));
        }
    }

    /// <summary>Cierra todas las pestañas.</summary>
    public void CerrarTodas()
    {
        foreach (var clave in _abiertos.Select(d => d.Clave).ToList()) Cerrar(clave);
    }

    private static DocumentoEventArgs Datos(Documento d) => new() { Clave = d.Clave, Titulo = d.Titulo };
}
