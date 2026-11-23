using GestionPedidos.Api.Seguridad;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GestionPedidos.Api.Tests;

/// <summary>La IP real solo se toma de X-Forwarded-For si la petición llega desde una red confiable (no necesitan base de datos).</summary>
public class ConfiguracionProxyTests
{
    private static async Task<string?> IpVistaAsync(string ipDelSocket, string? xForwardedFor, Dictionary<string, string?>? ajustes = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(ajustes ?? new()).Build();
        using var host = await new HostBuilder().ConfigureWebHost(w => w
            .UseTestServer()
            .ConfigureServices(s => s.AddProxyConfiable(config))
            .Configure(app =>
            {
                // La IP del socket la fija la prueba; luego actúa el middleware real.
                app.Use((ctx, next) => { ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ipDelSocket); return next(); });
                app.UseForwardedHeaders();
                app.Run(ctx => ctx.Response.WriteAsync(ctx.Connection.RemoteIpAddress?.ToString() ?? ""));
            })).StartAsync();

        var cliente = host.GetTestClient();
        if (xForwardedFor is not null) cliente.DefaultRequestHeaders.Add("X-Forwarded-For", xForwardedFor);
        return await cliente.GetStringAsync("/");
    }

    [Fact]
    public async Task ProxyEnRedPrivada_LaIpRealSaleDeXForwardedFor() =>
        Assert.Equal("203.0.113.9", await IpVistaAsync("172.18.0.5", "203.0.113.9"));

    [Fact]
    public async Task OrigenPublico_NoPuedeFalsearSuIp() =>
        Assert.Equal("198.51.100.7", await IpVistaAsync("198.51.100.7", "203.0.113.9"));

    [Fact]
    public async Task SinCabecera_SeQuedaConLaIpDelSocket() =>
        Assert.Equal("172.18.0.5", await IpVistaAsync("172.18.0.5", null));

    [Fact]
    public async Task RedesConfiablesPropias_SustituyenALasPorDefecto()
    {
        var ajustes = new Dictionary<string, string?> { ["ForwardedHeaders:RedesConfiables:0"] = "192.0.2.0/24" };
        Assert.Equal("172.18.0.5", await IpVistaAsync("172.18.0.5", "203.0.113.9", ajustes));   // privada, pero ya no confiable
        Assert.Equal("203.0.113.9", await IpVistaAsync("192.0.2.10", "203.0.113.9", ajustes));
    }

    [Fact]
    public void CidrInvalido_FallaConUnMensajeClaro()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ForwardedHeaders:RedesConfiables:0"] = "no-es-cidr" }).Build();
        var ex = Assert.Throws<InvalidOperationException>(() => ConfiguracionProxy.Aplicar(new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions(), config));
        Assert.Contains("no-es-cidr", ex.Message);
    }

    [Fact]
    public void ApagadoPorDefecto() =>
        Assert.False(ConfiguracionProxy.Habilitado(new ConfigurationBuilder().Build()));
}
