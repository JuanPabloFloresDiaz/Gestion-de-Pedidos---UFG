using GestionPedidos.Domain.Clientes;

namespace GestionPedidos.Domain.Tests;

/// <summary>Pruebas unitarias de las reglas del agregado Cliente ([Clientes.md]).</summary>
public class ClienteTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Crear_NormalizaCampos_YNaceActivo()
    {
        var c = Cliente.Crear(" Juan Carlos ", " Pérez López ", " 7123-4567 ", " Colonia Médica ", " JUAN@Correo.COM ", 1);

        Assert.Equal("Juan Carlos", c.Nombres);
        Assert.Equal("Pérez López", c.Apellidos);
        Assert.Equal("Juan Carlos Pérez López", c.NombreCompleto);
        Assert.Equal("7123-4567", c.Telefono);
        Assert.Equal("Colonia Médica", c.Direccion);
        Assert.Equal("juan@correo.com", c.Correo);
        Assert.Equal(1, c.CiudadId);
        Assert.True(c.Activo);
        Assert.Null(c.EliminadoEn);
    }

    [Fact]
    public void Crear_ConCorreoVacio_AsignaNull()
    {
        var c = Cliente.Crear("Carlos", "Gómez", "7890-1234", "San Salvador", "  ", 2);
        Assert.Null(c.Correo);
    }

    [Fact]
    public void Actualizar_ModificaCamposCorrectamente()
    {
        var c = Cliente.Crear("Ana", "López", "7000-0000", "Calle 1", null, 1);
        c.Actualizar(" Ana María ", " López Rivas ", " 2255-7788 ", " Av. Los Próceres ", " ana@empresa.com ", 5);

        Assert.Equal("Ana María", c.Nombres);
        Assert.Equal("López Rivas", c.Apellidos);
        Assert.Equal("2255-7788", c.Telefono);
        Assert.Equal("Av. Los Próceres", c.Direccion);
        Assert.Equal("ana@empresa.com", c.Correo);
        Assert.Equal(5, c.CiudadId);
    }

    [Fact]
    public void CambiarEstado_Desactivar_AplicaBajaLogicaYGuardaFecha()
    {
        var c = Cliente.Crear("Pedro", "Martínez", "7111-2222", "Calle Antigua", null, 1);

        c.CambiarEstado(false, Ahora);

        Assert.False(c.Activo);
        Assert.Equal(Ahora, c.EliminadoEn);
    }

    [Fact]
    public void CambiarEstado_Reactivar_LimpiaFechaDeBaja()
    {
        var c = Cliente.Crear("Pedro", "Martínez", "7111-2222", "Calle Antigua", null, 1);
        c.CambiarEstado(false, Ahora);

        c.CambiarEstado(true, Ahora.AddDays(1));

        Assert.True(c.Activo);
        Assert.Null(c.EliminadoEn);
    }

    [Theory]
    [InlineData("7123-4567", true)]
    [InlineData("+503 7123 4567", true)]
    [InlineData("2222-3333", true)]
    [InlineData("+503-2222-3333", true)]
    [InlineData("71234567", true)]
    [InlineData("1234", false)]              // muy corto
    [InlineData("123456789012345678901", false)] // más de 20 caracteres
    [InlineData("7123-ABCD", false)]         // letras inválidas
    [InlineData("", false)]
    [InlineData(null, false)]
    public void EsTelefonoValido_ValidaSegunRegla(string? telefono, bool esperado)
    {
        Assert.Equal(esperado, Cliente.EsTelefonoValido(telefono));
    }
}
