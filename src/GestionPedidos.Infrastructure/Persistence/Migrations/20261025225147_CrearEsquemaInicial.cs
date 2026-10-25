using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GestionPedidos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CrearEsquemaInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Departamentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departamentos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permisos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permisos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Productos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CantidadStock = table.Column<int>(type: "int", nullable: false),
                    PrecioVenta = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FechaIngreso = table.Column<DateOnly>(type: "date", nullable: false, defaultValueSql: "CONVERT(date, SYSUTCDATETIME())"),
                    MostrarStock = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Productos", x => x.Id);
                    table.CheckConstraint("CK_Productos_CantidadStock", "[CantidadStock] >= 0");
                    table.CheckConstraint("CK_Productos_PrecioVenta", "[PrecioVenta] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ciudades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartamentoId = table.Column<int>(type: "int", nullable: false),
                    Municipio = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ciudades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ciudades_Departamentos_DepartamentoId",
                        column: x => x.DepartamentoId,
                        principalTable: "Departamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolesPermisos",
                columns: table => new
                {
                    RolId = table.Column<int>(type: "int", nullable: false),
                    PermisoId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolesPermisos", x => new { x.RolId, x.PermisoId });
                    table.ForeignKey(
                        name: "FK_RolesPermisos_Permisos_PermisoId",
                        column: x => x.PermisoId,
                        principalTable: "Permisos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolesPermisos_Roles_RolId",
                        column: x => x.RolId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NombreUsuario = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NombreCompleto = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Correo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ClaveHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    RolId = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DebeCambiarClave = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IntentosFallidos = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    BloqueadoHasta = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    UltimoAcceso = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Usuarios_Roles_RolId",
                        column: x => x.RolId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Actividades",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<int>(type: "int", nullable: true),
                    Fecha = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    Accion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Entidad = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EntidadId = table.Column<int>(type: "int", nullable: true),
                    Detalle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Actividades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Actividades_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombres = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Apellidos = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Telefono = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Direccion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Correo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CiudadId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clientes_Ciudades_CiudadId",
                        column: x => x.CiudadId,
                        principalTable: "Ciudades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Clientes_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pedidos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    FechaPedido = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    FechaEntrega = table.Column<DateOnly>(type: "date", nullable: false),
                    Comentarios = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pendiente"),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pedidos", x => x.Id);
                    table.CheckConstraint("CK_Pedidos_Estado", "[Estado] IN (N'Pendiente', N'Entregado', N'Cancelado')");
                    table.CheckConstraint("CK_Pedidos_Total", "[Total] >= 0");
                    table.ForeignKey(
                        name: "FK_Pedidos_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pedidos_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DetallesPedido",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PedidoId = table.Column<int>(type: "int", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, computedColumnSql: "[Cantidad] * [PrecioUnitario]", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallesPedido", x => x.Id);
                    table.CheckConstraint("CK_DetallesPedido_Cantidad", "[Cantidad] > 0");
                    table.CheckConstraint("CK_DetallesPedido_PrecioUnitario", "[PrecioUnitario] >= 0");
                    table.ForeignKey(
                        name: "FK_DetallesPedido_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DetallesPedido_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Departamentos",
                columns: new[] { "Id", "Codigo", "Nombre" },
                values: new object[,]
                {
                    { 1, "AH", "Ahuachapán" },
                    { 2, "SS", "San Salvador" },
                    { 3, "LI", "La Libertad" },
                    { 4, "CH", "Chalatenango" },
                    { 5, "CU", "Cuscatlán" },
                    { 6, "CA", "Cabañas" },
                    { 7, "PA", "La Paz" },
                    { 8, "UN", "La Unión" },
                    { 9, "US", "Usulután" },
                    { 10, "SO", "Sonsonate" },
                    { 11, "SA", "Santa Ana" },
                    { 12, "SV", "San Vicente" },
                    { 13, "SM", "San Miguel" },
                    { 14, "MO", "Morazán" }
                });

            migrationBuilder.InsertData(
                table: "Permisos",
                columns: new[] { "Id", "Codigo", "Descripcion" },
                values: new object[,]
                {
                    { 1, "admin.acceso", "Entrar al área de Administración" },
                    { 2, "clientes.ver", "Ver clientes" },
                    { 3, "clientes.crear", "Crear clientes" },
                    { 4, "clientes.editar", "Editar clientes" },
                    { 5, "clientes.desactivar", "Dar de baja o reactivar clientes" },
                    { 6, "productos.ver", "Ver productos" },
                    { 7, "productos.crear", "Crear productos" },
                    { 8, "productos.editar", "Editar productos" },
                    { 9, "productos.desactivar", "Dar de baja o reactivar productos" },
                    { 10, "pedidos.ver", "Ver pedidos" },
                    { 11, "pedidos.crear", "Crear pedidos" },
                    { 12, "pedidos.editar", "Editar pedidos" },
                    { 13, "pedidos.cambiar_estado", "Cambiar el estado de un pedido" },
                    { 14, "pedidos.desactivar", "Dar de baja pedidos" },
                    { 15, "usuarios.ver", "Ver usuarios" },
                    { 16, "usuarios.crear", "Crear usuarios" },
                    { 17, "usuarios.editar", "Editar usuarios" },
                    { 18, "usuarios.desactivar", "Dar de baja o reactivar usuarios" },
                    { 19, "roles.ver", "Ver roles y permisos" },
                    { 20, "historial.ver", "Ver el historial de actividades" },
                    { 21, "catalogo.ver", "Ver el catálogo de la tienda" },
                    { 22, "mispedidos.ver", "Ver los pedidos propios" },
                    { 23, "mispedidos.crear", "Crear pedidos propios" },
                    { 24, "mispedidos.cancelar", "Cancelar pedidos propios" },
                    { 25, "perfil.editar", "Editar el perfil propio" }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Descripcion", "Nombre" },
                values: new object[,]
                {
                    { 1, "Gestión completa, usuarios e historial", "Administrador" },
                    { 2, "Trabajo diario con clientes, productos (consulta) y pedidos", "Operador" },
                    { 3, "Cliente de la tienda web", "Cliente" }
                });

            migrationBuilder.InsertData(
                table: "Ciudades",
                columns: new[] { "Id", "DepartamentoId", "Municipio", "Nombre" },
                values: new object[,]
                {
                    { 1, 1, "Ahuachapán Norte", "Atiquizaya" },
                    { 2, 1, "Ahuachapán Norte", "El Refugio" },
                    { 3, 1, "Ahuachapán Norte", "San Lorenzo" },
                    { 4, 1, "Ahuachapán Norte", "Turín" },
                    { 5, 1, "Ahuachapán Centro", "Ahuachapán" },
                    { 6, 1, "Ahuachapán Centro", "Apaneca" },
                    { 7, 1, "Ahuachapán Centro", "Concepción de Ataco" },
                    { 8, 1, "Ahuachapán Centro", "Tacuba" },
                    { 9, 1, "Ahuachapán Sur", "Guaymango" },
                    { 10, 1, "Ahuachapán Sur", "Jujutla" },
                    { 11, 1, "Ahuachapán Sur", "San Francisco Menéndez" },
                    { 12, 1, "Ahuachapán Sur", "San Pedro Puxtla" },
                    { 13, 2, "San Salvador Norte", "Aguilares" },
                    { 14, 2, "San Salvador Norte", "El Paisnal" },
                    { 15, 2, "San Salvador Norte", "Guazapa" },
                    { 16, 2, "San Salvador Oeste", "Apopa" },
                    { 17, 2, "San Salvador Oeste", "Nejapa" },
                    { 18, 2, "San Salvador Este", "Ilopango" },
                    { 19, 2, "San Salvador Este", "San Martín" },
                    { 20, 2, "San Salvador Este", "Soyapango" },
                    { 21, 2, "San Salvador Este", "Tonacatepeque" },
                    { 22, 2, "San Salvador Centro", "Ayutuxtepeque" },
                    { 23, 2, "San Salvador Centro", "Mejicanos" },
                    { 24, 2, "San Salvador Centro", "San Salvador" },
                    { 25, 2, "San Salvador Centro", "Cuscatancingo" },
                    { 26, 2, "San Salvador Centro", "Ciudad Delgado" },
                    { 27, 2, "San Salvador Sur", "Panchimalco" },
                    { 28, 2, "San Salvador Sur", "Rosario de Mora" },
                    { 29, 2, "San Salvador Sur", "San Marcos" },
                    { 30, 2, "San Salvador Sur", "Santo Tomás" },
                    { 31, 2, "San Salvador Sur", "Santiago Texacuangos" },
                    { 32, 3, "La Libertad Norte", "Quezaltepeque" },
                    { 33, 3, "La Libertad Norte", "San Matías" },
                    { 34, 3, "La Libertad Norte", "San Pablo Tacachico" },
                    { 35, 3, "La Libertad Centro", "San Juan Opico" },
                    { 36, 3, "La Libertad Centro", "Ciudad Arce" },
                    { 37, 3, "La Libertad Oeste", "Colón" },
                    { 38, 3, "La Libertad Oeste", "Jayaque" },
                    { 39, 3, "La Libertad Oeste", "Sacacoyo" },
                    { 40, 3, "La Libertad Oeste", "Tepecoyo" },
                    { 41, 3, "La Libertad Oeste", "Talnique" },
                    { 42, 3, "La Libertad Este", "Antiguo Cuscatlán" },
                    { 43, 3, "La Libertad Este", "Huizúcar" },
                    { 44, 3, "La Libertad Este", "Nuevo Cuscatlán" },
                    { 45, 3, "La Libertad Este", "San José Villanueva" },
                    { 46, 3, "La Libertad Este", "Zaragoza" },
                    { 47, 3, "La Libertad Costa", "Chiltiupán" },
                    { 48, 3, "La Libertad Costa", "Jicalapa" },
                    { 49, 3, "La Libertad Costa", "La Libertad" },
                    { 50, 3, "La Libertad Costa", "Tamanique" },
                    { 51, 3, "La Libertad Costa", "Teotepeque" },
                    { 52, 3, "La Libertad Sur", "Comasagua" },
                    { 53, 3, "La Libertad Sur", "Santa Tecla" },
                    { 54, 4, "Chalatenango Norte", "La Palma" },
                    { 55, 4, "Chalatenango Norte", "Citalá" },
                    { 56, 4, "Chalatenango Norte", "San Ignacio" },
                    { 57, 4, "Chalatenango Centro", "Nueva Concepción" },
                    { 58, 4, "Chalatenango Centro", "Tejutla" },
                    { 59, 4, "Chalatenango Centro", "La Reina" },
                    { 60, 4, "Chalatenango Centro", "Agua Caliente" },
                    { 61, 4, "Chalatenango Centro", "Dulce Nombre de María" },
                    { 62, 4, "Chalatenango Centro", "El Paraíso" },
                    { 63, 4, "Chalatenango Centro", "San Fernando" },
                    { 64, 4, "Chalatenango Centro", "San Francisco Morazán" },
                    { 65, 4, "Chalatenango Centro", "San Rafael" },
                    { 66, 4, "Chalatenango Centro", "Santa Rita" },
                    { 67, 4, "Chalatenango Sur", "Chalatenango" },
                    { 68, 4, "Chalatenango Sur", "Arcatao" },
                    { 69, 4, "Chalatenango Sur", "Azacualpa" },
                    { 70, 4, "Chalatenango Sur", "Comalapa" },
                    { 71, 4, "Chalatenango Sur", "Concepción Quezaltepeque" },
                    { 72, 4, "Chalatenango Sur", "El Carrizal" },
                    { 73, 4, "Chalatenango Sur", "La Laguna" },
                    { 74, 4, "Chalatenango Sur", "Las Vueltas" },
                    { 75, 4, "Chalatenango Sur", "Nombre de Jesús" },
                    { 76, 4, "Chalatenango Sur", "Nueva Trinidad" },
                    { 77, 4, "Chalatenango Sur", "Ojos de Agua" },
                    { 78, 4, "Chalatenango Sur", "Potónico" },
                    { 79, 4, "Chalatenango Sur", "San Antonio de La Cruz" },
                    { 80, 4, "Chalatenango Sur", "San Antonio Los Ranchos" },
                    { 81, 4, "Chalatenango Sur", "San Francisco Lempa" },
                    { 82, 4, "Chalatenango Sur", "San Isidro Labrador" },
                    { 83, 4, "Chalatenango Sur", "San José Cancasque" },
                    { 84, 4, "Chalatenango Sur", "San Miguel de Mercedes" },
                    { 85, 4, "Chalatenango Sur", "San José Las Flores" },
                    { 86, 4, "Chalatenango Sur", "San Luis del Carmen" },
                    { 87, 5, "Cuscatlán Norte", "Suchitoto" },
                    { 88, 5, "Cuscatlán Norte", "San José Guayabal" },
                    { 89, 5, "Cuscatlán Norte", "Oratorio de Concepción" },
                    { 90, 5, "Cuscatlán Norte", "San Bartolomé Perulapía" },
                    { 91, 5, "Cuscatlán Norte", "San Pedro Perulapán" },
                    { 92, 5, "Cuscatlán Sur", "Cojutepeque" },
                    { 93, 5, "Cuscatlán Sur", "San Rafael Cedros" },
                    { 94, 5, "Cuscatlán Sur", "Candelaria" },
                    { 95, 5, "Cuscatlán Sur", "Monte San Juan" },
                    { 96, 5, "Cuscatlán Sur", "El Carmen" },
                    { 97, 5, "Cuscatlán Sur", "San Cristóbal" },
                    { 98, 5, "Cuscatlán Sur", "Santa Cruz Michapa" },
                    { 99, 5, "Cuscatlán Sur", "San Ramón" },
                    { 100, 5, "Cuscatlán Sur", "El Rosario" },
                    { 101, 5, "Cuscatlán Sur", "Santa Cruz Analquito" },
                    { 102, 5, "Cuscatlán Sur", "Tenancingo" },
                    { 103, 6, "Cabañas Este", "Sensuntepeque" },
                    { 104, 6, "Cabañas Este", "Victoria" },
                    { 105, 6, "Cabañas Este", "Dolores" },
                    { 106, 6, "Cabañas Este", "Guacotecti" },
                    { 107, 6, "Cabañas Este", "San Isidro" },
                    { 108, 6, "Cabañas Oeste", "Ilobasco" },
                    { 109, 6, "Cabañas Oeste", "Tejutepeque" },
                    { 110, 6, "Cabañas Oeste", "Jutiapa" },
                    { 111, 6, "Cabañas Oeste", "Cinquera" },
                    { 112, 7, "La Paz Oeste", "Cuyultitán" },
                    { 113, 7, "La Paz Oeste", "Olocuilta" },
                    { 114, 7, "La Paz Oeste", "San Juan Talpa" },
                    { 115, 7, "La Paz Oeste", "San Luis Talpa" },
                    { 116, 7, "La Paz Oeste", "San Pedro Masahuat" },
                    { 117, 7, "La Paz Oeste", "Tapalhuaca" },
                    { 118, 7, "La Paz Oeste", "San Francisco Chinameca" },
                    { 119, 7, "La Paz Centro", "El Rosario" },
                    { 120, 7, "La Paz Centro", "Jerusalén" },
                    { 121, 7, "La Paz Centro", "Mercedes La Ceiba" },
                    { 122, 7, "La Paz Centro", "Paraíso de Osorio" },
                    { 123, 7, "La Paz Centro", "San Antonio Masahuat" },
                    { 124, 7, "La Paz Centro", "San Emigdio" },
                    { 125, 7, "La Paz Centro", "San Juan Tepezontes" },
                    { 126, 7, "La Paz Centro", "San Luis La Herradura" },
                    { 127, 7, "La Paz Centro", "San Miguel Tepezontes" },
                    { 128, 7, "La Paz Centro", "San Pedro Nonualco" },
                    { 129, 7, "La Paz Centro", "Santa María Ostuma" },
                    { 130, 7, "La Paz Centro", "Santiago Nonualco" },
                    { 131, 7, "La Paz Este", "San Juan Nonualco" },
                    { 132, 7, "La Paz Este", "San Rafael Obrajuelo" },
                    { 133, 7, "La Paz Este", "Zacatecoluca" },
                    { 134, 8, "La Unión Norte", "Anamorós" },
                    { 135, 8, "La Unión Norte", "Bolívar" },
                    { 136, 8, "La Unión Norte", "Concepción de Oriente" },
                    { 137, 8, "La Unión Norte", "El Sauce" },
                    { 138, 8, "La Unión Norte", "Lislique" },
                    { 139, 8, "La Unión Norte", "Nueva Esparta" },
                    { 140, 8, "La Unión Norte", "Pasaquina" },
                    { 141, 8, "La Unión Norte", "Polorós" },
                    { 142, 8, "La Unión Norte", "San José La Fuente" },
                    { 143, 8, "La Unión Norte", "Santa Rosa de Lima" },
                    { 144, 8, "La Unión Sur", "Conchagua" },
                    { 145, 8, "La Unión Sur", "El Carmen" },
                    { 146, 8, "La Unión Sur", "Intipucá" },
                    { 147, 8, "La Unión Sur", "La Unión" },
                    { 148, 8, "La Unión Sur", "Meanguera del Golfo" },
                    { 149, 8, "La Unión Sur", "San Alejo" },
                    { 150, 8, "La Unión Sur", "Yayantique" },
                    { 151, 8, "La Unión Sur", "Yucuaiquín" },
                    { 152, 9, "Usulután Norte", "Santiago de María" },
                    { 153, 9, "Usulután Norte", "Alegría" },
                    { 154, 9, "Usulután Norte", "Berlín" },
                    { 155, 9, "Usulután Norte", "Mercedes Umaña" },
                    { 156, 9, "Usulután Norte", "Jucuapa" },
                    { 157, 9, "Usulután Norte", "El Triunfo" },
                    { 158, 9, "Usulután Norte", "Estanzuelas" },
                    { 159, 9, "Usulután Norte", "San Buenaventura" },
                    { 160, 9, "Usulután Norte", "Nueva Granada" },
                    { 161, 9, "Usulután Este", "Usulután" },
                    { 162, 9, "Usulután Este", "Jucuarán" },
                    { 163, 9, "Usulután Este", "San Dionisio" },
                    { 164, 9, "Usulután Este", "Concepción Batres" },
                    { 165, 9, "Usulután Este", "Santa María" },
                    { 166, 9, "Usulután Este", "Ozatlán" },
                    { 167, 9, "Usulután Este", "Tecapán" },
                    { 168, 9, "Usulután Este", "Santa Elena" },
                    { 169, 9, "Usulután Este", "California" },
                    { 170, 9, "Usulután Este", "Ereguayquín" },
                    { 171, 9, "Usulután Oeste", "Jiquilisco" },
                    { 172, 9, "Usulután Oeste", "Puerto El Triunfo" },
                    { 173, 9, "Usulután Oeste", "San Agustín" },
                    { 174, 9, "Usulután Oeste", "San Francisco Javier" },
                    { 175, 10, "Sonsonate Norte", "Juayúa" },
                    { 176, 10, "Sonsonate Norte", "Nahuizalco" },
                    { 177, 10, "Sonsonate Norte", "Salcoatitán" },
                    { 178, 10, "Sonsonate Norte", "Santa Catarina Masahuat" },
                    { 179, 10, "Sonsonate Centro", "Sonsonate" },
                    { 180, 10, "Sonsonate Centro", "Sonzacate" },
                    { 181, 10, "Sonsonate Centro", "Nahulingo" },
                    { 182, 10, "Sonsonate Centro", "San Antonio del Monte" },
                    { 183, 10, "Sonsonate Centro", "Santo Domingo de Guzmán" },
                    { 184, 10, "Sonsonate Este", "Izalco" },
                    { 185, 10, "Sonsonate Este", "Armenia" },
                    { 186, 10, "Sonsonate Este", "Caluco" },
                    { 187, 10, "Sonsonate Este", "San Julián" },
                    { 188, 10, "Sonsonate Este", "Cuisnahuat" },
                    { 189, 10, "Sonsonate Este", "Santa Isabel Ishuatán" },
                    { 190, 10, "Sonsonate Oeste", "Acajutla" },
                    { 191, 11, "Santa Ana Norte", "Masahuat" },
                    { 192, 11, "Santa Ana Norte", "Metapán" },
                    { 193, 11, "Santa Ana Norte", "Santa Rosa Guachipilín" },
                    { 194, 11, "Santa Ana Norte", "Texistepeque" },
                    { 195, 11, "Santa Ana Centro", "Santa Ana" },
                    { 196, 11, "Santa Ana Este", "Coatepeque" },
                    { 197, 11, "Santa Ana Este", "El Congo" },
                    { 198, 11, "Santa Ana Oeste", "Candelaria de la Frontera" },
                    { 199, 11, "Santa Ana Oeste", "Chalchuapa" },
                    { 200, 11, "Santa Ana Oeste", "El Porvenir" },
                    { 201, 11, "Santa Ana Oeste", "San Antonio Pajonal" },
                    { 202, 11, "Santa Ana Oeste", "San Sebastián Salitrillo" },
                    { 203, 11, "Santa Ana Oeste", "Santiago de La Frontera" },
                    { 204, 12, "San Vicente Norte", "Apastepeque" },
                    { 205, 12, "San Vicente Norte", "Santa Clara" },
                    { 206, 12, "San Vicente Norte", "San Ildefonso" },
                    { 207, 12, "San Vicente Norte", "San Esteban Catarina" },
                    { 208, 12, "San Vicente Norte", "San Sebastián" },
                    { 209, 12, "San Vicente Norte", "San Lorenzo" },
                    { 210, 12, "San Vicente Norte", "Santo Domingo" },
                    { 211, 12, "San Vicente Sur", "San Vicente" },
                    { 212, 12, "San Vicente Sur", "Guadalupe" },
                    { 213, 12, "San Vicente Sur", "Verapaz" },
                    { 214, 12, "San Vicente Sur", "Tepetitán" },
                    { 215, 12, "San Vicente Sur", "Tecoluca" },
                    { 216, 12, "San Vicente Sur", "San Cayetano Istepeque" },
                    { 217, 13, "San Miguel Norte", "Ciudad Barrios" },
                    { 218, 13, "San Miguel Norte", "Sesori" },
                    { 219, 13, "San Miguel Norte", "Nuevo Edén de San Juan" },
                    { 220, 13, "San Miguel Norte", "San Gerardo" },
                    { 221, 13, "San Miguel Norte", "San Luis de La Reina" },
                    { 222, 13, "San Miguel Norte", "Carolina" },
                    { 223, 13, "San Miguel Norte", "San Antonio del Mosco" },
                    { 224, 13, "San Miguel Norte", "Chapeltique" },
                    { 225, 13, "San Miguel Centro", "San Miguel" },
                    { 226, 13, "San Miguel Centro", "Comacarán" },
                    { 227, 13, "San Miguel Centro", "Uluazapa" },
                    { 228, 13, "San Miguel Centro", "Moncagua" },
                    { 229, 13, "San Miguel Centro", "Quelepa" },
                    { 230, 13, "San Miguel Centro", "Chirilagua" },
                    { 231, 13, "San Miguel Oeste", "Chinameca" },
                    { 232, 13, "San Miguel Oeste", "Nueva Guadalupe" },
                    { 233, 13, "San Miguel Oeste", "Lolotique" },
                    { 234, 13, "San Miguel Oeste", "San Jorge" },
                    { 235, 13, "San Miguel Oeste", "San Rafael Oriente" },
                    { 236, 13, "San Miguel Oeste", "El Tránsito" },
                    { 237, 14, "Morazán Norte", "Arambala" },
                    { 238, 14, "Morazán Norte", "Cacaopera" },
                    { 239, 14, "Morazán Norte", "Corinto" },
                    { 240, 14, "Morazán Norte", "El Rosario" },
                    { 241, 14, "Morazán Norte", "Joateca" },
                    { 242, 14, "Morazán Norte", "Jocoaitique" },
                    { 243, 14, "Morazán Norte", "Meanguera" },
                    { 244, 14, "Morazán Norte", "Perquín" },
                    { 245, 14, "Morazán Norte", "San Fernando" },
                    { 246, 14, "Morazán Norte", "San Isidro" },
                    { 247, 14, "Morazán Norte", "Torola" },
                    { 248, 14, "Morazán Sur", "Chilanga" },
                    { 249, 14, "Morazán Sur", "Delicias de Concepción" },
                    { 250, 14, "Morazán Sur", "El Divisadero" },
                    { 251, 14, "Morazán Sur", "Gualococti" },
                    { 252, 14, "Morazán Sur", "Guatajiagua" },
                    { 253, 14, "Morazán Sur", "Jocoro" },
                    { 254, 14, "Morazán Sur", "Lolotiquillo" },
                    { 255, 14, "Morazán Sur", "Osicala" },
                    { 256, 14, "Morazán Sur", "San Carlos" },
                    { 257, 14, "Morazán Sur", "San Francisco Gotera" },
                    { 258, 14, "Morazán Sur", "San Simón" },
                    { 259, 14, "Morazán Sur", "Sensembra" },
                    { 260, 14, "Morazán Sur", "Sociedad" },
                    { 261, 14, "Morazán Sur", "Yamabal" },
                    { 262, 14, "Morazán Sur", "Yoloaiquín" }
                });

            migrationBuilder.InsertData(
                table: "RolesPermisos",
                columns: new[] { "PermisoId", "RolId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 2, 1 },
                    { 3, 1 },
                    { 4, 1 },
                    { 5, 1 },
                    { 6, 1 },
                    { 7, 1 },
                    { 8, 1 },
                    { 9, 1 },
                    { 10, 1 },
                    { 11, 1 },
                    { 12, 1 },
                    { 13, 1 },
                    { 14, 1 },
                    { 15, 1 },
                    { 16, 1 },
                    { 17, 1 },
                    { 18, 1 },
                    { 19, 1 },
                    { 20, 1 },
                    { 21, 1 },
                    { 1, 2 },
                    { 2, 2 },
                    { 3, 2 },
                    { 4, 2 },
                    { 6, 2 },
                    { 10, 2 },
                    { 11, 2 },
                    { 12, 2 },
                    { 13, 2 },
                    { 21, 2 },
                    { 21, 3 },
                    { 22, 3 },
                    { 23, 3 },
                    { 24, 3 },
                    { 25, 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Actividades_Fecha",
                table: "Actividades",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_Actividades_UsuarioId",
                table: "Actividades",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Ciudades_DepartamentoId_Nombre",
                table: "Ciudades",
                columns: new[] { "DepartamentoId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_Activo",
                table: "Clientes",
                column: "Activo");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_Apellidos_Nombres",
                table: "Clientes",
                columns: new[] { "Apellidos", "Nombres" });

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_CiudadId",
                table: "Clientes",
                column: "CiudadId");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_Correo",
                table: "Clientes",
                column: "Correo",
                unique: true,
                filter: "[Correo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_UsuarioId",
                table: "Clientes",
                column: "UsuarioId",
                unique: true,
                filter: "[UsuarioId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Departamentos_Codigo",
                table: "Departamentos",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departamentos_Nombre",
                table: "Departamentos",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DetallesPedido_PedidoId_ProductoId",
                table: "DetallesPedido",
                columns: new[] { "PedidoId", "ProductoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DetallesPedido_ProductoId",
                table: "DetallesPedido",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_ClienteId",
                table: "Pedidos",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_Estado_FechaPedido",
                table: "Pedidos",
                columns: new[] { "Estado", "FechaPedido" });

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_FechaEntrega",
                table: "Pedidos",
                column: "FechaEntrega");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_UsuarioId",
                table: "Pedidos",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Permisos_Codigo",
                table: "Permisos",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Productos_Activo",
                table: "Productos",
                column: "Activo");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_Nombre",
                table: "Productos",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_PrecioVenta",
                table: "Productos",
                column: "PrecioVenta");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Nombre",
                table: "Roles",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolesPermisos_PermisoId",
                table: "RolesPermisos",
                column: "PermisoId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Correo",
                table: "Usuarios",
                column: "Correo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_NombreUsuario",
                table: "Usuarios",
                column: "NombreUsuario",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_RolId",
                table: "Usuarios",
                column: "RolId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Actividades");

            migrationBuilder.DropTable(
                name: "DetallesPedido");

            migrationBuilder.DropTable(
                name: "RolesPermisos");

            migrationBuilder.DropTable(
                name: "Pedidos");

            migrationBuilder.DropTable(
                name: "Productos");

            migrationBuilder.DropTable(
                name: "Permisos");

            migrationBuilder.DropTable(
                name: "Clientes");

            migrationBuilder.DropTable(
                name: "Ciudades");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Departamentos");

            migrationBuilder.DropTable(
                name: "Roles");
        }
    }
}
