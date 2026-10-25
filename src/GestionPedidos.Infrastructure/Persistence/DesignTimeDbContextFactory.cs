using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GestionPedidos.Infrastructure.Persistence;

/// <summary>Fábrica usada solo por las herramientas de EF Core (<c>dotnet ef</c>) al generar migraciones.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<GestionPedidosDbContext>
{
    /// <inheritdoc/>
    public GestionPedidosDbContext CreateDbContext(string[] args)
    {
        var conexion = Environment.GetEnvironmentVariable("ConnectionStrings__Migraciones")
            ?? "Server=localhost,4391;Database=GestionPedidos;User Id=sa;Password=Dev_Password_123!;TrustServerCertificate=True;Encrypt=True";
        var opciones = new DbContextOptionsBuilder<GestionPedidosDbContext>().UseSqlServer(conexion).Options;
        return new GestionPedidosDbContext(opciones);
    }
}
