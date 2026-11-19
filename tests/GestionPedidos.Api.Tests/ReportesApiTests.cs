using System.Net;
using System.Net.Http.Json;
using System.Text;
using GestionPedidos.Contracts;

namespace GestionPedidos.Api.Tests;

/// <summary>Reportes (PDF, Excel y CSV): permisos, formatos, validación y que no se filtre ningún dato sensible.</summary>
[Collection(ApiCollection.Nombre)]
public class ReportesApiTests
{
    private readonly ApiFixture _api;
    public ReportesApiTests(ApiFixture api) => _api = api;

    private static readonly string[] Tipos =
        { "clientes-por-zona", "productos-por-categoria", "usuarios-por-rol", "permisos-por-rol", "actividades-por-usuario", "pedidos-por-estado" };

    [Theory]
    [InlineData("pdf", "application/pdf", "%PDF-")]
    [InlineData("xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "PK")]
    [InlineData("csv", "text/csv", "﻿")]   // el CSV empieza con la marca BOM (EF BB BF) para que Excel lea bien las tildes
    public async Task ElAdministrador_DescargaLosReportes_EnElFormatoPedido(string formato, string tipoContenido, string inicio)
    {
        var admin = await _api.AdminAsync();
        foreach (var tipo in Tipos)
        {
            var r = await admin.GetAsync($"/api/v1/reportes/{tipo}?formato={formato}");
            Assert.True(r.StatusCode == HttpStatusCode.OK, $"{tipo}.{formato}: {(int)r.StatusCode}");
            Assert.StartsWith(tipoContenido, r.Content.Headers.ContentType!.ToString());
            Assert.Matches($@"^reporte-{tipo}-\d{{8}}-\d{{4}}\.{formato}$", r.Content.Headers.ContentDisposition!.FileName!.Trim('"'));
            var bytes = await r.Content.ReadAsByteArrayAsync();
            Assert.True(bytes.Length > 8, $"{tipo}.{formato} vacío");   // en una base vacía el CSV es solo el encabezado: corto, pero no vacío
            Assert.StartsWith(inicio, Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 8)));
        }
    }

    [Fact]
    public async Task ClientesPorZona_AceptaDepartamentoYDistrito_YRechazaOtraAgrupacion()
    {
        var admin = await _api.AdminAsync();
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/reportes/clientes-por-zona?formato=csv&agruparPor=departamento")).StatusCode);
        var distrito = await admin.GetAsync("/api/v1/reportes/clientes-por-zona?formato=csv&agruparPor=distrito");
        Assert.Contains("Distrito", Encoding.UTF8.GetString(await distrito.Content.ReadAsByteArrayAsync()));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/reportes/clientes-por-zona?formato=csv&agruparPor=planeta")).StatusCode);
    }

