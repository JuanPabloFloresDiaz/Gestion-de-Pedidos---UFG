using GestionPedidos.Application.Seguridad;
using Microsoft.AspNetCore.Identity;

namespace GestionPedidos.Infrastructure.Seguridad;

/// <summary>Hash de contraseñas con <see cref="PasswordHasher{TUser}"/> (PBKDF2 con sal, estándar de Microsoft).</summary>
public sealed class HasherClave : IHasherClave
{
    private readonly PasswordHasher<string> _hasher = new();
    private readonly string _hashFalso;

    /// <summary>Crea el hasher y precalcula un hash falso para igualar tiempos.</summary>
    public HasherClave() => _hashFalso = _hasher.HashPassword("falso", Guid.NewGuid().ToString("N"));

    /// <inheritdoc/>
    public string Hashear(string clave) => _hasher.HashPassword("usuario", clave);

    /// <inheritdoc/>
    public bool Verificar(string hash, string clave) =>
        _hasher.VerifyHashedPassword("usuario", hash, clave) is not PasswordVerificationResult.Failed;

    /// <inheritdoc/>
    public void GastarTiempo() => _hasher.VerifyHashedPassword("falso", _hashFalso, "no-es-la-clave");
}
