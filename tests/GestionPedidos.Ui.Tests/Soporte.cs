using System.Net;
using System.Text;
using System.Text.Json;
using GestionPedidos.Contracts;
using GestionPedidos.Ui;
using GestionPedidos.Ui.Comun.Api;
using GestionPedidos.Ui.Comun.Sesion;
using Microsoft.Extensions.Time.Testing;

namespace GestionPedidos.Ui.Tests;

/// <summary>Manejador HTTP falso: responde lo que cada prueba programa, sin red.</summary>
internal sealed class ManejadorFalso : HttpMessageHandler
{
    public List<HttpRequestMessage> Peticiones { get; } = new();
    /// <summary>Cuerpo de cada petición, leído al recibirla (el contenido se libera tras el envío).</summary>
    public List<string> Cuerpos { get; } = new();
    public Func<HttpRequestMessage, (HttpStatusCode Estado, object? Cuerpo)> Responder { get; set; } = _ => (HttpStatusCode.OK, null);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Peticiones.Add(request);
        Cuerpos.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
        var (estado, cuerpo) = Responder(request);
        var r = new HttpResponseMessage(estado);
        if (cuerpo is not null) r.Content = new StringContent(JsonSerializer.Serialize(cuerpo, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json");
        return r;
    }
}

/// <summary>Arma la interfaz sin navegador ni servidor: reloj falso, almacén en memoria y API simulada.</summary>
internal sealed class Entorno
{
    public FakeTimeProvider Reloj { get; } = new(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero));
    public ManejadorFalso Manejador { get; } = new();
    public SesionAlmacenMemoria Almacen { get; } = new();
    public SesionService Sesion { get; }
    public ApiClient Api { get; }
    public UiOptions Opciones { get; } = new();

    public Entorno()
    {
        Sesion = new SesionService(Almacen, Reloj);
        Api = new ApiClient(new HttpClient(Manejador) { BaseAddress = new Uri("http://api.test/") }, Sesion);
    }

    public static SesionDto SesionDe(string rol, string[] permisos, DateTimeOffset expira, bool debeCambiar = false) =>
        new("token-" + rol, expira, debeCambiar, new UsuarioActualDto(1, rol.ToLowerInvariant(), rol, rol, permisos));
}
