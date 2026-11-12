using System.Net;
using System.Net.Http.Json;
using GestionPedidos.Contracts;

namespace GestionPedidos.Api.Tests;

/// <summary>
/// Endpoints de clientes contra la API real y SQL Server ([Clientes.md §4 y §7]): permisos, códigos HTTP,
/// búsqueda, concurrencia optimista y baja lógica.
/// </summary>
[Collection(ApiCollection.Nombre)]
public class ClientesApiTests
{
    private readonly ApiFixture _api;
    public ClientesApiTests(ApiFixture api) => _api = api;

    private static string Sufijo() => Guid.NewGuid().ToString("N")[..8];

    private async Task<HttpClient> OperadorAsync()
    {
        var admin = await _api.AdminAsync();
        var n = "op" + Sufijo();
        await admin.PostAsJsonAsync("/api/v1/usuarios", new CrearUsuarioSolicitud(n, "Operador", $"{n}@e.com", 2, "ClaveTemporal_2026"));
        var s = await _api.LoginAsync(n, "ClaveTemporal_2026");
        var cambio = await _api.ClienteCon(s).PostAsJsonAsync("/api/v1/auth/cambiar-clave", new CambiarClaveSolicitud("ClaveTemporal_2026", "ClaveNueva_Segura_2026"));
        return _api.ClienteCon((await cambio.Content.ReadFromJsonAsync<SesionDto>())!);
    }

    private static async Task<int> CiudadAsync(HttpClient c)
    {
        var deptos = (await c.GetFromJsonAsync<List<DepartamentoDto>>("/api/v1/departamentos"))!;
        return (await c.GetFromJsonAsync<List<CiudadDto>>($"/api/v1/departamentos/{deptos[0].Id}/ciudades"))![0].Id;
    }

    private static CrearClienteSolicitud Nuevo(int ciudad, string? apellidos = null, string? correo = null, string telefono = "7123-4567") =>
        new("Ana", apellidos ?? "Prueba" + Sufijo(), telefono, "Calle 1", correo, ciudad);

