using System.Text.Json;
using GestionPedidos.Ui;
using GestionPedidos.Ui.Comun.Sesion;
using Microsoft.Extensions.Logging;

namespace GestionPedidos.Desktop;

/// <summary>Punto de composición del escritorio: registra la interfaz compartida y lo propio de este hospedaje.</summary>
public static class MauiProgram
{
	/// <summary>Crea la aplicación MAUI.</summary>
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fuentes => fuentes.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"));

		builder.Services.AddMauiBlazorWebView();

		// La interfaz es la misma que la de la web; aquí solo se indica que este hospedaje es el escritorio
		// (solo entra el personal con permiso admin.acceso) y dónde está la API. Nunca hay cadena de conexión a la base.
		builder.Services.AddGestionPedidosUi(new UiOptions
		{
			ApiBaseUrl = LeerUrlDeLaApi(),
			EsEscritorio = true,
			RequiereAdminAcceso = true,
		});
		builder.Services.AddSingleton<ISesionAlmacen, SesionAlmacenMemoria>();   // el token vive solo en memoria

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif
		return builder.Build();
	}

	// Orden: variable de entorno GESTIONPEDIDOS_API_URL → appsettings.json junto al .exe → http://localhost:4390
	private static string LeerUrlDeLaApi()
	{
		var entorno = Environment.GetEnvironmentVariable("GESTIONPEDIDOS_API_URL");
		if (!string.IsNullOrWhiteSpace(entorno)) return entorno;
		try
		{
			var ruta = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
			if (File.Exists(ruta))
			{
				using var doc = JsonDocument.Parse(File.ReadAllText(ruta));
				if (doc.RootElement.TryGetProperty("Api", out var api) && api.TryGetProperty("BaseUrl", out var url)
					&& !string.IsNullOrWhiteSpace(url.GetString())) return url.GetString()!;
			}
		}
		catch (Exception ex) when (ex is IOException or JsonException) { /* se usa el valor por defecto */ }
		return "http://localhost:4390";
	}
}
