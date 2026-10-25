using GestionPedidos.Application.Common;

namespace GestionPedidos.Api.Errores;

/// <summary>Traduce resultados de casos de uso a respuestas HTTP con Problem Details (RFC 9457).</summary>
public static class ResultadoHttp
{
    /// <summary>Respuesta de error con el estado que corresponde al tipo de error.</summary>
    public static IResult AProblema(this ErrorApp error) => Results.Problem(
        statusCode: error.Tipo switch
        {
            TipoError.Validacion => StatusCodes.Status400BadRequest,
            TipoError.NoAutenticado => StatusCodes.Status401Unauthorized,
            TipoError.Prohibido => StatusCodes.Status403Forbidden,
            TipoError.NoEncontrado => StatusCodes.Status404NotFound,
            TipoError.Conflicto => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity,
        },
        title: error.Mensaje,
        extensions: new Dictionary<string, object?> { ["codigo"] = error.Codigo });

    /// <summary>Convierte un resultado: <paramref name="ok"/> si salió bien, Problem Details si falló.</summary>
    public static IResult AHttp<T>(this Resultado<T> resultado, Func<T, IResult> ok) =>
        resultado.Exito ? ok(resultado.Valor!) : resultado.Error!.AProblema();

    /// <summary>Convierte un resultado sin valor: 204 si salió bien.</summary>
    public static IResult AHttp(this Resultado resultado) =>
        resultado.Exito ? Results.NoContent() : resultado.Error!.AProblema();
}
