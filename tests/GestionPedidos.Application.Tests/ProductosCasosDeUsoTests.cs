using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Application.Productos;
using GestionPedidos.Contracts;
using GestionPedidos.Domain.Productos;
using Microsoft.Extensions.Time.Testing;

namespace GestionPedidos.Application.Tests;

internal sealed class ProductoRepositoryFake : IProductoRepository
{
    private int _siguienteId = 1;
    public List<Producto> Productos { get; } = new();
    public HashSet<int> SubcategoriasActivas { get; } = new();
    /// <summary>Opciones existentes: id de opción → (filtro, subcategoría).</summary>
    public Dictionary<int, (int FiltroId, int SubcategoriaId)> Opciones { get; } = new();
    public int VersionesFijadas { get; private set; }

    public Task<Producto?> ObtenerPorIdAsync(int id, CancellationToken ct) => Task.FromResult(Productos.FirstOrDefault(p => p.Id == id));

    public Task<(IReadOnlyList<Producto> Elementos, int Total)> BuscarAsync(string? q, int? categoriaId, int? subcategoriaId, bool? activo, int pagina, int tamano, CancellationToken ct)
    {
        var l = Productos.Where(p => activo is null || p.Activo == activo).ToList();
        return Task.FromResult(((IReadOnlyList<Producto>)l, l.Count));
    }

    public Task<bool> ExisteNombreAsync(string nombre, int? exceptoId, CancellationToken ct) =>
        Task.FromResult(Productos.Any(p => p.Nombre == nombre && (exceptoId is null || p.Id != exceptoId)));

    public Task<bool> ExisteSubcategoriaActivaAsync(int subcategoriaId, CancellationToken ct) => Task.FromResult(SubcategoriasActivas.Contains(subcategoriaId));

    public Task<IReadOnlyList<(int FiltroId, int OpcionId)>> ObtenerOpcionesValidasAsync(int subcategoriaId, IEnumerable<int> opcionIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<(int, int)>>(opcionIds.Distinct()
            .Where(id => Opciones.TryGetValue(id, out var o) && o.SubcategoriaId == subcategoriaId)
            .Select(id => (Opciones[id].FiltroId, id)).ToList());

    public void FijarVersionOriginal(Producto producto, byte[] rowVersion) => VersionesFijadas++;

    public void Agregar(Producto producto)
    {
        if (producto.Id == 0) producto.Id = _siguienteId++;
        Productos.Add(producto);
    }
}

/// <summary>
/// Casos de uso de productos ([Productos.md]): validaciones, clasificación, versión obligatoria al editar, fecha de El Salvador
/// y eventos (una vez y solo si tuvo éxito, [Testing.md §3]).
/// </summary>
public class ProductosCasosDeUsoTests
{
    // 12:00 UTC del 8-nov = 06:00 del 8-nov en El Salvador
    private static readonly DateTimeOffset Ahora = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoy = new(2026, 11, 8);
    private const string VersionValida = "AAAAAAAAB9E=";   // base64 de 8 bytes, como la devuelve SQL Server

    private readonly FakeTimeProvider _reloj = new(Ahora);
    private readonly UnitOfWorkFake _uow = new();
    private readonly EventosAplicacion _eventos = new();
    private readonly ProductoRepositoryFake _repo = new();
    private readonly List<EntidadCambiadaServidorEventArgs> _publicados = new();

    public ProductosCasosDeUsoTests()
    {
        _repo.SubcategoriasActivas.Add(5);
        _repo.Opciones[100] = (FiltroId: 1, SubcategoriaId: 5);   // Color = Negro
        _repo.Opciones[101] = (FiltroId: 1, SubcategoriaId: 5);   // Color = Blanco
        _repo.Opciones[200] = (FiltroId: 2, SubcategoriaId: 5);   // Talla = M
        _repo.Opciones[300] = (FiltroId: 9, SubcategoriaId: 6);   // de OTRA subcategoría
        _eventos.EntidadCambiada += (_, e) => _publicados.Add(e);
    }

    private CrearProducto Crear(TimeProvider? reloj = null) => new(_repo, _uow, _eventos, reloj ?? _reloj);
    private ActualizarProducto Actualizar() => new(_repo, _uow, _eventos, _reloj);

    private static CrearProductoSolicitud Nuevo(string nombre = "Camisa", decimal precio = 19.5m, int stock = 10, DateOnly? ingreso = null,
        int? subcategoria = null, IReadOnlyList<int>? opciones = null) =>
        new(nombre, "Descripción", precio, stock, ingreso ?? Hoy, subcategoria, true, opciones);

    private static ActualizarProductoSolicitud Edicion(string nombre = "Camisa", string? version = VersionValida, int? subcategoria = null, IReadOnlyList<int>? opciones = null) =>
        new(nombre, "Descripción", 25m, 8, Hoy, subcategoria, true, version, opciones);

    private Producto Existente(int id = 1, string nombre = "Camisa", bool activo = true)
    {
        var p = new Producto { Id = id, Codigo = $"PRD-{id:000000}", Nombre = nombre, Descripcion = "d", PrecioVenta = 10m, CantidadStock = 5, Activo = activo, FechaIngreso = Hoy };
        _repo.Productos.Add(p);
        return p;
    }

