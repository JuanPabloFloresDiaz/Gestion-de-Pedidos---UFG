using GestionPedidos.Domain.Seguridad;
using GestionPedidos.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestionPedidos.Infrastructure.Persistence.Configurations;

/// <summary>Mapa de la tabla <c>Roles</c> y su semilla (Administrador, Operador, Cliente).</summary>
public sealed class RolConfiguration : IEntityTypeConfiguration<Rol>
{
    /// <summary>Identificadores fijos de los roles del sistema.</summary>
    public static readonly IReadOnlyDictionary<string, int> Ids = new Dictionary<string, int>
    {
        [NombresRol.Administrador] = 1, [NombresRol.Operador] = 2, [NombresRol.Cliente] = 3,
    };

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Rol> b)
    {
        b.ToTable("Roles");
        b.Property(x => x.Nombre).HasMaxLength(50).IsRequired();
        b.Property(x => x.Descripcion).HasMaxLength(200);
        b.HasIndex(x => x.Nombre).IsUnique();
        b.HasData(
            new Rol { Id = 1, Nombre = NombresRol.Administrador, Descripcion = "Gestión completa, usuarios e historial" },
            new Rol { Id = 2, Nombre = NombresRol.Operador, Descripcion = "Trabajo diario con clientes, productos (consulta) y pedidos" },
            new Rol { Id = 3, Nombre = NombresRol.Cliente, Descripcion = "Cliente de la tienda web" });
    }
}

/// <summary>Mapa de la tabla <c>Permisos</c> y su semilla (catálogo de permisos).</summary>
public sealed class PermisoConfiguration : IEntityTypeConfiguration<Permiso>
{
    /// <summary>Identificador estable de un permiso según su posición en el catálogo.</summary>
    public static int IdDe(string codigo) =>
        CatalogoPermisos.Todos.ToList().FindIndex(p => p.Codigo == codigo) + 1;

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Permiso> b)
    {
        b.ToTable("Permisos");
        b.Property(x => x.Codigo).HasMaxLength(60).IsRequired();
        b.Property(x => x.Descripcion).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.Codigo).IsUnique();
        b.HasData(CatalogoPermisos.Todos.Select((p, i) => new Permiso { Id = i + 1, Codigo = p.Codigo, Descripcion = p.Descripcion }));
    }
}

/// <summary>Mapa de la tabla <c>RolesPermisos</c> y su semilla (matriz de permisos).</summary>
public sealed class RolPermisoConfiguration : IEntityTypeConfiguration<RolPermiso>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<RolPermiso> b)
    {
        b.ToTable("RolesPermisos");
        b.HasKey(x => new { x.RolId, x.PermisoId });
        b.HasOne(x => x.Rol).WithMany(r => r.Permisos).HasForeignKey(x => x.RolId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Permiso).WithMany().HasForeignKey(x => x.PermisoId).OnDelete(DeleteBehavior.Cascade);
        b.HasData(RolConfiguration.Ids.SelectMany(r =>
            CatalogoPermisos.DeRol(r.Key).Select(c => new RolPermiso { RolId = r.Value, PermisoId = PermisoConfiguration.IdDe(c) })));
    }
}

/// <summary>Mapa de la tabla <c>Usuarios</c>.</summary>
public sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("Usuarios");
        b.Property(x => x.NombreUsuario).HasMaxLength(150).IsRequired();
        b.Property(x => x.NombreCompleto).HasMaxLength(150).IsRequired();
        b.Property(x => x.Correo).HasMaxLength(150).IsRequired();
        b.Property(x => x.ClaveHash).HasMaxLength(255).IsRequired();
        b.Property(x => x.Activo).HasDefaultValue(true);
        b.Property(x => x.DebeCambiarClave).HasDefaultValue(false);
        b.Property(x => x.IntentosFallidos).HasDefaultValue(0);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.NombreUsuario).IsUnique();
        b.HasIndex(x => x.Correo).IsUnique();
        b.HasOne(x => x.Rol).WithMany().HasForeignKey(x => x.RolId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Mapa de las tablas geográficas y su semilla de El Salvador.</summary>
public sealed class DepartamentoConfiguration : IEntityTypeConfiguration<GestionPedidos.Domain.Geografia.Departamento>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<GestionPedidos.Domain.Geografia.Departamento> b)
    {
        b.ToTable("Departamentos");
        b.Property(x => x.Codigo).HasMaxLength(3).IsRequired();
        b.Property(x => x.Nombre).HasMaxLength(60).IsRequired();
        b.HasIndex(x => x.Codigo).IsUnique();
        b.HasIndex(x => x.Nombre).IsUnique();
        b.Ignore(x => x.Ciudades);
        b.HasData(GeoSeed.Departamentos.Select(d => new GestionPedidos.Domain.Geografia.Departamento
            { Id = d.Id, Codigo = d.Codigo, Nombre = d.Nombre }));
    }
}

/// <summary>Mapa de la tabla <c>Ciudades</c> (distritos) y su semilla.</summary>
public sealed class CiudadConfiguration : IEntityTypeConfiguration<GestionPedidos.Domain.Geografia.Ciudad>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<GestionPedidos.Domain.Geografia.Ciudad> b)
    {
        b.ToTable("Ciudades");
        b.Property(x => x.Municipio).HasMaxLength(80).IsRequired();
        b.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
        b.HasIndex(x => new { x.DepartamentoId, x.Nombre }).IsUnique();   // sirve a "ciudades de un departamento"
        b.HasOne(x => x.Departamento).WithMany().HasForeignKey(x => x.DepartamentoId).OnDelete(DeleteBehavior.Restrict);
        b.HasData(GeoSeed.Ciudades.Select(c => new GestionPedidos.Domain.Geografia.Ciudad
            { Id = c.Id, DepartamentoId = c.DepartamentoId, Municipio = c.Municipio, Nombre = c.Nombre }));
    }
}