    [Fact]
    public async Task FormatoFaltanteOInvalido_Da400_YUnReporteInexistente_Da404()
    {
        var admin = await _api.AdminAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/reportes/usuarios-por-rol")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/reportes/usuarios-por-rol?formato=docx")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/reportes/no-existe?formato=pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/reportes/actividades-por-usuario?formato=csv&desde=2026-11-10&hasta=2026-11-01")).StatusCode);
    }

    [Fact]
    public async Task SinSesion401_YUnClienteDeLaTienda403_EnTodosLosReportes()
    {
        var correo = $"rep{Guid.NewGuid():N}"[..14] + "@ejemplo.com";
        var registro = await _api.CreateClient().PostAsJsonAsync("/api/v1/auth/registro",
            new RegistroClienteSolicitud("Cliente", "De Reportes", "78945612", "Colonia Escalón, casa 1", correo, 1, "ClaveCliente_2026"));
        Assert.Equal(HttpStatusCode.Created, registro.StatusCode);
        var cliente = _api.ClienteCon(await _api.LoginAsync(correo, "ClaveCliente_2026"));
        var anonimo = _api.CreateClient();

        foreach (var tipo in Tipos)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync($"/api/v1/reportes/{tipo}?formato=csv")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync($"/api/v1/reportes/{tipo}?formato=csv")).StatusCode);
        }
    }

    [Fact]
    public async Task ElReporteDeUsuarios_NuncaTrae_ClavesNiHashes()
    {
        var admin = await _api.AdminAsync();
        var csv = Encoding.UTF8.GetString(await (await admin.GetAsync("/api/v1/reportes/usuarios-por-rol?formato=csv")).Content.ReadAsByteArrayAsync());
        Assert.Contains("admin", csv);
        Assert.DoesNotContain("AQAAAA", csv);   // prefijo de los hashes de PasswordHasher
        Assert.DoesNotContain("ClaveHash", csv);
    }

    [Fact]
    public async Task ElReporteDeActividades_FiltraPorUsuario_YElDeUnUsuarioInexistenteEs404()
    {
        var admin = await _api.AdminAsync();
        var yo = (await admin.GetFromJsonAsync<UsuarioActualDto>("/api/v1/auth/yo"))!;
        var propio = Encoding.UTF8.GetString(await (await admin.GetAsync($"/api/v1/reportes/actividades-por-usuario?formato=csv&usuarioId={yo.Id}")).Content.ReadAsByteArrayAsync());
        Assert.Contains("admin", propio);
        Assert.Contains("Detalle de actividades", propio);   // el detalle va en su propio bloque, con el título del bloque

        var ajeno = await admin.GetAsync("/api/v1/reportes/actividades-por-usuario?formato=csv&usuarioId=987654");
        Assert.Equal(HttpStatusCode.NotFound, ajeno.StatusCode);   // un usuario que no existe no es un reporte vacío
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/reportes/actividades-por-usuario?formato=csv&usuarioId=0")).StatusCode);
    }

    [Fact]
    public async Task ElReporteDeProductos_SeAcotaAUnaCategoria_YUnaInexistenteEs404()
    {
        var admin = await _api.AdminAsync();
        // La base de pruebas parte vacía: se crea una categoría con una subcategoría para poder pedir su reporte.
        var nombre = "Cat" + Guid.NewGuid().ToString("N")[..8];
        var creada = await admin.PostAsJsonAsync("/api/v1/categorias", new GuardarClasificacionSolicitud(nombre, "Para el reporte"));
        Assert.Equal(HttpStatusCode.Created, creada.StatusCode);
        var categoria = (await creada.Content.ReadFromJsonAsync<CategoriaDto>(new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)))!;
        var opciones = (await admin.GetFromJsonAsync<List<OpcionReporteDto>>("/api/v1/reportes/productos-por-categoria/opciones"))!;
        Assert.Contains(opciones, o => o.Id == categoria.Id && o.Texto == nombre);
        var uno = await admin.GetAsync($"/api/v1/reportes/productos-por-categoria?formato=csv&categoriaId={categoria.Id}");
        Assert.Equal(HttpStatusCode.OK, uno.StatusCode);
        var csv = Encoding.UTF8.GetString(await uno.Content.ReadAsByteArrayAsync());
        Assert.Contains("Categoría: " + nombre, csv);
        Assert.Contains("Subcategorías y filtros", csv);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/reportes/productos-por-categoria?formato=csv&categoriaId=987654")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/reportes/productos-por-categoria?formato=csv&categoriaId=-1")).StatusCode);
    }

    [Theory]
    [InlineData("azul")]
    [InlineData("rosa")]
    public async Task ElColorDelReporte_SeAceptaEnPdfYExcel_YUnoInventadoEs400(string acento)
    {
        var admin = await _api.AdminAsync();
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/reportes/usuarios-por-rol?formato=pdf&acento={acento}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/reportes/usuarios-por-rol?formato=xlsx&acento={acento}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/reportes/usuarios-por-rol?formato=pdf&acento=fucsia")).StatusCode);
    }

    [Fact]
    public async Task LasOpciones_SoloLasTienenLosReportesQueSePuedenAcotar_ConSuPermiso()
    {
        var admin = await _api.AdminAsync();
        var usuarios = (await admin.GetFromJsonAsync<List<OpcionReporteDto>>("/api/v1/reportes/actividades-por-usuario/opciones"))!;
        Assert.Contains(usuarios, u => u.Texto.EndsWith("(admin)"));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/reportes/usuarios-por-rol/opciones")).StatusCode);

        // Sin sesión 401; un cliente de la tienda 403 (ven el mismo permiso que el reporte).
        var correo = $"opc{Guid.NewGuid():N}"[..14] + "@ejemplo.com";
        Assert.Equal(HttpStatusCode.Created, (await _api.CreateClient().PostAsJsonAsync("/api/v1/auth/registro",
            new RegistroClienteSolicitud("Cliente", "De Opciones", "78945612", "Colonia Escalón, casa 1", correo, 1, "ClaveCliente_2026"))).StatusCode);
        var cliente = _api.ClienteCon(await _api.LoginAsync(correo, "ClaveCliente_2026"));
        foreach (var tipo in new[] { "actividades-por-usuario", "productos-por-categoria" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await _api.CreateClient().GetAsync($"/api/v1/reportes/{tipo}/opciones")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync($"/api/v1/reportes/{tipo}/opciones")).StatusCode);
        }
    }
}
