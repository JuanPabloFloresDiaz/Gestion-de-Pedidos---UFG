using GestionPedidos.Domain.Seguridad;

namespace GestionPedidos.Domain.Tests;

public class UsuarioTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private static Usuario Nuevo() => Usuario.Crear("ana", " Ana Pérez ", " ANA@Ejemplo.com ", "hash", 2, debeCambiarClave: true);

    [Fact]
    public void Crear_NormalizaCorreoYNombre_YNaceActivoConCambioObligatorio()
    {
        var u = Nuevo();
        Assert.Equal("ana@ejemplo.com", u.Correo);
        Assert.Equal("Ana Pérez", u.NombreCompleto);
        Assert.True(u.Activo);
        Assert.True(u.DebeCambiarClave);
    }

    [Fact]
    public void RegistrarFalloAcceso_CuatroFallos_NoBloquea()
    {
        var u = Nuevo();
        for (var i = 0; i < Usuario.MaximoIntentosFallidos - 1; i++) u.RegistrarFalloAcceso(Ahora);
        Assert.False(u.EstaBloqueado(Ahora));
        Assert.Equal(4, u.IntentosFallidos);
    }

    [Fact]
    public void RegistrarFalloAcceso_QuintoFallo_BloqueaQuinceMinutos()
    {
        var u = Nuevo();
        for (var i = 0; i < Usuario.MaximoIntentosFallidos; i++) u.RegistrarFalloAcceso(Ahora);
        Assert.True(u.EstaBloqueado(Ahora));
        Assert.True(u.EstaBloqueado(Ahora.AddMinutes(14)));
        Assert.False(u.EstaBloqueado(Ahora.AddMinutes(15)));
        Assert.Equal(0, u.IntentosFallidos);   // el contador se reinicia al bloquear
    }

    [Fact]
    public void RegistrarAccesoExitoso_LimpiaFallosYBloqueo()
    {
        var u = Nuevo();
        for (var i = 0; i < Usuario.MaximoIntentosFallidos; i++) u.RegistrarFalloAcceso(Ahora);
        u.RegistrarAccesoExitoso(Ahora.AddMinutes(20));
        Assert.False(u.EstaBloqueado(Ahora.AddMinutes(20)));
        Assert.Equal(Ahora.AddMinutes(20), u.UltimoAcceso);
    }

    [Fact]
    public void CambiarClave_QuitaLaObligacionDeCambiarla()
    {
        var u = Nuevo();
        u.CambiarClave("hash-nuevo");
        Assert.Equal("hash-nuevo", u.ClaveHash);
        Assert.False(u.DebeCambiarClave);
    }

    [Fact]
    public void CambiarEstado_Reactivar_LimpiaBloqueo()
    {
        var u = Nuevo();
        for (var i = 0; i < Usuario.MaximoIntentosFallidos; i++) u.RegistrarFalloAcceso(Ahora);
        u.CambiarEstado(false, Ahora);
        u.CambiarEstado(true, Ahora);
        Assert.True(u.Activo);
        Assert.False(u.EstaBloqueado(Ahora));
    }

    [Fact]
    public void DarDeBaja_GuardaLaFechaDeEliminadoSuave_YReactivarLaBorra()
    {
        var u = Nuevo();
        Assert.Null(u.EliminadoEn);
        u.CambiarEstado(false, Ahora);
        Assert.False(u.Activo);
        Assert.Equal(Ahora, u.EliminadoEn);
        u.CambiarEstado(true, Ahora.AddDays(1));
        Assert.True(u.Activo);
        Assert.Null(u.EliminadoEn);
    }

    [Fact]
    public void DarDeBajaDosVeces_ConservaLaFechaOriginal()
    {
        var u = Nuevo();
        u.CambiarEstado(false, Ahora);
        u.CambiarEstado(false, Ahora.AddDays(5));
        Assert.Equal(Ahora, u.EliminadoEn);
    }
}
