using GestionPedidos.Infrastructure;
using GestionPedidos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace GestionPedidos.Infrastructure.Tests;

/// <summary>Configuración de EF Core que debe mantenerse (no abre ninguna conexión).</summary>
public class ConfiguracionEfTests
{
    [Fact]
    public void ConsultasConVariasColecciones_SeDividen()
    {
        using var proveedor = new ServiceCollection().AddGestionPedidos("Server=ninguno;Database=ninguna;").BuildServiceProvider();
        using var alcance = proveedor.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<GestionPedidosDbContext>();

        var relacional = db.GetService<IDbContextOptions>().Extensions.OfType<RelationalOptionsExtension>().Single();

        Assert.Equal(QuerySplittingBehavior.SplitQuery, relacional.QuerySplittingBehavior);
    }
}
