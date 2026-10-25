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
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Routes).Assembly);   // las páginas están en la biblioteca Ui

app.Run();
