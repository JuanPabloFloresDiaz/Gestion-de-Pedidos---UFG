using System.Security.Claims;
using GestionPedidos.Domain.Seguridad;
using Microsoft.AspNetCore.Authorization;

namespace GestionPedidos.Api.Seguridad;

/// <summary>Requisito de autorización: el usuario debe tener un permiso concreto.</summary>
/// <param name="Codigo">Código del permiso (por ejemplo <c>usuarios.crear</c>).</param>
public sealed record PermisoRequirement(string Codigo) : IAuthorizationRequirement;

/// <summary>Evalúa un <see cref="PermisoRequirement"/> contra las claims <c>permiso</c> del token.</summary>
public sealed class PermisoHandler : AuthorizationHandler<PermisoRequirement>
{
    /// <inheritdoc/>
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermisoRequirement requirement)
    {
        if (context.User.HasClaim(EmisorTokens.ClaimPermiso, requirement.Codigo)) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

/// <summary>Extensiones de autorización por permisos.</summary>
public static class AutorizacionExtensions
{
    /// <summary>Registra una política por cada permiso del catálogo (el nombre de la política es el código).</summary>
    public static IServiceCollection AddPoliticasDePermisos(this IServiceCollection servicios)
    {
        servicios.AddSingleton<IAuthorizationHandler, PermisoHandler>();
        servicios.AddAuthorization(o =>
        {
            foreach (var (codigo, _) in CatalogoPermisos.Todos)
                o.AddPolicy(codigo, p => p.RequireAuthenticatedUser().AddRequirements(new PermisoRequirement(codigo)));
        });
        return servicios;
    }

    /// <summary>Exige un permiso en un endpoint.</summary>
    public static TBuilder RequierePermiso<TBuilder>(this TBuilder builder, string permiso) where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(permiso);

    /// <summary>Identificador del usuario autenticado (claim <c>sub</c>).</summary>
    public static int IdUsuario(this ClaimsPrincipal usuario) =>
        int.Parse(usuario.FindFirstValue("sub") ?? throw new InvalidOperationException("Token sin claim sub."));
}
