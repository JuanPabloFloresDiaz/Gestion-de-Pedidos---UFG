using GestionPedidos.Application.Imagenes;

namespace GestionPedidos.Application.Tests;

public class ImagenesTests
{
    [Fact]
    public void Claves_SeArmanConElCodigo_YDeLaClaveSeRecuperaElCodigo()
    {
        Assert.Equal("productos/ROP-CAM-001.jpg", ClavesDeImagen.Clave(TipoImagen.Producto, "ROP-CAM-001", "jpg"));
        Assert.Equal("categorias/ROP.png", ClavesDeImagen.Clave(TipoImagen.Categoria, "ROP", "png"));
        Assert.Equal("ROP-CAM-001", ClavesDeImagen.CodigoDe("productos/ROP-CAM-001.jpg"));
        Assert.Equal("ROP", ClavesDeImagen.CodigoDe("categorias/ROP.webp"));
        Assert.Null(ClavesDeImagen.CodigoDe("productos/archivo con espacios.jpg"));
        Assert.Null(ClavesDeImagen.CodigoDe("productos/.jpg"));
    }

    [Theory]
    [InlineData("ROP-CAM-001", true)]
    [InlineData("PRD_000123", true)]
    [InlineData("../secreto", false)]
    [InlineData("a/b", false)]
    [InlineData("con espacio", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("ESTE-CODIGO-ES-DEMASIADO-LARGO-PARA-SER-VALIDO", false)]
    public void Codigo_SoloAceptaLetrasNumerosGuionYGuionBajo(string? codigo, bool valido) => Assert.Equal(valido, ClavesDeImagen.CodigoValido(codigo));

    [Theory]
    [InlineData("productos", TipoImagen.Producto)]
    [InlineData("categorias", TipoImagen.Categoria)]
    public void Tipo_DeLaCarpeta(string carpeta, TipoImagen esperado) => Assert.Equal(esperado, ClavesDeImagen.Tipo(carpeta));

    [Fact]
    public void Tipo_DeOtraCarpeta_EsNulo() { Assert.Null(ClavesDeImagen.Tipo("usuarios")); Assert.Null(ClavesDeImagen.Tipo(null)); }

    [Fact]
    public void Detectar_ElFormatoPorLosPrimerosBytes_NoPorElNombre()
    {
        Assert.Equal(("jpg", "image/jpeg"), GestionarImagenes.Detectar(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0 }));
        Assert.Equal(("png", "image/png"), GestionarImagenes.Detectar(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }));
        Assert.Equal(("webp", "image/webp"), GestionarImagenes.Detectar("RIFF\0\0\0\0WEBPVP8 "u8.ToArray()));
        Assert.Null(GestionarImagenes.Detectar("<html>no soy una imagen</html>"u8));
        Assert.Null(GestionarImagenes.Detectar("GIF89a"u8));   // GIF no se acepta
        Assert.Null(GestionarImagenes.Detectar(ReadOnlySpan<byte>.Empty));
    }
}
