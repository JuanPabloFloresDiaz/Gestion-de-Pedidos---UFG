using GestionPedidos.Domain.Clientes;
using GestionPedidos.Domain.Historial;
using GestionPedidos.Domain.Pedidos;
using GestionPedidos.Domain.Productos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestionPedidos.Infrastructure.Persistence.Configurations;

/// <summary>Mapa de la tabla <c>Clientes</c>.</summary>
public sealed class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Cliente> b)
    {
        b.ToTable("Clientes");
        b.Property(x => x.Nombres).HasMaxLength(100).IsRequired();
        b.Property(x => x.Apellidos).HasMaxLength(100).IsRequired();
        b.Property(x => x.Telefono).HasMaxLength(20).IsRequired();
        b.Property(x => x.Direccion).HasMaxLength(250).IsRequired();
        b.Property(x => x.Correo).HasMaxLength(150);
        b.Property(x => x.Activo).HasDefaultValue(true);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.Apellidos, x.Nombres });
        b.HasIndex(x => x.CiudadId);
        b.HasIndex(x => x.Activo);
        // Únicos solo cuando hay valor: el correo es opcional y un cliente sin cuenta no tiene UsuarioId.
        b.HasIndex(x => x.Correo).IsUnique().HasFilter("[Correo] IS NOT NULL");
        b.HasIndex(x => x.UsuarioId).IsUnique().HasFilter("[UsuarioId] IS NOT NULL");
        b.HasOne(x => x.Ciudad).WithMany().HasForeignKey(x => x.CiudadId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Mapa de la tabla <c>Productos</c>.</summary>
public sealed class ProductoConfiguration : IEntityTypeConfiguration<Producto>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Producto> b)
    {
        b.ToTable("Productos", t =>
        {
            t.HasCheckConstraint("CK_Productos_CantidadStock", "[CantidadStock] >= 0");
            t.HasCheckConstraint("CK_Productos_PrecioVenta", "[PrecioVenta] >= 0");
        });
        b.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
        b.Property(x => x.Descripcion).HasMaxLength(500).IsRequired();
        b.Property(x => x.FechaIngreso).HasColumnType("date").HasDefaultValueSql("CONVERT(date, SYSUTCDATETIME())");
        b.Property(x => x.MostrarStock).HasDefaultValue(false);
        b.Property(x => x.Activo).HasDefaultValue(true);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.Nombre);
        b.HasIndex(x => x.PrecioVenta);
        b.HasIndex(x => x.Activo);
    }
}

/// <summary>Mapa de la tabla <c>Pedidos</c>.</summary>
public sealed class PedidoConfiguration : IEntityTypeConfiguration<Pedido>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Pedido> b)
    {
        b.ToTable("Pedidos", t =>
        {
            t.HasCheckConstraint("CK_Pedidos_Estado", "[Estado] IN (N'Pendiente', N'Entregado', N'Cancelado')");
            t.HasCheckConstraint("CK_Pedidos_Total", "[Total] >= 0");
        });
        b.Property(x => x.FechaEntrega).HasColumnType("date");
        b.Property(x => x.Comentarios).HasMaxLength(500);
        b.Property(x => x.Estado).HasMaxLength(20).HasDefaultValue(EstadoPedido.Pendiente).IsRequired();
        b.Property(x => x.Activo).HasDefaultValue(true);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.ClienteId);
        b.HasIndex(x => x.UsuarioId);
        b.HasIndex(x => new { x.Estado, x.FechaPedido });
        b.HasIndex(x => x.FechaEntrega);
        b.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Detalles).WithOne(d => d.Pedido).HasForeignKey(d => d.PedidoId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Mapa de la tabla <c>DetallesPedido</c>.</summary>
public sealed class DetallePedidoConfiguration : IEntityTypeConfiguration<DetallePedido>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DetallePedido> b)
    {
        b.ToTable("DetallesPedido", t =>
        {
            t.HasCheckConstraint("CK_DetallesPedido_Cantidad", "[Cantidad] > 0");
            t.HasCheckConstraint("CK_DetallesPedido_PrecioUnitario", "[PrecioUnitario] >= 0");
        });
        // Subtotal lo calcula y persiste la base de datos: Cantidad × PrecioUnitario.
        b.Property(x => x.Subtotal).HasComputedColumnSql("[Cantidad] * [PrecioUnitario]", stored: true);
        b.HasIndex(x => new { x.PedidoId, x.ProductoId }).IsUnique();   // un producto una sola vez por pedido
        b.HasIndex(x => x.ProductoId);
        b.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Mapa de la tabla <c>Actividades</c> (historial; la aplicación solo inserta y consulta).</summary>
public sealed class ActividadConfiguration : IEntityTypeConfiguration<Actividad>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Actividad> b)
    {
        b.ToTable("Actividades");
        b.Property(x => x.Accion).HasMaxLength(40).IsRequired();
        b.Property(x => x.Entidad).HasMaxLength(40).IsRequired();
        b.Property(x => x.Detalle).HasMaxLength(500);
        b.HasIndex(x => x.Fecha);
        b.HasIndex(x => x.UsuarioId);
        b.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
    }
}
