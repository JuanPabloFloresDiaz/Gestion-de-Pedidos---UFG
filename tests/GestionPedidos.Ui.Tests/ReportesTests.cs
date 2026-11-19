using System.Net;
using System.Net.Http.Headers;
using System.Text;
using GestionPedidos.Ui.Comun.Api;
using GestionPedidos.Ui.Comun.Reportes;
using GestionPedidos.Ui.Modulos.Catalogos;
using GestionPedidos.Ui.Modulos.Historial;
using Microsoft.JSInterop;

namespace GestionPedidos.Ui.Tests;

/// <summary>Descarga de reportes desde la interfaz: ruta, archivo, errores y entrega al navegador (sin navegador real).</summary>
public class ReportesTests
{
    /// <summary>Manejador que responde un archivo binario con su Content-Disposition.</summary>
    private sealed class ArchivoHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Peticion { get; private set; }
        public HttpStatusCode Estado { get; set; } = HttpStatusCode.OK;
        public string CuerpoError { get; set; } = "";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Peticion = request;
            var r = new HttpResponseMessage(Estado);
            if (Estado == HttpStatusCode.OK)
            {
                r.Content = new ByteArrayContent(new byte[] { 0x25, 0x50, 0x44, 0x46 });
                r.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                r.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = "reporte-usuarios-por-rol-20261117-2130.pdf" };
            }
            else r.Content = new StringContent(CuerpoError, Encoding.UTF8, "application/json");
            return Task.FromResult(r);
        }
    }

    private sealed class ModuloFalso : IJSObjectReference
    {
        public List<object?[]> Descargas { get; } = new();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        { Descargas.Add(args ?? Array.Empty<object?>()); return ValueTask.FromResult(default(TValue)!); }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class JsFalso : IJSRuntime
    {
        public ModuloFalso Modulo { get; } = new();
        public string? ModuloImportado { get; private set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            ModuloImportado = args?[0]?.ToString();
            return ValueTask.FromResult((TValue)(object)Modulo);
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }

    private static (Entorno, ArchivoHandler, JsFalso, ReportesService) Crear()
    {
        var e = new Entorno();
        var handler = new ArchivoHandler();
        var api = new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://api.test/") }, e.Sesion);
        var js = new JsFalso();
        return (e, handler, js, new ReportesService(api, js));
    }

    [Fact]
    public void Ruta_LlevaElFormato_EscapaLosValores_YOmiteLosVacios()
    {
        var ruta = ReportesService.Ruta("clientes-por-zona", "pdf", new Dictionary<string, string?>
        {
            ["agruparPor"] = "distrito", ["vacio"] = "", ["nulo"] = null, ["q"] = "a&b=c d",
        });

        Assert.Equal("/api/v1/reportes/clientes-por-zona?formato=pdf&agruparPor=distrito&q=a%26b%3Dc%20d", ruta);
        Assert.Equal("/api/v1/reportes/usuarios-por-rol?formato=csv", ReportesService.Ruta("usuarios-por-rol", "csv"));
    }

    [Fact]
    public async Task Descargar_PideElArchivoALaApi_YSeLoEntregaAlNavegadorConSuNombre()
    {
        var (_, handler, js, servicio) = Crear();

        var nombre = await servicio.DescargarAsync("usuarios-por-rol", FormatosDeReporte.Pdf);

        Assert.Equal("reporte-usuarios-por-rol-20261117-2130.pdf", nombre);
        Assert.Equal("/api/v1/reportes/usuarios-por-rol", handler.Peticion!.RequestUri!.AbsolutePath);
        Assert.Equal("./_content/GestionPedidos.Ui/js/descarga.js", js.ModuloImportado);
        var descarga = Assert.Single(js.Modulo.Descargas);
        Assert.Equal("reporte-usuarios-por-rol-20261117-2130.pdf", descarga[0]);
        Assert.Equal("application/pdf", descarga[1]);
        Assert.Equal(new byte[] { 0x25, 0x50, 0x44, 0x46 }, descarga[2]);
    }

    [Fact]
    public async Task Descargar_EnviaElTokenDeLaSesion()
    {
        var (e, handler, _, servicio) = Crear();
        await e.Sesion.IniciarAsync(Entorno.SesionDe("Administrador", new[] { "usuarios.ver" }, e.Reloj.GetUtcNow().AddHours(1)));

        await servicio.DescargarAsync("usuarios-por-rol", FormatosDeReporte.Csv);

        Assert.Equal("Bearer", handler.Peticion!.Headers.Authorization!.Scheme);
        Assert.Equal("token-Administrador", handler.Peticion.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task Descargar_SinPermiso_LanzaUnErrorEnEspanol_YNoEntregaNingunArchivo()
    {
        var (_, handler, js, servicio) = Crear();
        handler.Estado = HttpStatusCode.Forbidden;

        var ex = await Assert.ThrowsAsync<ApiException>(() => servicio.DescargarAsync("permisos-por-rol", FormatosDeReporte.Excel));

        Assert.Equal("No tiene permiso para esta acción.", ex.Message);
        Assert.Null(js.ModuloImportado);
    }

    [Fact]
    public async Task Descargar_ConError400_MuestraElMensajeDeLaApi()
    {
        var (_, handler, _, servicio) = Crear();
        handler.Estado = HttpStatusCode.BadRequest;
        handler.CuerpoError = "{\"title\":\"La fecha inicial no puede ser posterior a la final.\",\"codigo\":\"VALIDACION\"}";

        var ex = await Assert.ThrowsAsync<ApiException>(() => servicio.DescargarAsync("actividades-por-usuario", FormatosDeReporte.Pdf));

        Assert.Equal("La fecha inicial no puede ser posterior a la final.", ex.Message);
    }

    [Fact]
    public void Historial_ExportaConElRangoDeFechasQueSeEstaViendo()
    {
        var vm = new HistorialViewModel(new HistorialService(new Entorno().Api))
        {
            Desde = new DateOnly(2026, 11, 1), Hasta = new DateOnly(2026, 11, 17),
        };

        Assert.Equal("2026-11-01", vm.FiltrosDeReporte["desde"]);
        Assert.Equal("2026-11-17", vm.FiltrosDeReporte["hasta"]);
        vm.Desde = null;
        Assert.Null(vm.FiltrosDeReporte["desde"]);   // sin fecha no se envía (la ruta omite los vacíos)
    }

    [Fact]
    public void Catalogos_OfreceLosDosReportesDeClientesPorZona()
    {
        Assert.Equal("departamento", CatalogosViewModel.PorDepartamento["agruparPor"]);
        Assert.Equal("distrito", CatalogosViewModel.PorDistrito["agruparPor"]);
    }

    // ---------- Color, opción elegida y lista de opciones ----------

    [Fact]
    public void FiltrosDelBoton_AgregaElColorDeLaAplicacion_YLaOpcionElegida_SinPerderLosFiltrosDeLaPantalla()
    {
        var pantalla = new Dictionary<string, string?> { ["desde"] = "2026-11-01", ["hasta"] = null };

        var filtros = ReportesService.FiltrosDelBoton(pantalla, "usuarioId", 7, "rosa");

        Assert.Equal("7", filtros["usuarioId"]);
        Assert.Equal("rosa", filtros["acento"]);
        Assert.Equal("2026-11-01", filtros["desde"]);
        Assert.Single(pantalla, p => p.Key == "desde" && p.Value == "2026-11-01");   // no modifica el diccionario de la pantalla
        Assert.DoesNotContain("acento", pantalla.Keys);
        Assert.Equal("/api/v1/reportes/actividades-por-usuario?formato=pdf&desde=2026-11-01&usuarioId=7&acento=rosa",
            ReportesService.Ruta("actividades-por-usuario", "pdf", filtros));   // «hasta» vacío se omite
    }

    [Fact]
    public void FiltrosDelBoton_SinOpcionElegida_NoEnviaElFiltro_PeroSiElColor()
    {
        var filtros = ReportesService.FiltrosDelBoton(null, "categoriaId", null, "azul");
        Assert.DoesNotContain("categoriaId", filtros.Keys);   // «todas»: el reporte completo
        Assert.Equal("azul", filtros["acento"]);
        Assert.DoesNotContain("categoriaId", ReportesService.FiltrosDelBoton(null, null, 5, "azul").Keys);   // sin lista, ninguna opción
    }

    [Fact]
    public async Task Opciones_SePidenALaApiDelReporte_YSeLeen()
    {
        var e = new Entorno();
        var handler = new OpcionesHandler();
        var servicio = new ReportesService(new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://api.test/") }, e.Sesion), new JsFalso());
        await e.Sesion.IniciarAsync(Entorno.SesionDe("Administrador", new[] { "categorias.ver" }, e.Reloj.GetUtcNow().AddHours(1)));

        var opciones = await servicio.OpcionesAsync("productos-por-categoria");

        Assert.Equal("/api/v1/reportes/productos-por-categoria/opciones", handler.Ruta);
        Assert.Equal(new[] { "Ropa", "Hogar" }, opciones.Select(o => o.Texto));
        Assert.Equal(new[] { 1, 2 }, opciones.Select(o => o.Id));
    }

    private sealed class OpcionesHandler : HttpMessageHandler
    {
        public string? Ruta { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Ruta = request.RequestUri!.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("[{\"id\":1,\"texto\":\"Ropa\"},{\"id\":2,\"texto\":\"Hogar\"}]", Encoding.UTF8, "application/json") });
        }
    }
}
