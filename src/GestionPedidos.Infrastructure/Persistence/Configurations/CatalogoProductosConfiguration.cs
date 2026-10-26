using GestionPedidos.Domain.Catalogo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestionPedidos.Infrastructure.Persistence.Configurations;

/// <summary>Mapa de la tabla <c>Categorias</c>.</summary>
public sealed class CategoriaConfiguration : IEntityTypeConfiguration<Categoria>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Categoria> b)
    {
        b.ToTable("Categorias");
        b.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
        b.Property(x => x.Descripcion).HasMaxLength(300);
        b.Property(x => x.Activo).HasDefaultValue(true);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.Nombre).IsUnique();   // la intercalación de SQL Server no distingue mayúsculas
    }
}

/// <summary>Mapa de la tabla <c>Subcategorias</c>.</summary>
public sealed class SubcategoriaConfiguration : IEntityTypeConfiguration<Subcategoria>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Subcategoria> b)
    {
        b.ToTable("Subcategorias");
        b.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
        b.Property(x => x.Descripcion).HasMaxLength(300);
        b.Property(x => x.Activo).HasDefaultValue(true);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.CategoriaId, x.Nombre }).IsUnique();
        b.HasOne(x => x.Categoria).WithMany(c => c.Subcategorias).HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Mapa de la tabla <c>Filtros</c>.</summary>
public sealed class FiltroConfiguration : IEntityTypeConfiguration<Filtro>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Filtro> b)
    {
        b.ToTable("Filtros");
        b.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
        b.Property(x => x.Activo).HasDefaultValue(true);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.SubcategoriaId, x.Nombre }).IsUnique();
        b.HasOne(x => x.Subcategoria).WithMany(s => s.Filtros).HasForeignKey(x => x.SubcategoriaId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Mapa de la tabla <c>OpcionesFiltro</c>.</summary>
public sealed class OpcionFiltroConfiguration : IEntityTypeConfiguration<OpcionFiltro>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<OpcionFiltro> b)
    {
        b.ToTable("OpcionesFiltro");
        b.Property(x => x.Valor).HasMaxLength(100).IsRequired();
        b.Property(x => x.Activo).HasDefaultValue(true);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.FiltroId, x.Valor }).IsUnique();
        // (Id, FiltroId) único: es el destino de la clave foránea compuesta de ProductosOpciones, que así
        // garantiza que la opción elegida sea realmente del filtro indicado.
        b.HasAlternateKey(x => new { x.Id, x.FiltroId });
        b.HasOne(x => x.Filtro).WithMany(f => f.Opciones).HasForeignKey(x => x.FiltroId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Mapa de la tabla <c>ProductosOpciones</c> (el detalle del producto: una opción por filtro).</summary>
public sealed class ProductoOpcionConfiguration : IEntityTypeConfiguration<ProductoOpcion>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ProductoOpcion> b)
    {
        b.ToTable("ProductosOpciones");
        b.HasKey(x => new { x.ProductoId, x.FiltroId });   // una sola opción por filtro y producto
        b.HasIndex(x => x.OpcionFiltroId);                 // para filtrar la tienda por opción
        b.HasOne(x => x.Producto).WithMany(p => p.Opciones).HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Filtro).WithMany().HasForeignKey(x => x.FiltroId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Opcion).WithMany().HasForeignKey(x => new { x.OpcionFiltroId, x.FiltroId })
            .HasPrincipalKey(o => new { o.Id, o.FiltroId }).OnDelete(DeleteBehavior.Restrict);
    }
}