    private static void AssertError(Resultado res, TipoError tipo)
    {
        Assert.False(res.Exito);
        Assert.Equal(tipo, res.Error!.Tipo);
    }

    // ===================== Crear =====================

    [Fact]
    public async Task Crear_Exitoso_Guarda_Una_Vez_Y_PublicaUnEvento()
    {
        var res = await Crear().EjecutarAsync(Nuevo(), 7, default);

        Assert.True(res.Exito);
        Assert.Equal(1, _uow.Guardados);
        Assert.Equal("Camisa", res.Valor!.Nombre);
        var ev = Assert.Single(_publicados);
        Assert.Equal(TipoCambioServidor.Registrada, ev.Tipo);
        Assert.Equal(7, ev.UsuarioId);
    }

    [Theory]
    [InlineData("", 10, 5)]
    [InlineData("   ", 10, 5)]
    [InlineData("Camisa", -1, 5)]
    [InlineData("Camisa", 10.123, 5)]          // más de 2 decimales
    [InlineData("Camisa", 10000000000, 5)]     // no cabe en decimal(12,2)
    [InlineData("Camisa", 10, -1)]
    public async Task Crear_DatosInvalidos_Validacion_SinGuardarNiEvento(string nombre, decimal precio, int stock)
    {
        var res = await Crear().EjecutarAsync(Nuevo(nombre, precio, stock), 7, default);

        AssertError(res, TipoError.Validacion);
        Assert.Equal(0, _uow.Guardados);
        Assert.Empty(_publicados);
    }

    [Fact]
    public async Task Crear_NombreYDescripcionMuyLargos_Validacion()
    {
        AssertError(await Crear().EjecutarAsync(Nuevo(new string('x', 151)), 7, default), TipoError.Validacion);
        AssertError(await Crear().EjecutarAsync(Nuevo() with { Descripcion = new string('x', 501) }, 7, default), TipoError.Validacion);
    }

    [Fact]
    public async Task Crear_NombreRepetido_Conflicto()
    {
        Existente(nombre: "Camisa");
        AssertError(await Crear().EjecutarAsync(Nuevo("Camisa"), 7, default), TipoError.Conflicto);
    }

    [Fact]
    public async Task Crear_FechaDeIngreso_UsaLaFechaDeElSalvador_NoLaDelServidor()
    {
        // 19:00 del 9-nov en El Salvador = 01:00 UTC del 10-nov: "hoy" para el usuario es el 9
        var tarde = new FakeTimeProvider(new DateTimeOffset(2026, 11, 10, 1, 0, 0, TimeSpan.Zero));

        Assert.True((await Crear(tarde).EjecutarAsync(Nuevo("A", ingreso: new DateOnly(2026, 11, 9)), 7, default)).Exito);
        AssertError(await Crear(tarde).EjecutarAsync(Nuevo("B", ingreso: new DateOnly(2026, 11, 10)), 7, default), TipoError.Validacion);   // mañana para él
    }

    [Fact]
    public async Task Crear_FechaDeIngresoFutura_Validacion() =>
        AssertError(await Crear().EjecutarAsync(Nuevo(ingreso: Hoy.AddDays(1)), 7, default), TipoError.Validacion);

    // ---------- Clasificación (antes: 500 o se descartaba en silencio) ----------

    [Fact]
    public async Task Crear_SubcategoriaInexistente_Validacion_NoErrorDeBase()
    {
        var res = await Crear().EjecutarAsync(Nuevo(subcategoria: 99999), 7, default);

        AssertError(res, TipoError.Validacion);
        Assert.Contains("subcategoría", res.Error!.Mensaje);
        Assert.Equal(0, _uow.Guardados);
        Assert.Empty(_publicados);
    }

    [Fact]
    public async Task Crear_DosOpcionesDelMismoFiltro_Validacion()
    {
        var res = await Crear().EjecutarAsync(Nuevo(subcategoria: 5, opciones: new[] { 100, 101 }), 7, default);   // Negro y Blanco

        AssertError(res, TipoError.Validacion);
        Assert.Contains("una opción por filtro", res.Error!.Mensaje);
        Assert.Equal(0, _uow.Guardados);
    }

    [Theory]
    [InlineData(999999)]   // no existe
    [InlineData(300)]      // existe pero es de otra subcategoría
    public async Task Crear_OpcionInexistenteODeOtraSubcategoria_Validacion_NoSeDescartaEnSilencio(int opcion)
    {
        var res = await Crear().EjecutarAsync(Nuevo(subcategoria: 5, opciones: new[] { 100, opcion }), 7, default);

        AssertError(res, TipoError.Validacion);
        Assert.Equal(0, _uow.Guardados);
    }

    [Fact]
    public async Task Crear_OpcionesSinSubcategoria_Validacion() =>
        AssertError(await Crear().EjecutarAsync(Nuevo(subcategoria: null, opciones: new[] { 100 }), 7, default), TipoError.Validacion);

