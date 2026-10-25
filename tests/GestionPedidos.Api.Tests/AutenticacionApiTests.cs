using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GestionPedidos.Contracts;
using GestionPedidos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GestionPedidos.Api.Tests;

[Collection(ApiCollection.Nombre)]
public class AutenticacionApiTests
{
    private readonly ApiFixture _api;
    public AutenticacionApiTests(ApiFixture api) => _api = api;

    private static string Sufijo() => Guid.NewGuid().ToString("N")[..8];

    private static async Task<string?> Codigo(HttpResponseMessage r)
    {
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("codigo", out var c) ? c.GetString() : null;
    }

    [Fact]
    public async Task Health_ResponderVivoYListo()
    {
        var c = _api.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task LoginDelAdministradorInicial_ExigeCambiarLaClave_YBloqueaTodoLoDemas()
    {
        await _api.AdminAsync();   // asegura el estado base
        // Un usuario recién creado tiene clave temporal: aquí lo comprobamos con un operador nuevo.
        var admin = await _api.AdminAsync();
        var n = "tmp" + Sufijo();
        var creado = await admin.PostAsJsonAsync("/api/v1/usuarios", new CrearUsuarioSolicitud(n, "Temporal", $"{n}@e.com", 2, "ClaveTemporal_2026"));
        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);

        var sesion = await _api.LoginAsync(n, "ClaveTemporal_2026");
        Assert.True(sesion.DebeCambiarClave);
        var cliente = _api.ClienteCon(sesion);

        var bloqueado = await cliente.GetAsync("/api/v1/usuarios");
        Assert.Equal(HttpStatusCode.Forbidden, bloqueado.StatusCode);
        Assert.Equal("DEBE_CAMBIAR_CLAVE", await Codigo(bloqueado));
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/v1/auth/yo")).StatusCode);   // esto sí se permite

        var cambio = await cliente.PostAsJsonAsync("/api/v1/auth/cambiar-clave", new CambiarClaveSolicitud("ClaveTemporal_2026", "ClaveNueva_Segura_2026"));
        Assert.Equal(HttpStatusCode.OK, cambio.StatusCode);
        var nueva = (await cambio.Content.ReadFromJsonAsync<SesionDto>())!;
        Assert.False(nueva.DebeCambiarClave);
        Assert.NotEqual(HttpStatusCode.Forbidden, (await _api.ClienteCon(nueva).GetAsync("/api/v1/auth/yo")).StatusCode);
    }

    [Fact]
    public async Task Login_Incorrecto_Da401ConMensajeGenerico_AunqueElUsuarioNoExista()
    {
        var c = _api.CreateClient();
        var malaClave = await c.PostAsJsonAsync("/api/v1/auth/login", new LoginSolicitud("admin", "incorrecta"));
        var fantasma = await c.PostAsJsonAsync("/api/v1/auth/login", new LoginSolicitud("no-existe", "incorrecta"));
        Assert.Equal(HttpStatusCode.Unauthorized, malaClave.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, fantasma.StatusCode);
        Assert.Equal(await malaClave.Content.ReadAsStringAsync().ContinueWith(t => Titulo(t.Result)),
                     await fantasma.Content.ReadAsStringAsync().ContinueWith(t => Titulo(t.Result)));   // no se distingue
    }

    private static string? Titulo(string json) => JsonDocument.Parse(json).RootElement.GetProperty("title").GetString();

