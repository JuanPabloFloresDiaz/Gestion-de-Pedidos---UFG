namespace GestionPedidos.Application.Common;

/// <summary>Tipos de error de negocio; la API los traduce a códigos HTTP.</summary>
public enum TipoError
{
    /// <summary>Entrada inválida (400).</summary>
    Validacion,
    /// <summary>Sin credenciales válidas (401).</summary>
    NoAutenticado,
    /// <summary>Sin permiso (403).</summary>
    Prohibido,
    /// <summary>No existe (404).</summary>
    NoEncontrado,
    /// <summary>Conflicto de unicidad o concurrencia (409).</summary>
    Conflicto,
    /// <summary>Regla de negocio violada (422).</summary>
    Regla,
}

/// <summary>Error de un caso de uso.</summary>
/// <param name="Tipo">Clase de error.</param>
/// <param name="Codigo">Código estable (ver <c>CodigosError</c>).</param>
/// <param name="Mensaje">Mensaje en español apto para el usuario.</param>
public sealed record ErrorApp(TipoError Tipo, string Codigo, string Mensaje);

/// <summary>Resultado de un caso de uso sin valor de retorno.</summary>
public class Resultado
{
    /// <summary>Indica si salió bien.</summary>
    public bool Exito => Error is null;
    /// <summary>Error, si falló.</summary>
    public ErrorApp? Error { get; protected init; }

    /// <summary>Resultado correcto.</summary>
    public static Resultado Ok() => new();

    /// <summary>Resultado fallido.</summary>
    public static Resultado Falla(TipoError tipo, string codigo, string mensaje) =>
        new() { Error = new ErrorApp(tipo, codigo, mensaje) };
}

/// <summary>Resultado de un caso de uso con valor de retorno.</summary>
/// <typeparam name="T">Tipo del valor.</typeparam>
public sealed class Resultado<T> : Resultado
{
    /// <summary>Valor, si salió bien.</summary>
    public T? Valor { get; private init; }

    /// <summary>Resultado correcto con valor.</summary>
    public static Resultado<T> Ok(T valor) => new() { Valor = valor };

    /// <summary>Resultado fallido.</summary>
    public static new Resultado<T> Falla(TipoError tipo, string codigo, string mensaje) =>
        new() { Error = new ErrorApp(tipo, codigo, mensaje) };

    /// <summary>Resultado fallido a partir de un error existente.</summary>
    public static Resultado<T> Falla(ErrorApp error) => new() { Error = error };
}
