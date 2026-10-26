using GestionPedidos.Ui;
using GestionPedidos.Ui.Comun.Sesion;
using GestionPedidos.Web;
using GestionPedidos.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// La web llama a la API desde SU servidor (red de Docker): el navegador nunca ve el token ni necesita CORS.
builder.Services.AddGestionPedidosUi(new UiOptions
{
    ApiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:4390",
    EsEscritorio = false,
    RequiereAdminAcceso = false,
});
builder.Services.AddScoped<ISesionAlmacen, SesionAlmacenWeb>();

var app = builder.Build();

if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/error");
app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/health", () => Results.Ok(new { estado = "vivo" }));

// Las fotos de productos y categorías las entrega la API (que las lee del almacén de objetos). El navegador las pide a ESTE sitio
// (/imagenes/productos/CODIGO) y el servidor las trae de la API por la red interna: no hace falta exponer la API ni configurar CORS.
app.MapGet("/imagenes/{tipo}/{codigo}", async (string tipo, string codigo, HttpContext http, IHttpClientFactory fabrica, CancellationToken ct) =>
{
    using var peticion = new HttpRequestMessage(HttpMethod.Get, $"api/v1/imagenes/{Uri.EscapeDataString(tipo)}/{Uri.EscapeDataString(codigo)}");
    if (http.Request.Headers.IfNoneMatch.Count > 0) peticion.Headers.TryAddWithoutValidation("If-None-Match", http.Request.Headers.IfNoneMatch.ToString());
    using var respuesta = await fabrica.CreateClient("api").SendAsync(peticion, HttpCompletionOption.ResponseHeadersRead, ct);
    http.Response.StatusCode = (int)respuesta.StatusCode;
    if (!respuesta.IsSuccessStatusCode) return;   // 404 o 304: sin cuerpo
    http.Response.ContentType = respuesta.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
    foreach (var cabecera in new[] { "ETag", "Cache-Control" })
        if (respuesta.Headers.TryGetValues(cabecera, out var valores)) http.Response.Headers[cabecera] = valores.ToArray();
    if (respuesta.Headers.ETag is { } etag) http.Response.Headers.ETag = etag.ToString();
    await respuesta.Content.CopyToAsync(http.Response.Body, ct);
});
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Routes).Assembly);   // las páginas están en la biblioteca Ui

app.Run();
