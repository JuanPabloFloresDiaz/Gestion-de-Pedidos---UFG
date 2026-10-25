using GestionPedidos.Domain;
using GestionPedidos.Domain.Clientes;
using GestionPedidos.Domain.Geografia;
using GestionPedidos.Domain.Historial;
using GestionPedidos.Domain.Pedidos;
using GestionPedidos.Domain.Productos;
using GestionPedidos.Domain.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace GestionPedidos.Infrastructure.Persistence;

/// <summary>Contexto de EF Core: unidad de trabajo y mapa de todas las tablas del sistema.</summary>
public class GestionPedidosDbContext : DbContext
{
    private readonly TimeProvider _reloj;

    /// <summary>Crea el contexto.</summary>
    /// <param name="options">Opciones (proveedor y cadena de conexión).</param>
    /// <param name="reloj">Reloj inyectable (UTC).</param>
    public GestionPedidosDbContext(DbContextOptions<GestionPedidosDbContext> options, TimeProvider? reloj = null)
        : base(options) => _reloj = reloj ?? TimeProvider.System;

    /// <summary>Roles.</summary>
    public DbSet<Rol> Roles => Set<Rol>();
    /// <summary>Permisos.</summary>
    public DbSet<Permiso> Permisos => Set<Permiso>();
    /// <summary>Relación rol–permiso.</summary>
    public DbSet<RolPermiso> RolesPermisos => Set<RolPermiso>();
    /// <summary>Usuarios.</summary>
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    /// <summary>Departamentos.</summary>
    public DbSet<Departamento> Departamentos => Set<Departamento>();
    /// <summary>Ciudades (distritos).</summary>
    public DbSet<Ciudad> Ciudades => Set<Ciudad>();
    /// <summary>Clientes.</summary>
    public DbSet<Cliente> Clientes => Set<Cliente>();
    /// <summary>Productos.</summary>
    public DbSet<Producto> Productos => Set<Producto>();
    /// <summary>Pedidos.</summary>
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    /// <summary>Líneas de pedido.</summary>
    public DbSet<DetallePedido> DetallesPedido => Set<DetallePedido>();
    /// <summary>Historial de actividades.</summary>
    public DbSet<Actividad> Actividades => Set<Actividad>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GestionPedidosDbContext).Assembly);

    /// <inheritdoc/>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Convenciones globales de [BaseDatos.md §3]: dinero con 2 decimales, fechas UTC sin fracciones.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<DateTimeOffset>().HavePrecision(0);
        configurationBuilder.Properties<string>().HaveMaxLength(250);   // nada de nvarchar(max) sin justificación
    }

    /// <inheritdoc/>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // CreadoEn / ActualizadoEn los mantiene el contexto, no el código de negocio.
        var ahora = _reloj.GetUtcNow();
        foreach (var e in ChangeTracker.Entries<IAuditable>())
        {
            if (e.State == EntityState.Added) { e.Entity.CreadoEn = ahora; e.Entity.ActualizadoEn = ahora; }
            else if (e.State == EntityState.Modified) e.Entity.ActualizadoEn = ahora;
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
