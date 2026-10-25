using System.Net.Http.Headers;
using System.Net.Http.Json;
using GestionPedidos.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.MsSql;

namespace GestionPedidos.Api.Tests;

/// <summary>Arranca la API real contra un SQL Server real en Docker (migraciones y administrador inicial incluidos).</summary>
public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string ClaveAdminInicial = "Admin_Inicial_Pruebas_1";
    public const string ClaveAdmin = "Admin_Segura_Pruebas_2026";

    private readonly MsSqlContainer _bd = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("Pruebas_Password_123!")
        .Build();

    public string ConexionBd => $"{_bd.GetConnectionString()};Database=GestionPedidosApi";

    public Task InitializeAsync() => _bd.StartAsync();
    async Task IAsyncLifetime.DisposeAsync() { await base.DisposeAsync(); await _bd.DisposeAsync(); }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // UseSetting (y no ConfigureAppConfiguration): el Program lee la configuración antes de que esta se aplique.
        var ajustes = new Dictionary<string, string>
        {
            ["ConnectionStrings:Default"] = ConexionBd,
            ["ConnectionStrings:Migraciones"] = ConexionBd,
            ["Jwt:SigningKey"] = "clave-de-pruebas-de-integracion-0123456789-abcdef",
            ["Admin:ClaveInicial"] = ClaveAdminInicial,
            ["Database:MigrarAlIniciar"] = "true",
            ["RateLimiting:LoginPorMinuto"] = "100000",
            ["RateLimiting:RegistroPorHora"] = "100000",
        };
        foreach (var (clave, valor) in ajustes) builder.UseSetting(clave, valor);
    }

    /// <summary>Inicia sesión y devuelve la sesión (lanza si falla).</summary>
    public async Task<SesionDto> LoginAsync(string usuario, string clave)
    {
        var r = await CreateClient().PostAsJsonAsync("/api/v1/auth/login", new LoginSolicitud(usuario, clave));
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<SesionDto>())!;
    }

    /// <summary>Cliente HTTP con el token de la sesión.</summary>
    public HttpClient ClienteCon(SesionDto sesion)
    {
        var c = CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sesion.AccessToken);
        return c;
    }

    /// <summary>Administrador con la contraseña ya cambiada (sesión plenamente operativa); las pruebas de una colección corren en serie.</summary>
    public async Task<HttpClient> AdminAsync()
    {
        var intento = await CreateClient().PostAsJsonAsync("/api/v1/auth/login", new LoginSolicitud("admin", ClaveAdmin));
        if (intento.IsSuccessStatusCode) return ClienteCon((await intento.Content.ReadFromJsonAsync<SesionDto>())!);

        var inicial = await LoginAsync("admin", ClaveAdminInicial);
        var cambio = await ClienteCon(inicial).PostAsJsonAsync("/api/v1/auth/cambiar-clave", new CambiarClaveSolicitud(ClaveAdminInicial, ClaveAdmin));
        cambio.EnsureSuccessStatusCode();
        return ClienteCon((await cambio.Content.ReadFromJsonAsync<SesionDto>())!);
    }
}

[CollectionDefinition(Nombre)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Nombre = "api";
}
