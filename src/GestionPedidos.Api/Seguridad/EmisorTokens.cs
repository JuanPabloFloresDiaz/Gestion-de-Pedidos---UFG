using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GestionPedidos.Application.Seguridad;
using GestionPedidos.Contracts;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GestionPedidos.Api.Seguridad;

/// <summary>Emite el JWT HS256 de sesión (60 minutos, sin <i>refresh tokens</i>).</summary>
public sealed class EmisorTokens : IEmisorTokens
{
    /// <summary>Nombre de la claim con cada permiso.</summary>
    public const string ClaimPermiso = "permiso";
    /// <summary>Nombre de la claim que marca el cambio de clave obligatorio.</summary>
    public const string ClaimDebeCambiarClave = "debe_cambiar_clave";

    private readonly OpcionesJwt _opciones;
    private readonly TimeProvider _reloj;

    /// <summary>Crea el emisor.</summary>
    public EmisorTokens(IOptions<OpcionesJwt> opciones, TimeProvider reloj)
    {
        _opciones = opciones.Value; _reloj = reloj;
    }

    /// <inheritdoc/>
    public (string Token, DateTimeOffset ExpiraEn) Emitir(UsuarioActualDto usuario, bool debeCambiarClave)
    {
        var ahora = _reloj.GetUtcNow();
        var expira = ahora.AddMinutes(_opciones.MinutosDeVida);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Name, usuario.NombreUsuario),
            new("nombre_completo", usuario.NombreCompleto),
            new(ClaimTypes.Role, usuario.Rol),
        };
        claims.AddRange(usuario.Permisos.Select(p => new Claim(ClaimPermiso, p)));
        if (debeCambiarClave) claims.Add(new Claim(ClaimDebeCambiarClave, "true"));

        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opciones.SigningKey)), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(_opciones.Issuer, _opciones.Audience, claims,
            notBefore: ahora.UtcDateTime, expires: expira.UtcDateTime, signingCredentials: credenciales);
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expira);
    }
}
