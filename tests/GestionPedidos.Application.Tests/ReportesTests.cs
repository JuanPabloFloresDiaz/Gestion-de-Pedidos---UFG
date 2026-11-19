using GestionPedidos.Application.Common;
using GestionPedidos.Application.Reportes;
using GestionPedidos.Contracts;
using Microsoft.Extensions.Time.Testing;

namespace GestionPedidos.Application.Tests;

/// <summary>El caso de uso de reportes: valida entradas, elige la consulta correcta y nombra el archivo.</summary>
public class ReportesTests
{
    private sealed class RepoFake : IReportesRepository
    {
        public List<string> Llamadas { get; } = new();
        public (int? Usuario, DateTimeOffset? Desde, DateTimeOffset? Hasta, int Maximo)? Actividades { get; private set; }
        public int? Categoria { get; private set; }
        /// <summary>Si es <c>true</c>, la consulta responde «no existe» (<c>null</c>), como cuando piden un usuario o una categoría inexistente.</summary>
        public bool NoExiste { get; set; }

        private static TablaReporte Tabla(string titulo, DateTimeOffset ahora) =>
            new(titulo, "d", new[] { new ColumnaReporte("A") }, Array.Empty<IReadOnlyList<object?>>(), ahora);

        public Task<TablaReporte> ClientesPorZonaAsync(bool porDistrito, DateTimeOffset ahora, CancellationToken ct)
        { Llamadas.Add(porDistrito ? "clientes-distrito" : "clientes-departamento"); return Task.FromResult(Tabla("c", ahora)); }
        public Task<TablaReporte?> ProductosPorCategoriaAsync(int? categoriaId, DateTimeOffset ahora, CancellationToken ct)
        { Llamadas.Add("productos"); Categoria = categoriaId; return Task.FromResult<TablaReporte?>(NoExiste ? null : Tabla("p", ahora)); }
        public Task<TablaReporte> UsuariosPorRolAsync(DateTimeOffset ahora, CancellationToken ct) { Llamadas.Add("usuarios"); return Task.FromResult(Tabla("u", ahora)); }
        public Task<TablaReporte> PermisosPorRolAsync(DateTimeOffset ahora, CancellationToken ct) { Llamadas.Add("permisos"); return Task.FromResult(Tabla("pe", ahora)); }
        public Task<TablaReporte?> ActividadesPorUsuarioAsync(int? usuarioId, DateTimeOffset? desde, DateTimeOffset? hasta, int maximoFilas, DateTimeOffset ahora, CancellationToken ct)
        { Llamadas.Add("actividades"); Actividades = (usuarioId, desde, hasta, maximoFilas); return Task.FromResult<TablaReporte?>(NoExiste ? null : Tabla("a", ahora)); }
        public Task<TablaReporte> ProductosMasVendidosAsync(DateTimeOffset ahora, CancellationToken ct) { Llamadas.Add("vendidos"); return Task.FromResult(Tabla("v", ahora)); }
        public Task<TablaReporte> PedidosPorEstadoAsync(DateTimeOffset? desde, DateTimeOffset? hasta, DateTimeOffset ahora, CancellationToken ct)
        { Llamadas.Add("pedidos"); return Task.FromResult(Tabla("ped", ahora)); }
    }

    private sealed class ExportadorFake : IExportadorDeReportes
    {
        public FormatoReporte? Formato { get; private set; }
        public string? Acento { get; private set; }
        public ArchivoReporte Exportar(TablaReporte tabla, FormatoReporte formato, string? acento = null)
        {
            Formato = formato;
            Acento = acento;
            return new ArchivoReporte(new byte[] { 1 }, "x/y", "se-reemplaza");
        }
    }

    private sealed class OpcionesFake : IOpcionesDeReporte
    {
        public Task<IReadOnlyList<OpcionReporteDto>> UsuariosAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<OpcionReporteDto>>(new[] { new OpcionReporteDto(1, "Ana (ana)") });
        public Task<IReadOnlyList<OpcionReporteDto>> CategoriasAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<OpcionReporteDto>>(new[] { new OpcionReporteDto(9, "Ropa") });
    }

    // 03:30 UTC del 18-nov = 21:30 del 17-nov en El Salvador
    private static readonly DateTimeOffset Ahora = new(2026, 11, 18, 3, 30, 0, TimeSpan.Zero);
    private readonly RepoFake _repo = new();
    private readonly ExportadorFake _exportador = new();
    private GenerarReporte Caso() => new(_repo, _exportador, new FakeTimeProvider(Ahora), new OpcionesFake());