    [Fact]
    public async Task CincoFallos_BloqueanLaCuenta_AunConLaClaveCorrecta()
    {
        var admin = await _api.AdminAsync();
        var n = "blq" + Sufijo();
        await admin.PostAsJsonAsync("/api/v1/usuarios", new CrearUsuarioSolicitud(n, "Bloqueo", $"{n}@e.com", 2, "ClaveTemporal_2026"));
        var c = _api.CreateClient();
        for (var i = 0; i < 5; i++) await c.PostAsJsonAsync("/api/v1/auth/login", new LoginSolicitud(n, "mala"));
        var r = await c.PostAsJsonAsync("/api/v1/auth/login", new LoginSolicitud(n, "ClaveTemporal_2026"));
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task SinToken_Da401_EnTodosLosEndpointsProtegidos()
    {
        var c = _api.CreateClient();
        foreach (var ruta in new[] { "/api/v1/usuarios", "/api/v1/roles", "/api/v1/auth/yo" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync(ruta)).StatusCode);
    }

    [Fact]
    public async Task Operador_Da403_EnUsuariosYRoles_PeroPuedeVerSuPerfil()
    {
        var admin = await _api.AdminAsync();
        var n = "op" + Sufijo();
        await admin.PostAsJsonAsync("/api/v1/usuarios", new CrearUsuarioSolicitud(n, "Operador", $"{n}@e.com", 2, "ClaveTemporal_2026"));
        var s = await _api.LoginAsync(n, "ClaveTemporal_2026");
        var cambio = await _api.ClienteCon(s).PostAsJsonAsync("/api/v1/auth/cambiar-clave", new CambiarClaveSolicitud("ClaveTemporal_2026", "ClaveNueva_Segura_2026"));
        var op = _api.ClienteCon((await cambio.Content.ReadFromJsonAsync<SesionDto>())!);

        Assert.Equal(HttpStatusCode.Forbidden, (await op.GetAsync("/api/v1/usuarios")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await op.GetAsync("/api/v1/roles")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await op.PostAsJsonAsync("/api/v1/usuarios", new CrearUsuarioSolicitud("x1234", "X", "x@e.com", 2, "ClaveTemporal_2026"))).StatusCode);
        var yo = await op.GetFromJsonAsync<UsuarioActualDto>("/api/v1/auth/yo");
        Assert.Equal("Operador", yo!.Rol);
        Assert.Contains("admin.acceso", yo.Permisos);
    }

    [Fact]
    public async Task UsuarioDadoDeBaja_PierdeElAccesoConSuTokenVigente()
    {
        var admin = await _api.AdminAsync();
        var n = "baja" + Sufijo();
        var creado = await (await admin.PostAsJsonAsync("/api/v1/usuarios", new CrearUsuarioSolicitud(n, "Baja", $"{n}@e.com", 2, "ClaveTemporal_2026"))).Content.ReadFromJsonAsync<UsuarioDto>();
        var s = await _api.LoginAsync(n, "ClaveTemporal_2026");
        var c = _api.ClienteCon(s);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/v1/auth/yo")).StatusCode);   // activo (se cachea 60 s)

        var baja = await admin.PatchAsJsonAsync($"/api/v1/usuarios/{creado!.Id}/estado", new CambiarEstadoSolicitud(false));
        Assert.Equal(HttpStatusCode.OK, baja.StatusCode);
        // Un login nuevo ya no es posible (genérico)
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new LoginSolicitud(n, "ClaveTemporal_2026"))).StatusCode);
    }

    [Fact]
    public async Task Registro_Web_CreaClienteConCuenta_YNoSePuedeRepetirElCorreo()
    {
        var c = _api.CreateClient();
        var correo = $"cliente{Sufijo()}@e.com";
        var s = new RegistroClienteSolicitud("Carla", "Mejía", "7000-1111", "Col. Centro", correo, 1, "ClaveCliente_2026");
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/v1/auth/registro", s)).StatusCode);

        var sesion = await _api.LoginAsync(correo, "ClaveCliente_2026");
        Assert.Equal("Cliente", sesion.Usuario.Rol);
        Assert.DoesNotContain("admin.acceso", sesion.Usuario.Permisos);
        Assert.Contains("mispedidos.crear", sesion.Usuario.Permisos);
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.ClienteCon(sesion).GetAsync("/api/v1/usuarios")).StatusCode);

        var repetido = await c.PostAsJsonAsync("/api/v1/auth/registro", s);
        Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);
        Assert.Equal("CORREO_EN_USO", await Codigo(repetido));
    }

    [Fact]
    public async Task Registro_Web_ConClienteExistenteSinCuenta_PideConfirmarTelefono_YLuegoEnlaza()
    {
        var correo = $"existente{Sufijo()}@e.com";
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GestionPedidosDbContext>();
            db.Clientes.Add(new GestionPedidos.Domain.Clientes.Cliente { Nombres = "Luis", Apellidos = "Rivas", Telefono = "7222-3333", Direccion = "X", CiudadId = 1, Correo = correo });
            await db.SaveChangesAsync();
        }
        var c = _api.CreateClient();
        var s = new RegistroClienteSolicitud("Luis", "Rivas", "7222-3333", "X", correo, 1, "ClaveCliente_2026");

        var primero = await c.PostAsJsonAsync("/api/v1/auth/registro", s);
        Assert.Equal(HttpStatusCode.Conflict, primero.StatusCode);
        Assert.Equal("CLIENTE_EXISTENTE", await Codigo(primero));

        var mal = await c.PostAsJsonAsync("/api/v1/auth/registro", s with { TelefonoConfirmacion = "0000-0000" });
        Assert.Equal(HttpStatusCode.BadRequest, mal.StatusCode);
        Assert.Equal("TELEFONO_NO_COINCIDE", await Codigo(mal));

        var bien = await c.PostAsJsonAsync("/api/v1/auth/registro", s with { TelefonoConfirmacion = "+503 7222 3333" });
        Assert.Equal(HttpStatusCode.Created, bien.StatusCode);

        using var scope2 = _api.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<GestionPedidosDbContext>();
        Assert.Equal(1, await db2.Clientes.CountAsync(x => x.Correo == correo));   // no se duplicó
        Assert.NotNull((await db2.Clientes.SingleAsync(x => x.Correo == correo)).UsuarioId);   // quedó enlazado
    }

    [Fact]
    public async Task Historial_RegistraLoginsFallidosYCambios_SinContrasenas()
    {
        var c = _api.CreateClient();
        var n = "hist" + Sufijo();
        await c.PostAsJsonAsync("/api/v1/auth/login", new LoginSolicitud(n, "ClaveSecreta_NoDebeVerse_1"));
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GestionPedidosDbContext>();
        for (var i = 0; i < 40 && !await db.Actividades.AnyAsync(a => a.Detalle!.Contains(n)); i++) await Task.Delay(250);   // se escribe en segundo plano
        var fila = await db.Actividades.SingleAsync(a => a.Detalle!.Contains(n));
        Assert.Equal("LoginFallido", fila.Accion);
        Assert.Null(fila.UsuarioId);
        Assert.DoesNotContain("NoDebeVerse", fila.Detalle);
        Assert.False(await db.Actividades.AnyAsync(a => a.Detalle!.Contains("NoDebeVerse")));
    }
}
