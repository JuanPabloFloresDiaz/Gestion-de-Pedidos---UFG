using GestionPedidos.Api.Errores;
using GestionPedidos.Api.Seguridad;
using GestionPedidos.Application.Autenticacion;
using GestionPedidos.Application.Common;
using GestionPedidos.Contracts;
using GestionPedidos.Application.Seguridad;

namespace GestionPedidos.Api.Modulos.Auth;

/// <summary>Endpoints de autenticación bajo <c>/api/v1/auth</c> ([AutenticacionYPermisos.md §2]).</summary>
public static class AuthEndpoints
{
    /// <summary>Registra el grupo de rutas de autenticación.</summary>
    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/auth").WithTags("Autenticación");

        g.MapPost("/login", async (LoginSolicitud s, AutenticarUsuario caso, CancellationToken ct) =>
                (await caso.EjecutarAsync(s, ct)).AHttp(Results.Ok))
            .AllowAnonymous().RequireRateLimiting("login");

        g.MapPost("/registro", async (RegistroClienteSolicitud s, RegistrarClienteWeb caso, CancellationToken ct) =>
            {
                var r = await caso.EjecutarAsync(s, ct);
                return r.Exito ? Results.StatusCode(StatusCodes.Status201Created) : r.Error!.AProblema();
            })
            .AllowAnonymous().RequireRateLimiting("registro");

        // Devuelve una sesión nueva: el token anterior aún marca "debe cambiar la clave".
        g.MapPost("/cambiar-clave", async (CambiarClaveSolicitud s, HttpContext http, CambiarClave caso,
                ObtenerUsuarioActual actual, IEmisorTokens tokens, CancellationToken ct) =>
            {
                var id = http.User.IdUsuario();
                var r = await caso.EjecutarAsync(id, s, ct);
                if (!r.Exito) return r.Error!.AProblema();
                var yo = await actual.EjecutarAsync(id, ct);
                if (!yo.Exito) return yo.Error!.AProblema();
                var (token, expira) = tokens.Emitir(yo.Valor!, debeCambiarClave: false);
                return Results.Ok(new SesionDto(token, expira, false, yo.Valor!));
            })
            .RequireAuthorization();

        g.MapGet("/yo", async (HttpContext http, ObtenerUsuarioActual caso, CancellationToken ct) =>
                (await caso.EjecutarAsync(http.User.IdUsuario(), ct)).AHttp(Results.Ok))
            .RequireAuthorization();
        return app;
    }
}
