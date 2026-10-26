using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GestionPedidos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCatalogoDeProductos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SubcategoriaId",
                table: "Productos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Categorias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EliminadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categorias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subcategorias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoriaId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EliminadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subcategorias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subcategorias_Categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Filtros",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubcategoriaId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EliminadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Filtros", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Filtros_Subcategorias_SubcategoriaId",
                        column: x => x.SubcategoriaId,
                        principalTable: "Subcategorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpcionesFiltro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FiltroId = table.Column<int>(type: "int", nullable: false),
                    Valor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EliminadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpcionesFiltro", x => x.Id);
                    table.UniqueConstraint("AK_OpcionesFiltro_Id_FiltroId", x => new { x.Id, x.FiltroId });
                    table.ForeignKey(
                        name: "FK_OpcionesFiltro_Filtros_FiltroId",
                        column: x => x.FiltroId,
                        principalTable: "Filtros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductosOpciones",
                columns: table => new
                {
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    FiltroId = table.Column<int>(type: "int", nullable: false),
                    OpcionFiltroId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductosOpciones", x => new { x.ProductoId, x.FiltroId });
                    table.ForeignKey(
                        name: "FK_ProductosOpciones_Filtros_FiltroId",
                        column: x => x.FiltroId,
                        principalTable: "Filtros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductosOpciones_OpcionesFiltro_OpcionFiltroId_FiltroId",
                        columns: x => new { x.OpcionFiltroId, x.FiltroId },
                        principalTable: "OpcionesFiltro",
                        principalColumns: new[] { "Id", "FiltroId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductosOpciones_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Permisos",
                columns: new[] { "Id", "Codigo", "Descripcion" },
                values: new object[,]
                {
                    { 29, "categorias.ver", "Ver categorías, subcategorías, filtros y opciones" },
                    { 30, "categorias.crear", "Crear categorías, subcategorías, filtros y opciones" },
                    { 31, "categorias.editar", "Editar categorías, subcategorías, filtros y opciones" },
                    { 32, "categorias.desactivar", "Dar de baja o reactivar categorías, subcategorías, filtros y opciones" },
                    { 33, "categorias.eliminar", "Eliminar definitivamente elementos inactivos y sin uso de la clasificación" }
                });

            migrationBuilder.InsertData(
                table: "RolesPermisos",
                columns: new[] { "PermisoId", "RolId" },
                values: new object[,]
                {
                    { 29, 1 },
                    { 30, 1 },
                    { 31, 1 },
                    { 32, 1 },
                    { 33, 1 },
                    { 29, 2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Productos_SubcategoriaId",
                table: "Productos",
                column: "SubcategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Categorias_Nombre",
                table: "Categorias",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Filtros_SubcategoriaId_Nombre",
                table: "Filtros",
                columns: new[] { "SubcategoriaId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpcionesFiltro_FiltroId_Valor",
                table: "OpcionesFiltro",
                columns: new[] { "FiltroId", "Valor" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductosOpciones_FiltroId",
                table: "ProductosOpciones",
                column: "FiltroId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductosOpciones_OpcionFiltroId",
                table: "ProductosOpciones",
                column: "OpcionFiltroId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductosOpciones_OpcionFiltroId_FiltroId",
                table: "ProductosOpciones",
                columns: new[] { "OpcionFiltroId", "FiltroId" });

            migrationBuilder.CreateIndex(
                name: "IX_Subcategorias_CategoriaId_Nombre",
                table: "Subcategorias",
                columns: new[] { "CategoriaId", "Nombre" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_Subcategorias_SubcategoriaId",
                table: "Productos",
                column: "SubcategoriaId",
                principalTable: "Subcategorias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Productos_Subcategorias_SubcategoriaId",
                table: "Productos");

            migrationBuilder.DropTable(
                name: "ProductosOpciones");

            migrationBuilder.DropTable(
                name: "OpcionesFiltro");

            migrationBuilder.DropTable(
                name: "Filtros");

            migrationBuilder.DropTable(
                name: "Subcategorias");

            migrationBuilder.DropTable(
                name: "Categorias");

            migrationBuilder.DropIndex(
                name: "IX_Productos_SubcategoriaId",
                table: "Productos");

            migrationBuilder.DeleteData(
                table: "RolesPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 29, 1 });

            migrationBuilder.DeleteData(
                table: "RolesPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 30, 1 });

            migrationBuilder.DeleteData(
                table: "RolesPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 31, 1 });

            migrationBuilder.DeleteData(
                table: "RolesPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 32, 1 });

            migrationBuilder.DeleteData(
                table: "RolesPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 33, 1 });

            migrationBuilder.DeleteData(
                table: "RolesPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 29, 2 });

            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DropColumn(
                name: "SubcategoriaId",
                table: "Productos");
        }
    }
}
