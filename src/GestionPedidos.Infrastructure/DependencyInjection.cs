using GestionPedidos.Application.Autenticacion;
using GestionPedidos.Application.Catalogo;
using GestionPedidos.Application.Catalogos;
using GestionPedidos.Application.Roles;
using GestionPedidos.Application.Common;
using GestionPedidos.Application.Eventos;
using GestionPedidos.Application.Historial;
using GestionPedidos.Application.Seguridad;
using GestionPedidos.Application.Usuarios;
using GestionPedidos.Infrastructure.Historial;
using GestionPedidos.Infrastructure.Persistence;
using GestionPedidos.Infrastructure.Persistence.Repositories;
using GestionPedidos.Infrastructure.Persistence.Seed;
using GestionPedidos.Infrastructure.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GestionPedidos.Infrastructure;

/// <summary>Registro de dependencias de la aplicación y de la infraestructura.</summary>
public static class DependencyInjection
{
    /// <summary>Registra casos de uso, repositorios, EF Core, eventos e historial.</summary>
    /// <param name="servicios">Colección de servicios.</param>
    /// <param name="conexion">Cadena de conexión de la aplicación (usuario de mínimo privilegio).</param>
    public static IServiceCollection AddGestionPedidos(this IServiceCollection servicios, string conexion)
    {
        servicios.TryAddSingleton(TimeProvider.System);
        servicios.AddDbContext<GestionPedidosDbContext>(o => o.UseSqlServer(conexion, sql => sql.EnableRetryOnFailure()));

        servicios.AddScoped<IUnitOfWork, UnitOfWork>();
        servicios.AddScoped<IUsuarioRepository, UsuarioRepository>();
        servicios.AddScoped<IRolRepository, RolRepository>();
        servicios.AddScoped<IRegistroClienteRepository, RegistroClienteRepository>();
        servicios.AddScoped<ICatalogoGeoRepository, CatalogoGeoRepository>();
        servicios.AddScoped<IActividadRepository, ActividadRepository>();
        servicios.AddScoped<IClasificacionRepository, ClasificacionRepository>();
        servicios.AddSingleton<IHasherClave, HasherClave>();
        servicios.AddScoped<AdministradorInicial>();

        // Casos de uso (uno por clase)
        servicios.AddScoped<AutenticarUsuario>();
        servicios.AddScoped<CambiarClave>();
        servicios.AddScoped<ObtenerUsuarioActual>();
        servicios.AddScoped<RegistrarClienteWeb>();
        servicios.AddScoped<ListarUsuarios>();
        servicios.AddScoped<ObtenerUsuario>();
        servicios.AddScoped<CrearUsuario>();
        servicios.AddScoped<ActualizarUsuario>();
        servicios.AddScoped<CambiarEstadoUsuario>();
        servicios.AddScoped<EliminarUsuario>();
        servicios.AddScoped<ListarRoles>();
        servicios.AddScoped<ListarPermisos>();
        servicios.AddScoped<ActualizarPermisosDeRol>();
        servicios.AddScoped<ListarDepartamentos>();
        servicios.AddScoped<ListarCiudades>();
        servicios.AddScoped<ListarActividades>();
        servicios.AddScoped<ObtenerArbolDeCatalogo>();
        servicios.AddScoped<ObtenerFiltrosDeSubcategoria>();
        servicios.AddScoped<GestionarCategorias>();
        servicios.AddScoped<GestionarSubcategorias>();
        servicios.AddScoped<GestionarFiltros>();
        servicios.AddScoped<GestionarOpciones>();

        // Eventos: una publicadora única y el historial como suscriptor (se registra en un solo lugar)
        servicios.AddSingleton<EventosAplicacion>();
        servicios.AddSingleton<RegistroActividadesEnSegundoPlano>();
        servicios.AddSingleton<IRegistroActividades>(sp => sp.GetRequiredService<RegistroActividadesEnSegundoPlano>());
        servicios.AddHostedService(sp => sp.GetRequiredService<RegistroActividadesEnSegundoPlano>());
        servicios.AddSingleton<HistorialSubscriber>();
        return servicios;
    }
}
