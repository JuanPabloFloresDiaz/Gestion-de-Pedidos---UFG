using GestionPedidos.Ui.Comun.Eventos;

namespace GestionPedidos.Ui.Comun.Documentos;

/// <summary>Un documento abierto en una pestaña del shell.</summary>
/// <param name="Clave">Clave única de la pestaña (los módulos abiertos desde el menú usan la clave del módulo).</param>
/// <param name="Titulo">Título visible.</param>
/// <param name="Tipo">Componente Razor que se muestra.</param>
/// <param name="Version">Se incrementa al recargar la pestaña: un componente nuevo se crea y vuelve a cargar sus datos.</param>
public sealed record Documento(string Clave, string Titulo, Type Tipo, int Version = 0);

/// <summary>
/// Interfaz de múltiples documentos: lista de pestañas abiertas y la activa. Abrir un módulo desde el menú activa su
/// pestaña si ya estaba abierto; el botón "+" abre una pestaña nueva (un lanzador) que permite abrir un módulo en una
/// <b>instancia nueva</b>, así se pueden tener varias pestañas del mismo módulo con filtros distintos. Cada pestaña
/// conserva su estado mientras siga abierta.
/// </summary>
public sealed class DocumentosService
{
    private readonly List<Documento> _abiertos = new();
    private int _contadorNuevas;

    /// <summary>Se produce al abrir una pestaña nueva.</summary>
    public event DocumentoEventHandler? DocumentoAbierto;
    /// <summary>Se produce al cerrar una pestaña.</summary>
    public event DocumentoEventHandler? DocumentoCerrado;
    /// <summary>Se produce al cambiar de pestaña.</summary>
    public event DocumentoEventHandler? DocumentoActivado;
    /// <summary>Se produce al recargar una pestaña (su contenido se vuelve a crear).</summary>
    public event DocumentoEventHandler? DocumentoRecargado;

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

    /// <summary>Abre una pestaña nueva con el lanzador (el botón "+"); se activa de inmediato.</summary>
    /// <param name="tipoLanzador">Componente que lista los módulos disponibles.</param>
    public Documento NuevaPestana(Type tipoLanzador)
    {
        var doc = new Documento($"nueva#{++_contadorNuevas}", "Nueva pestaña", tipoLanzador);
        _abiertos.Add(doc);
        DocumentoAbierto?.Invoke(this, Datos(doc));
        Activar(doc.Clave);
        return doc;
    }

    /// <summary>
    /// Convierte una pestaña (normalmente el lanzador) en el módulo indicado, en el mismo lugar. Si ese módulo ya
    /// estaba abierto, crea una instancia nueva con título numerado (por ejemplo "Usuarios (2)").
    /// </summary>
    /// <param name="clave">Pestaña que se reemplaza.</param>
    /// <param name="modulo">Módulo que se abre.</param>
    public Documento? ReemplazarPorInstancia(string clave, Documento modulo)
    {
        var indice = _abiertos.FindIndex(d => d.Clave == clave);
        if (indice < 0) return null;
        var anterior = _abiertos[indice];

        var existentes = _abiertos.Count(d => d.Clave == modulo.Clave || d.Clave.StartsWith(modulo.Clave + "#", StringComparison.Ordinal));
        var nuevo = existentes == 0
            ? modulo
            : modulo with { Clave = $"{modulo.Clave}#{existentes + 1}", Titulo = $"{modulo.Titulo} ({existentes + 1})" };

        _abiertos[indice] = nuevo;
        DocumentoCerrado?.Invoke(this, Datos(anterior));
        DocumentoAbierto?.Invoke(this, Datos(nuevo));
        Activo = null;
        Activar(nuevo.Clave);
        return nuevo;
    }

    /// <summary>Vuelve a crear el contenido de la pestaña (recarga los datos y descarta su estado).</summary>
    public void Recargar(string clave)
    {
        var indice = _abiertos.FindIndex(d => d.Clave == clave);
        if (indice < 0) return;
        var recargado = _abiertos[indice] with { Version = _abiertos[indice].Version + 1 };
        _abiertos[indice] = recargado;
        if (Activo?.Clave == clave) Activo = recargado;
        DocumentoRecargado?.Invoke(this, Datos(recargado));
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
