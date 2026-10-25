namespace GestionPedidos.Desktop;

/// <summary>Aplicación MAUI: crea la ventana principal que aloja la interfaz Blazor compartida.</summary>
public partial class App : Application
{
	/// <summary>Inicializa la aplicación y asigna la página que contiene el BlazorWebView.</summary>
	public App()
	{
		InitializeComponent();
		MainPage = new MainPage();
	}

	/// <summary>Configura el título y el tamaño de la ventana de escritorio.</summary>
	protected override Window CreateWindow(IActivationState? activationState)
	{
		var ventana = base.CreateWindow(activationState);
		ventana.Title = "Gestión de Pedidos — UFG";
		ventana.Width = 1280;
		ventana.Height = 800;
		ventana.MinimumWidth = 900;
		ventana.MinimumHeight = 600;
		return ventana;
	}
}
