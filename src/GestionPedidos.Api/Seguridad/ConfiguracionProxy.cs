using Microsoft.AspNetCore.HttpOverrides;

namespace GestionPedidos.Api.Seguridad;

/// <summary>
/// Confianza en el proxy inverso (Traefik/Dokploy/Nginx). Detrás de un proxy, <c>RemoteIpAddress</c> es la IP del proxy y no la de la
/// persona: sin esto, el límite de intentos de login por IP se compartiría entre TODOS los usuarios. Con el proxy bien configurado, la IP
/// real sale de <c>X-Forwarded-For</c>. Solo se aceptan esas cabeceras si la petición llega desde una red confiable; si no, cualquiera
/// podría falsear su IP y saltarse el límite.
/// </summary>
public static class ConfiguracionProxy
{
    /// <summary>Sección de configuración: <c>ForwardedHeaders:Habilitado</c> y <c>ForwardedHeaders:RedesConfiables</c> (lista de CIDR).</summary>
    public const string Seccion = "ForwardedHeaders";

    /// <summary>Redes que se confían si no se indican otras: la propia máquina y las redes privadas (donde viven los contenedores).</summary>
    public static readonly string[] RedesPorDefecto = ["127.0.0.0/8", "::1/128", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "fc00::/7"];

    /// <summary>Indica si la API está detrás de un proxy y debe leer las cabeceras <c>X-Forwarded-*</c> (apagado por defecto).</summary>
    public static bool Habilitado(IConfiguration config) => config.GetValue($"{Seccion}:Habilitado", false);

    /// <summary>Registra las opciones de cabeceras reenviadas según la configuración.</summary>
    public static IServiceCollection AddProxyConfiable(this IServiceCollection servicios, IConfiguration config) =>
        servicios.Configure<ForwardedHeadersOptions>(o => Aplicar(o, config));

    /// <summary>Rellena las opciones: cabeceras que se leen y redes desde las que se aceptan.</summary>
    public static void Aplicar(ForwardedHeadersOptions o, IConfiguration config)
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.ForwardLimit = 1;   // un solo salto de proxy
        o.KnownProxies.Clear();
        o.KnownIPNetworks.Clear();   // por defecto solo confía en loopback: se sustituye por la lista explícita

        var redes = config.GetSection($"{Seccion}:RedesConfiables").Get<string[]>() is { Length: > 0 } propias ? propias : RedesPorDefecto;
        foreach (var red in redes)
        {
            if (!System.Net.IPNetwork.TryParse(red.Trim(), out var ipRed))
                throw new InvalidOperationException($"{Seccion}:RedesConfiables contiene un CIDR no válido: '{red}'.");
            o.KnownIPNetworks.Add(ipRed);
        }
    }
}
