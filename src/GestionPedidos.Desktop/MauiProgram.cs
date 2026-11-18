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

		// La interfaz es la misma que la de la web; aquí solo se indica qué hospedaje es y dónde está la API.
		// Nunca hay cadena de conexión a la base.
		builder.Services.AddGestionPedidosUi(new UiOptions
		{
			ApiBaseUrl = LeerUrlDeLaApi(),
#if ANDROID
			// CELULAR: se comporta como la web (la tienda para el cliente y, con su cuenta, la administración para el personal).
			EsEscritorio = false,
			RequiereAdminAcceso = false,
#else
			// ESCRITORIO: solo entra el personal con permiso admin.acceso.
			EsEscritorio = true,
			RequiereAdminAcceso = true,
#endif
		});
		builder.Services.AddSingleton<ISesionAlmacen, SesionAlmacenMemoria>();   // el token vive solo en memoria

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif
		return builder.Build();
	}

	// Escritorio: variable de entorno GESTIONPEDIDOS_API_URL → appsettings.json junto al .exe → http://localhost:4390
	// Android:    appsettings.android.json dentro del APK (por defecto http://10.0.2.2:4390, el equipo anfitrión visto
	//             desde el emulador). Con un celular real se pone ahí la IP del equipo en la red (ver docs/Equipo/PruebaMovil.md).
	private static string LeerUrlDeLaApi()
	{
		var entorno = Environment.GetEnvironmentVariable("GESTIONPEDIDOS_API_URL");
		if (!string.IsNullOrWhiteSpace(entorno)) return entorno;
		try
		{
			var json = LeerAppSettings();
			if (json is not null)
			{
				using var doc = JsonDocument.Parse(json);
				if (doc.RootElement.TryGetProperty("Api", out var api) && api.TryGetProperty("BaseUrl", out var url)
					&& !string.IsNullOrWhiteSpace(url.GetString())) return url.GetString()!;
			}
		}
		catch (Exception ex) when (ex is IOException or JsonException) { /* se usa el valor por defecto */ }
		return PorDefecto;
	}

#if ANDROID
	private const string PorDefecto = "http://10.0.2.2:4390";

	// El archivo viaja como activo del APK (MauiAsset); se lee con el administrador de activos de Android.
	private static string? LeerAppSettings()
	{
		using var flujo = Android.App.Application.Context.Assets!.Open("appsettings.json");
		using var lector = new StreamReader(flujo);
		return lector.ReadToEnd();
	}
#else
	private const string PorDefecto = "http://localhost:4390";

	private static string? LeerAppSettings()
	{
		var ruta = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
		return File.Exists(ruta) ? File.ReadAllText(ruta) : null;
	}
#endif
}
