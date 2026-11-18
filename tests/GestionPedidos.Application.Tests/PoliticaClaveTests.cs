using GestionPedidos.Application.Autenticacion;

namespace GestionPedidos.Application.Tests;

public class PoliticaClaveTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("corta123")]
    [InlineData("abcdefghij")]       // 10 caracteres: era el mínimo anterior
    [InlineData("abcdefghijklm")]    // 13 caracteres: a uno del mínimo
    public void Validar_ClaveCorta_Falla(string? clave) => Assert.NotNull(PoliticaClave.Validar(clave));

    [Fact]
    public void Validar_ClaveDeCatorceCaracteres_Pasa() => Assert.Null(PoliticaClave.Validar("abcdefghijklmn"));

    [Fact]
    public void ElMinimoEsCatorce() => Assert.Equal(14, PoliticaClave.Minimo);

    [Fact]
    public void Validar_MasDe128Caracteres_Falla() => Assert.NotNull(PoliticaClave.Validar(new string('a', 129)));

    [Fact]
    public void Validar_IgualAlUsuarioOAlCorreo_Falla()
    {
        Assert.NotNull(PoliticaClave.Validar("administrador", "Administrador"));
        Assert.NotNull(PoliticaClave.Validar("ana@ejemplo.com", "ana", "ANA@ejemplo.com"));
    }
}
