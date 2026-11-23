using GestionPedidos.Application.Seguridad;
using GestionPedidos.Infrastructure.Seguridad;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace GestionPedidos.Infrastructure.Tests;

/// <summary>Pruebas del hasher real de contraseñas (no necesitan base de datos).</summary>
public class HasherClaveTests
{
    private readonly HasherClave _hasher = new();

    [Fact]
    public void HashPropio_EsCorrectoYEstaAlDia()
    {
        var hash = _hasher.Hashear("Clave_Valida_123");
        Assert.Equal(ResultadoVerificacion.Correcta, _hasher.Verificar(hash, "Clave_Valida_123"));
    }

    [Fact]
    public void ClaveIncorrecta_EsIncorrecta() =>
        Assert.Equal(ResultadoVerificacion.Incorrecta, _hasher.Verificar(_hasher.Hashear("Clave_Valida_123"), "otra"));

    [Fact]
    public void HashConMenosIteraciones_EsCorrectoPeroObsoleto()
    {
        // Simula un hash creado antes de subir el costo del algoritmo.
        var viejo = new PasswordHasher<string>(Options.Create(new PasswordHasherOptions { IterationCount = 1_000 }));
        var hash = viejo.HashPassword("usuario", "Clave_Valida_123");

        Assert.Equal(ResultadoVerificacion.CorrectaConHashObsoleto, _hasher.Verificar(hash, "Clave_Valida_123"));
        Assert.Equal(ResultadoVerificacion.Incorrecta, _hasher.Verificar(hash, "otra"));
    }
}
