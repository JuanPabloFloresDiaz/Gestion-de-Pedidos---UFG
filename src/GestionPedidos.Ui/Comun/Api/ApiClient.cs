using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GestionPedidos.Ui.Comun.Sesion;

namespace GestionPedidos.Ui.Comun.Api;

/// <summary>
/// Único punto de salida hacia la API: añade el JWT, traduce <i>Problem Details</i> a <see cref="ApiException"/>
/// con mensajes en español y avisa a la sesión cuando la API responde 401. Ninguna página crea su propio HttpClient.
/// </summary>
public sealed class ApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly SesionService _sesion;

    /// <summary>Crea el cliente.</summary>
    /// <param name="http">Cliente HTTP con la dirección base de la API.</param>
    /// <param name="sesion">Sesión de la que sale el token.</param>
    public ApiClient(HttpClient http, SesionService sesion)
    {
        _http = http;
        _sesion = sesion;
    }

    /// <summary>GET que devuelve un objeto JSON.</summary>
    public Task<T> GetAsync<T>(string ruta, CancellationToken ct = default) => EnviarAsync<T>(HttpMethod.Get, ruta, null, ct);

    /// <summary>POST con cuerpo JSON que devuelve un objeto JSON.</summary>
    public Task<T> PostAsync<T>(string ruta, object? cuerpo, CancellationToken ct = default) => EnviarAsync<T>(HttpMethod.Post, ruta, cuerpo, ct);

    /// <summary>POST con cuerpo JSON sin valor de retorno.</summary>
    public Task PostAsync(string ruta, object? cuerpo, CancellationToken ct = default) => EnviarAsync<object>(HttpMethod.Post, ruta, cuerpo, ct, sinRespuesta: true);

    /// <summary>PUT con cuerpo JSON que devuelve un objeto JSON.</summary>
    public Task<T> PutAsync<T>(string ruta, object? cuerpo, CancellationToken ct = default) => EnviarAsync<T>(HttpMethod.Put, ruta, cuerpo, ct);

    /// <summary>DELETE sin valor de retorno.</summary>
    public Task DeleteAsync(string ruta, CancellationToken ct = default) => EnviarAsync<object>(HttpMethod.Delete, ruta, null, ct, sinRespuesta: true);

    /// <summary>PATCH con cuerpo JSON que devuelve un objeto JSON.</summary>
    public Task<T> PatchAsync<T>(string ruta, object? cuerpo, CancellationToken ct = default) => EnviarAsync<T>(HttpMethod.Patch, ruta, cuerpo, ct);

    private async Task<T> EnviarAsync<T>(HttpMethod metodo, string ruta, object? cuerpo, CancellationToken ct, bool sinRespuesta = false)
    {
        using var peticion = new HttpRequestMessage(metodo, ruta);
        if (_sesion.Token is { } token) peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (cuerpo is not null) peticion.Content = JsonContent.Create(cuerpo, options: Json);

        HttpResponseMessage respuesta;
        try { respuesta = await _http.SendAsync(peticion, ct); }
        catch (HttpRequestException) { throw new ApiException("No se pudo conectar con el servidor. Verifique que la API esté en ejecución."); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new ApiException("El servidor tardó demasiado en responder."); }

        using (respuesta)
        {
            if (respuesta.IsSuccessStatusCode)
                return sinRespuesta || respuesta.StatusCode == HttpStatusCode.NoContent
                    ? default!
                    : (await respuesta.Content.ReadFromJsonAsync<T>(Json, ct))!;
            throw await CrearErrorAsync(respuesta, ct);
        }
    }

    // Traduce el cuerpo Problem Details (title + codigo) a un mensaje para la persona.
    private async Task<ApiException> CrearErrorAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        string? titulo = null, codigo = null;
        try
        {
            using var doc = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.TryGetProperty("title", out var t)) titulo = t.GetString();
            if (doc.RootElement.TryGetProperty("codigo", out var c)) codigo = c.GetString();
        }
        catch (JsonException) { /* cuerpo sin JSON: se usa el mensaje por estado */ }

        var estado = (int)respuesta.StatusCode;
        if (estado == 401 && _sesion.Autenticada) await _sesion.ExpirarAsync();   // el token venció o se revocó
        var mensaje = estado switch
        {
            401 => titulo ?? "Su sesión no es válida.",
            403 => titulo ?? "No tiene permiso para esta acción.",
            404 => titulo ?? "No se encontró el registro.",
            409 => titulo ?? "El registro cambió; recargue e intente de nuevo.",
            429 => "Demasiados intentos. Espere un momento e intente de nuevo.",
            >= 500 => "Ocurrió un error en el servidor. Intente más tarde.",
            _ => titulo ?? "No se pudo completar la operación.",
        };
        return new ApiException(mensaje, estado, codigo);
    }
}
