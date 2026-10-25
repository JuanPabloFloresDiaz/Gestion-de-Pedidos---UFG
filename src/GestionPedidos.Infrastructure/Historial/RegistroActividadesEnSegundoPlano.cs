using System.Threading.Channels;
using GestionPedidos.Application.Historial;
using GestionPedidos.Domain.Historial;
using GestionPedidos.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GestionPedidos.Infrastructure.Historial;

/// <summary>
/// Guarda las actividades en segundo plano: los eventos solo las encolan (no bloquean al usuario) y un
/// servicio las inserta con su propio contexto. Si falla un registro se loguea, nunca revierte la operación.
/// </summary>
public sealed class RegistroActividadesEnSegundoPlano : BackgroundService, IRegistroActividades
{
    private readonly Channel<Actividad> _cola = Channel.CreateUnbounded<Actividad>();
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _reloj;
    private readonly ILogger<RegistroActividadesEnSegundoPlano> _log;

    /// <summary>Crea el servicio.</summary>
    public RegistroActividadesEnSegundoPlano(IServiceScopeFactory scopes, TimeProvider reloj,
        ILogger<RegistroActividadesEnSegundoPlano> log)
    {
        _scopes = scopes; _reloj = reloj; _log = log;
    }

    /// <inheritdoc/>
    public void Registrar(Actividad actividad)
    {
        actividad.Fecha = _reloj.GetUtcNow();
        _cola.Writer.TryWrite(actividad);
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var actividad in _cola.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<GestionPedidosDbContext>();
                    db.Actividades.Add(actividad);
                    await db.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log.LogError(ex, "No se pudo guardar una actividad del historial.");
                }
            }
        }
        catch (OperationCanceledException) { /* apagado normal */ }
    }
}
