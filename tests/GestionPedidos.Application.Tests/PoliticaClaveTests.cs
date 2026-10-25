using GestionPedidos.Application.Autenticacion;

namespace GestionPedidos.Application.Tests;

public class PoliticaClaveTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("corta123")]
    public void Validar_ClaveCorta_Falla(string? clave) => Assert.NotNull(PoliticaClave.Validar(clave));

    [Fact]
    public void Validar_ClaveDeDiezCaracteres_Pasa() => Assert.Null(PoliticaClave.Validar("abcdefghij"));

    [Fact]
    public void Validar_MasDe128Caracteres_Falla() => Assert.NotNull(PoliticaClave.Validar(new string('a', 129)));

    [Fact]
    public void Validar_IgualAlUsuarioOAlCorreo_Falla()
    {
        Assert.NotNull(PoliticaClave.Validar("administrador", "Administrador"));
        Assert.NotNull(PoliticaClave.Validar("ana@ejemplo.com", "ana", "ANA@ejemplo.com"));
    }
}