    [Theory]
    [InlineData("pdf", FormatoReporte.Pdf)]
    [InlineData("PDF", FormatoReporte.Pdf)]
    [InlineData("xlsx", FormatoReporte.Excel)]
    [InlineData("excel", FormatoReporte.Excel)]
    [InlineData(" csv ", FormatoReporte.Csv)]
    public void InterpretarFormato_AceptaPdfXlsxYCsv(string texto, FormatoReporte esperado) =>
        Assert.Equal(esperado, GenerarReporte.InterpretarFormato(texto));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("docx")]
    public async Task FormatoInvalido_EsValidacion_YNoConsultaNada(string? formato)
    {
        var r = await Caso().EjecutarAsync(TiposDeReporte.UsuariosPorRol, formato, new FiltrosReporte(), default);
        Assert.Equal(TipoError.Validacion, r.Error!.Tipo);
        Assert.Empty(_repo.Llamadas);
    }

    [Fact]
    public async Task TipoInexistente_EsNoEncontrado()
    {
        var r = await Caso().EjecutarAsync("secretos", "pdf", new FiltrosReporte(), default);
        Assert.Equal(TipoError.NoEncontrado, r.Error!.Tipo);
    }

    [Fact]
    public async Task ClientesPorZona_ElegirDistritoODepartamento_YRechazaOtraCosa()
    {
        await Caso().EjecutarAsync(TiposDeReporte.ClientesPorZona, "pdf", new FiltrosReporte(AgruparPor: "distrito"), default);
        await Caso().EjecutarAsync(TiposDeReporte.ClientesPorZona, "pdf", new FiltrosReporte(AgruparPor: "Departamento"), default);
        await Caso().EjecutarAsync(TiposDeReporte.ClientesPorZona, "pdf", new FiltrosReporte(), default);   // por defecto: departamento
        Assert.Equal(new[] { "clientes-distrito", "clientes-departamento", "clientes-departamento" }, _repo.Llamadas);

        var mala = await Caso().EjecutarAsync(TiposDeReporte.ClientesPorZona, "pdf", new FiltrosReporte(AgruparPor: "pais"), default);
        Assert.Equal(TipoError.Validacion, mala.Error!.Tipo);
    }

    [Fact]
    public async Task CadaTipo_LlamaALaConsultaQueLeCorresponde()
    {
        foreach (var tipo in new[] { TiposDeReporte.ProductosPorCategoria, TiposDeReporte.UsuariosPorRol, TiposDeReporte.PermisosPorRol, TiposDeReporte.ActividadesPorUsuario, TiposDeReporte.PedidosPorEstado, TiposDeReporte.ProductosMasVendidos })
            Assert.True((await Caso().EjecutarAsync(tipo, "csv", new FiltrosReporte(), default)).Exito);
        Assert.Equal(new[] { "productos", "usuarios", "permisos", "actividades", "pedidos", "vendidos" }, _repo.Llamadas);
        Assert.Equal(FormatoReporte.Csv, _exportador.Formato);
    }

    [Fact]
    public async Task Actividades_ElDiaFinalSeIncluyeCompleto_YHayUnMaximoDeFilas()
    {
        await Caso().EjecutarAsync(TiposDeReporte.ActividadesPorUsuario, "xlsx",
            new FiltrosReporte(UsuarioId: 7, Desde: new DateOnly(2026, 11, 1), Hasta: new DateOnly(2026, 11, 10)), default);

        var a = _repo.Actividades!.Value;
        Assert.Equal(7, a.Usuario);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), a.Desde);
        Assert.Equal(new DateTimeOffset(2026, 11, 11, 0, 0, 0, TimeSpan.Zero), a.Hasta);   // el 10 completo = hasta el 11 a las 00:00
        Assert.Equal(GenerarReporte.MaximoFilasActividades, a.Maximo);
    }

    [Fact]
    public async Task FechasInvertidas_SonValidacion()
    {
        var r = await Caso().EjecutarAsync(TiposDeReporte.ActividadesPorUsuario, "pdf",
            new FiltrosReporte(Desde: new DateOnly(2026, 11, 10), Hasta: new DateOnly(2026, 11, 1)), default);
        Assert.Equal(TipoError.Validacion, r.Error!.Tipo);
    }

    [Theory]
    [InlineData("pdf", "pdf")]
    [InlineData("xlsx", "xlsx")]
    [InlineData("csv", "csv")]
    public async Task ElNombreDelArchivoLlevaElTipoLaFechaDeElSalvadorYLaExtension(string formato, string extension)
    {
        var r = await Caso().EjecutarAsync(TiposDeReporte.UsuariosPorRol, formato, new FiltrosReporte(), default);
        Assert.Equal($"reporte-usuarios-por-rol-20261117-2130.{extension}", r.Valor!.NombreArchivo);   // 21:30 del 17-nov en El Salvador
    }

    // ---------- Filtros nuevos: categoría, usuario y color ----------

    [Fact]
    public async Task Productos_SinCategoria_EsElReporteCompleto_YConCategoria_SeLaPasaALaConsulta()
    {
        await Caso().EjecutarAsync(TiposDeReporte.ProductosPorCategoria, "pdf", new FiltrosReporte(), default);
        Assert.Null(_repo.Categoria);
        await Caso().EjecutarAsync(TiposDeReporte.ProductosPorCategoria, "pdf", new FiltrosReporte(CategoriaId: 5), default);
        Assert.Equal(5, _repo.Categoria);
    }

    [Theory]
    [InlineData(TiposDeReporte.ProductosPorCategoria, "La categoría no existe.")]
    [InlineData(TiposDeReporte.ActividadesPorUsuario, "El usuario no existe.")]
    public async Task UnaCategoriaOUnUsuarioQueNoExisten_SonNoEncontrado(string tipo, string mensaje)
    {
        _repo.NoExiste = true;
        var r = await Caso().EjecutarAsync(tipo, "pdf", new FiltrosReporte(UsuarioId: 99, CategoriaId: 99), default);
        Assert.Equal(TipoError.NoEncontrado, r.Error!.Tipo);
        Assert.Equal(mensaje, r.Error.Mensaje);
        Assert.Null(_exportador.Formato);   // no se llegó a exportar nada
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task UnaCategoriaOUnUsuarioNoPositivos_SonValidacion_YNoConsultanNada(int id)
    {
        var a = await Caso().EjecutarAsync(TiposDeReporte.ProductosPorCategoria, "pdf", new FiltrosReporte(CategoriaId: id), default);
        var b = await Caso().EjecutarAsync(TiposDeReporte.ActividadesPorUsuario, "pdf", new FiltrosReporte(UsuarioId: id), default);
        Assert.Equal(TipoError.Validacion, a.Error!.Tipo);
        Assert.Equal(TipoError.Validacion, b.Error!.Tipo);
        Assert.Empty(_repo.Llamadas);
    }

    [Theory]
    [InlineData("azul")]
    [InlineData("Turquesa")]
    [InlineData("grafito")]
    [InlineData(null)]
    [InlineData("")]
    public async Task ElColorValido_SeLeEntregaAlExportador(string? acento)
    {
        var r = await Caso().EjecutarAsync(TiposDeReporte.UsuariosPorRol, "pdf", new FiltrosReporte(Acento: acento), default);
        Assert.True(r.Exito);
        Assert.Equal(acento, _exportador.Acento);
    }

    [Fact]
    public async Task UnColorQueNoExiste_EsValidacion_YNoConsultaNada()
    {
        var r = await Caso().EjecutarAsync(TiposDeReporte.UsuariosPorRol, "pdf", new FiltrosReporte(Acento: "fucsia"), default);
        Assert.Equal(TipoError.Validacion, r.Error!.Tipo);
        Assert.Empty(_repo.Llamadas);
    }

    [Fact]
    public void LaPaleta_TieneLosSieteColoresDeLaAplicacion_YAclararMezclaConBlanco()
    {
        Assert.Equal(7, AcentosDeReporte.Ids.Count());
        Assert.Equal("#2F5BEA", AcentosDeReporte.Color("azul"));
        Assert.Equal("#2F5BEA", AcentosDeReporte.Color("no-existe"));   // por defecto
        Assert.Equal("#FFFFFF", AcentosDeReporte.Aclarar("#2F5BEA", 1));
        Assert.Equal("#2F5BEA", AcentosDeReporte.Aclarar("#2F5BEA", 0));
        Assert.Equal("#808080", AcentosDeReporte.Aclarar("#000000", 0.5));
    }

    [Fact]
    public async Task Opciones_DeActividadesSonUsuarios_DeProductosSonCategorias_YElRestoNoTiene()
    {
        var usuarios = await Caso().OpcionesAsync(TiposDeReporte.ActividadesPorUsuario, default);
        var categorias = await Caso().OpcionesAsync(TiposDeReporte.ProductosPorCategoria, default);
        var otro = await Caso().OpcionesAsync(TiposDeReporte.UsuariosPorRol, default);
        Assert.Equal("Ana (ana)", usuarios.Valor![0].Texto);
        Assert.Equal("Ropa", categorias.Valor![0].Texto);
        Assert.Equal(TipoError.NoEncontrado, otro.Error!.Tipo);
    }
}