    private static async Task<ClienteDto> CrearAsync(HttpClient c, CrearClienteSolicitud s)
    {
        var r = await c.PostAsJsonAsync("/api/v1/clientes", s);
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<ClienteDto>())!;
    }

    private static ActualizarClienteSolicitud Edicion(ClienteDto c, string? version, string nombres = "Ana María") =>
        new(nombres, c.Apellidos, c.Telefono, c.Direccion, c.Correo, c.CiudadId, version);

    // ===================== Permisos =====================

    [Fact]
    public async Task SinSesion_Da401_EnTodosLosEndpoints()
    {
        var anonimo = _api.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/v1/clientes")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/v1/clientes/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsJsonAsync("/api/v1/clientes", Nuevo(1))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PatchAsJsonAsync("/api/v1/clientes/1/estado", new CambiarEstadoSolicitud(false))).StatusCode);
    }

    [Fact]
    public async Task Operador_NoPuedeDarDeBaja_Da403()
    {
        var admin = await _api.AdminAsync();
        var c = await CrearAsync(admin, Nuevo(await CiudadAsync(admin)));
        var op = await OperadorAsync();

        Assert.Equal(HttpStatusCode.OK, (await op.GetAsync("/api/v1/clientes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await op.PatchAsJsonAsync($"/api/v1/clientes/{c.Id}/estado", new CambiarEstadoSolicitud(false))).StatusCode);
    }

    // ===================== Registrar =====================

    [Fact]
    public async Task Registrar_Valido_Da201_ConLocation_YClienteActivo()
    {
        var admin = await _api.AdminAsync();
        var r = await admin.PostAsJsonAsync("/api/v1/clientes", Nuevo(await CiudadAsync(admin), correo: $"c{Sufijo()}@e.com"));

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        Assert.NotNull(r.Headers.Location);
        var c = (await r.Content.ReadFromJsonAsync<ClienteDto>())!;
        Assert.True(c.Activo);
        Assert.False(c.TieneCuentaWeb);
        Assert.NotEmpty(c.Departamento);
    }

    [Theory]
    [InlineData("--------")]
    [InlineData("++++++++")]
    [InlineData("1234")]
    [InlineData("7123-ABCD")]
    public async Task Registrar_ConTelefonoInvalido_Da400(string telefono)
    {
        var admin = await _api.AdminAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/clientes", Nuevo(await CiudadAsync(admin), telefono: telefono))).StatusCode);
    }

    [Fact]
    public async Task Registrar_ConCuerpoVacioOCamposNulos_Da400_NoError500()
    {
        var admin = await _api.AdminAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/clientes", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"))).StatusCode);
        var nulos = await admin.PostAsJsonAsync("/api/v1/clientes", new { nombres = (string?)null, apellidos = (string?)null, telefono = (string?)null, direccion = (string?)null, ciudadId = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, nulos.StatusCode);
    }

    [Fact]
    public async Task Registrar_CiudadInexistente_Da422()
    {
        var admin = await _api.AdminAsync();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsJsonAsync("/api/v1/clientes", Nuevo(999999))).StatusCode);
    }

    [Fact]
    public async Task Registrar_CorreoDuplicado_Da409_SinImportarMayusculasNiEspacios()
    {
        var admin = await _api.AdminAsync();
        var ciudad = await CiudadAsync(admin);
        var correo = $"dup{Sufijo()}@e.com";
        await CrearAsync(admin, Nuevo(ciudad, correo: correo));

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/clientes", Nuevo(ciudad, correo: correo.ToUpperInvariant()))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/clientes", Nuevo(ciudad, correo: $" {correo} "))).StatusCode);
    }

    // ===================== Actualizar y concurrencia =====================

    [Fact]
    public async Task Actualizar_ConVersionVigente_Da200_YConVersionVieja_Da409()
    {
        var admin = await _api.AdminAsync();
        var c = await CrearAsync(admin, Nuevo(await CiudadAsync(admin)));

        var ok = await admin.PutAsJsonAsync($"/api/v1/clientes/{c.Id}", Edicion(c, c.RowVersion));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("Ana María", (await ok.Content.ReadFromJsonAsync<ClienteDto>())!.Nombres);

        var vieja = await admin.PutAsJsonAsync($"/api/v1/clientes/{c.Id}", Edicion(c, c.RowVersion, "Otro nombre"));
        Assert.Equal(HttpStatusCode.Conflict, vieja.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("###no-base64###")]
    public async Task Actualizar_ConVersionAusenteOBasura_Da400_NuncaSobrescribeNiDa500(string? version)
    {
        var admin = await _api.AdminAsync();
        var c = await CrearAsync(admin, Nuevo(await CiudadAsync(admin)));

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/clientes/{c.Id}", Edicion(c, version))).StatusCode);
        Assert.Equal("Ana", (await admin.GetFromJsonAsync<ClienteDto>($"/api/v1/clientes/{c.Id}"))!.Nombres);   // no cambió
    }

    [Fact]
    public async Task Actualizar_Inexistente_Da404()
    {
        var admin = await _api.AdminAsync();
        var c = await CrearAsync(admin, Nuevo(await CiudadAsync(admin)));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync("/api/v1/clientes/999999", Edicion(c, c.RowVersion))).StatusCode);
    }

    // ===================== Baja lógica =====================

    [Fact]
    public async Task Baja_Reactivacion_YEdicionDeInactivo()
    {
        var admin = await _api.AdminAsync();
        var c = await CrearAsync(admin, Nuevo(await CiudadAsync(admin)));

        var baja = await admin.PatchAsJsonAsync($"/api/v1/clientes/{c.Id}/estado", new CambiarEstadoSolicitud(false));
        var dadoDeBaja = (await baja.Content.ReadFromJsonAsync<ClienteDto>())!;
        Assert.Equal(HttpStatusCode.OK, baja.StatusCode);
        Assert.False(dadoDeBaja.Activo);
        Assert.NotNull(dadoDeBaja.EliminadoEn);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PutAsJsonAsync($"/api/v1/clientes/{c.Id}", Edicion(c, dadoDeBaja.RowVersion))).StatusCode);

        var reactivado = (await (await admin.PatchAsJsonAsync($"/api/v1/clientes/{c.Id}/estado", new CambiarEstadoSolicitud(true))).Content.ReadFromJsonAsync<ClienteDto>())!;
        Assert.True(reactivado.Activo);
        Assert.Null(reactivado.EliminadoEn);
    }

    // ===================== Búsqueda y paginación =====================

    [Fact]
    public async Task Buscar_PorNombreYApellidoJuntos_SinDistinguirAcentosNiMayusculas()
    {
        var admin = await _api.AdminAsync();
        var marca = "Zz" + Sufijo();
        await CrearAsync(admin, Nuevo(await CiudadAsync(admin), apellidos: $"{marca} López"));

        async Task<int> Total(string q) => (await admin.GetFromJsonAsync<PaginaDto<ClienteDto>>($"/api/v1/clientes?q={Uri.EscapeDataString(q)}"))!.Total;

        Assert.Equal(1, await Total($"Ana {marca}"));        // nombre + apellido
        Assert.Equal(1, await Total($"{marca} lopez"));      // sin tilde
        Assert.Equal(1, await Total(marca.ToUpperInvariant()));
        Assert.Equal(0, await Total($"Luis {marca}"));       // todas las palabras deben coincidir
    }

    [Fact]
    public async Task Buscar_FiltraPorEstado_Pagina_YTopeDeTamano()
    {
        var admin = await _api.AdminAsync();
        var ciudad = await CiudadAsync(admin);
        var marca = "Pg" + Sufijo();
        for (var i = 0; i < 3; i++) await CrearAsync(admin, Nuevo(ciudad, apellidos: $"{marca}{i}"));

        var p1 = (await admin.GetFromJsonAsync<PaginaDto<ClienteDto>>($"/api/v1/clientes?q={marca}&pagina=1&tamanoPagina=2"))!;
        Assert.Equal(3, p1.Total);
        Assert.Equal(2, p1.Elementos.Count);
        Assert.Single((await admin.GetFromJsonAsync<PaginaDto<ClienteDto>>($"/api/v1/clientes?q={marca}&pagina=2&tamanoPagina=2"))!.Elementos);
        Assert.Equal(0, (await admin.GetFromJsonAsync<PaginaDto<ClienteDto>>($"/api/v1/clientes?q={marca}&activo=false"))!.Total);
        Assert.Equal(100, (await admin.GetFromJsonAsync<PaginaDto<ClienteDto>>("/api/v1/clientes?tamanoPagina=5000"))!.TamanoPagina);
    }

    [Fact]
    public async Task Buscar_ConTextoMalicioso_NoRompeNiDevuelveNada()
    {
        var admin = await _api.AdminAsync();
        var r = await admin.GetAsync("/api/v1/clientes?q=%27%3B%20DROP%20TABLE%20Clientes%3B%20--");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(0, (await r.Content.ReadFromJsonAsync<PaginaDto<ClienteDto>>())!.Total);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/clientes")).StatusCode);
    }

    [Fact]
    public async Task Obtener_Inexistente_Da404()
    {
        var admin = await _api.AdminAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/clientes/999999")).StatusCode);
    }

    [Fact]
    public async Task Listar_SinFiltroDeEstado_SoloActivos_ConIncluirInactivos_Todos()
    {
        var admin = await _api.AdminAsync();
        var marca = "Es" + Sufijo();
        var activo = await CrearAsync(admin, Nuevo(await CiudadAsync(admin), apellidos: marca + "A"));
        var inactivo = await CrearAsync(admin, Nuevo(await CiudadAsync(admin), apellidos: marca + "B"));
        await admin.PatchAsJsonAsync($"/api/v1/clientes/{inactivo.Id}/estado", new CambiarEstadoSolicitud(false));

        async Task<PaginaDto<ClienteDto>> Lista(string extra) => (await admin.GetFromJsonAsync<PaginaDto<ClienteDto>>($"/api/v1/clientes?q={marca}{extra}"))!;

        var porDefecto = await Lista("");
        Assert.Equal(activo.Id, Assert.Single(porDefecto.Elementos).Id);
        Assert.Equal(2, (await Lista("&incluirInactivos=true")).Total);
        Assert.Equal(inactivo.Id, Assert.Single((await Lista("&activo=false")).Elementos).Id);
    }
}
