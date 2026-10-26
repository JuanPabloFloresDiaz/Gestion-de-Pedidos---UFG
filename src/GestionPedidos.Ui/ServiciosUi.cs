using GestionPedidos.Ui.Comun.Api;
using GestionPedidos.Ui.Comun.Apariencia;
using GestionPedidos.Ui.Comun.Documentos;
using GestionPedidos.Ui.Comun.Eventos;
using GestionPedidos.Ui.Comun.Notificaciones;
using GestionPedidos.Ui.Comun.Sesion;
using GestionPedidos.Ui.Modulos.Catalogos;
using GestionPedidos.Ui.Modulos.Categorias;
using GestionPedidos.Ui.Modulos.Historial;
using GestionPedidos.Ui.Modulos.Roles;
using GestionPedidos.Ui.Modulos.Usuarios;
using GestionPedidos.Ui.Shell;
using GestionPedidos.Ui.Tienda;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GestionPedidos.Ui;

/// <summary>Registro de dependencias de la interfaz compartida (lo llaman Web y Desktop).</summary>
public static class ServiciosUi
{
    /// <summary>Registra servicios, ViewModels y el cliente de la API. El hospedaje debe registrar <see cref="ISesionAlmacen"/>.</summary>
    public static IServiceCollection AddGestionPedidosUi(this IServiceCollection servicios, UiOptions opciones)
    {
        servicios.AddSingleton(opciones);
        servicios.TryAddSingleton(TimeProvider.System);
        servicios.AddHttpClient("api", c => c.BaseAddress = new Uri(opciones.ApiBaseUrl.TrimEnd('/') + "/"));

        // ApiClient es Scoped (no "typed client") para compartir la SesionService del circuito/aplicación.
        servicios.AddScoped(sp => new ApiClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("api"),
            sp.GetRequiredService<SesionService>()));
        servicios.AddScoped<SesionService>();
        servicios.AddScoped<DocumentosService>();
        servicios.AddScoped<AvisosService>();
        servicios.AddScoped<AparienciaService>();
        servicios.AddScoped<IAvisos>(sp => sp.GetRequiredService<AvisosService>());
        servicios.AddScoped<Notificador>();

        // Módulos: servicio (publicador) + ViewModel por página (Transient: una instancia por pestaña)
        servicios.AddScoped<UsuariosService>();
        servicios.AddScoped<IPublicadorDeCambios>(sp => sp.GetRequiredService<UsuariosService>());
        servicios.AddTransient<UsuariosViewModel>();
        servicios.AddScoped<RolesService>();
        servicios.AddScoped<IPublicadorDeCambios>(sp => sp.GetRequiredService<RolesService>());
        servicios.AddTransient<RolesViewModel>();
        servicios.AddScoped<CategoriasService>();
        servicios.AddScoped<IPublicadorDeCambios>(sp => sp.GetRequiredService<CategoriasService>());
        servicios.AddTransient<CategoriasViewModel>();
        servicios.AddScoped<CatalogosService>();
        servicios.AddTransient<CatalogosViewModel>();
        servicios.AddScoped<HistorialService>();
        servicios.AddTransient<HistorialViewModel>();
        // Tienda pública: servicio de consultas y carrito (por visita) y un ViewModel por página
        servicios.AddScoped<TiendaService>();
        servicios.AddScoped<CarritoService>();
        servicios.AddTransient<CatalogoViewModel>();
        servicios.AddTransient<ProductoViewModel>();
        servicios.AddTransient<RegistroViewModel>();
        servicios.AddTransient<LoginViewModel>();
        servicios.AddTransient<CambiarClaveViewModel>();
        return servicios;
    }
}
