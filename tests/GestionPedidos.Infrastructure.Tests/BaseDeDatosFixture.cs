using GestionPedidos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace GestionPedidos.Infrastructure.Tests;

/// <summary>Levanta UN SQL Server real en Docker por ejecución y aplica las migraciones reales (sin SQLite ni InMemory).</summary>
public sealed class BaseDeDatosFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _contenedor = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("Pruebas_Password_123!")
        .Build();

    /// <summary>Cadena de conexión de 'sa' al servidor del contenedor (base <c>master</c>).</summary>
    public string Servidor => _contenedor.GetConnectionString();

    /// <summary>Cadena de conexión a una base concreta del contenedor.</summary>
    public string Conexion(string baseDeDatos) => $"{Servidor};Database={baseDeDatos}";

    /// <summary>Crea un contexto sobre la base indicada.</summary>
    public GestionPedidosDbContext Contexto(string baseDeDatos = "GestionPedidosPruebas") =>
        new(new DbContextOptionsBuilder<GestionPedidosDbContext>().UseSqlServer(Conexion(baseDeDatos)).Options);

    public async Task InitializeAsync()
    {
        await _contenedor.StartAsync();
        await using var db = Contexto();
        await db.Database.MigrateAsync();   // las MISMAS migraciones que se usan en producción
    }

    public Task DisposeAsync() => _contenedor.DisposeAsync().AsTask();
}

[CollectionDefinition(Nombre)]
public sealed class BaseDeDatosCollection : ICollectionFixture<BaseDeDatosFixture>
{
    public const string Nombre = "base-de-datos";
}