    [Fact]
    public async Task Crear_ConClasificacionValida_GuardaUnaOpcionPorFiltro()
    {
        var res = await Crear().EjecutarAsync(Nuevo(subcategoria: 5, opciones: new[] { 100, 200 }), 7, default);

        Assert.True(res.Exito);
        var producto = _repo.Productos.Single();
        Assert.Equal(5, producto.SubcategoriaId);
        Assert.Equal(2, producto.Opciones.Count);
    }

    [Fact]
    public async Task Crear_OpcionRepetidaEnLaLista_SeTrataComoUna()
    {
        var res = await Crear().EjecutarAsync(Nuevo(subcategoria: 5, opciones: new[] { 100, 100 }), 7, default);
        Assert.True(res.Exito);
        Assert.Single(_repo.Productos.Single().Opciones);
    }

    // ===================== Actualizar =====================

    [Fact]
    public async Task Actualizar_Exitoso_FijaLaVersion_Guarda_YPublicaUnEvento()
    {
        Existente();

        var res = await Actualizar().EjecutarAsync(1, Edicion(), 7, default);

        Assert.True(res.Exito);
        Assert.Equal(1, _repo.VersionesFijadas);
        Assert.Equal(25m, res.Valor!.PrecioVenta);
        var ev = Assert.Single(_publicados);
        Assert.Equal(TipoCambioServidor.Actualizada, ev.Tipo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Actualizar_SinRowVersion_Validacion_NoPisaElProducto(string? version)
    {
        var p = Existente();

        var res = await Actualizar().EjecutarAsync(1, Edicion(version: version), 7, default);

        AssertError(res, TipoError.Validacion);
        Assert.Contains("RowVersion", res.Error!.Mensaje);
        Assert.Equal(10m, p.PrecioVenta);   // intacto
        Assert.Equal(0, _uow.Guardados);
        Assert.Empty(_publicados);
    }

    [Theory]
    [InlineData("xx!!")]
    [InlineData("no es base64")]
    [InlineData("====")]
    public async Task Actualizar_RowVersionQueNoEsBase64_Validacion_NoErrorDeFormato(string version) =>
        AssertError(await Actualizar().EjecutarAsync(1, Edicion(version: version), 7, default), TipoError.Validacion);

    [Fact]
    public async Task Actualizar_Inexistente_NoEncontrado() =>
        AssertError(await Actualizar().EjecutarAsync(999, Edicion(), 7, default), TipoError.NoEncontrado);

    [Fact]
    public async Task Actualizar_ProductoInactivo_Regla()
    {
        Existente(activo: false);
        AssertError(await Actualizar().EjecutarAsync(1, Edicion(), 7, default), TipoError.Regla);
    }

    [Fact]
    public async Task Actualizar_NombreDeOtroProducto_Conflicto()
    {
        Existente(1, "Camisa");
        Existente(2, "Pantalón");

        AssertError(await Actualizar().EjecutarAsync(1, Edicion("Pantalón"), 7, default), TipoError.Conflicto);
    }

    [Fact]
    public async Task Actualizar_ConservandoSuPropioNombre_NoEsConflicto()
    {
        Existente(1, "Camisa");
        Assert.True((await Actualizar().EjecutarAsync(1, Edicion("Camisa"), 7, default)).Exito);
    }

    [Fact]
    public async Task Actualizar_SubcategoriaInexistente_YDosOpcionesDelMismoFiltro_Validacion()
    {
        Existente();

        AssertError(await Actualizar().EjecutarAsync(1, Edicion(subcategoria: 99999), 7, default), TipoError.Validacion);
        AssertError(await Actualizar().EjecutarAsync(1, Edicion(subcategoria: 5, opciones: new[] { 100, 101 }), 7, default), TipoError.Validacion);
        Assert.Equal(0, _uow.Guardados);
    }

    [Fact]
    public async Task Actualizar_QuitarLaSubcategoria_LimpiaLasOpciones()
    {
        var p = Existente();
        p.EstablecerClasificacion(5, new[] { (1, 100) });

        var res = await Actualizar().EjecutarAsync(1, Edicion(subcategoria: null), 7, default);

        Assert.True(res.Exito);
        Assert.Null(p.SubcategoriaId);
        Assert.Empty(p.Opciones);
    }

    [Fact]
    public async Task Actualizar_CambiarDeSubcategoria_ReemplazaLasOpciones()
    {
        var p = Existente();
        p.EstablecerClasificacion(5, new[] { (1, 100) });
        _repo.SubcategoriasActivas.Add(6);

        var res = await Actualizar().EjecutarAsync(1, Edicion(subcategoria: 6, opciones: new[] { 300 }), 7, default);

        Assert.True(res.Exito);
        Assert.Equal(6, p.SubcategoriaId);
        Assert.Equal(new[] { 300 }, p.Opciones.Select(o => o.OpcionFiltroId));
    }

    [Fact]
    public async Task Actualizar_FechaDeIngresoFutura_Validacion()
    {
        Existente();
        AssertError(await Actualizar().EjecutarAsync(1, Edicion() with { FechaIngreso = Hoy.AddDays(1) }, 7, default), TipoError.Validacion);
    }
}
