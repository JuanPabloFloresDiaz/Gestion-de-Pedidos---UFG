using Android.App;
using Android.Runtime;

namespace GestionPedidos.Desktop;

/// <summary>Aplicación de Android: crea la aplicación MAUI con la misma composición que el escritorio (<see cref="MauiProgram"/>).</summary>
[Application]
public class MainApplication : MauiApplication
{
	/// <summary>Constructor que exige Android.</summary>
	public MainApplication(IntPtr handle, JniHandleOwnership ownership) : base(handle, ownership)
	{
	}

	/// <inheritdoc/>
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
