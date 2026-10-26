using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionPedidos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCodigosEImagenes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "SecuenciaCategorias",
                startValue: 1000L);

            migrationBuilder.CreateSequence<int>(
                name: "SecuenciaProductos",
                startValue: 1000L);

            // Las filas que ya existen reciben un código derivado de su Id (el mismo formato que la base genera después):
            // se agrega la columna sin restricción, se rellena y recién entonces se vuelve obligatoria con su valor por defecto.
            migrationBuilder.AddColumn<string>(name: "Codigo", table: "Productos", type: "nvarchar(30)", maxLength: 30, nullable: true);
            migrationBuilder.Sql("UPDATE dbo.Productos SET Codigo = 'PRD-' + RIGHT('000000' + CAST(Id AS varchar(10)), 6)");
            migrationBuilder.AlterColumn<string>(
                name: "Codigo",
                table: "Productos",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValueSql: "'PRD-' + RIGHT('000000' + CAST(NEXT VALUE FOR dbo.SecuenciaProductos AS varchar(10)), 6)",
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImagenClave",
                table: "Productos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            // Las filas que ya existen reciben un código derivado de su Id (el mismo formato que la base genera después):
            // se agrega la columna sin restricción, se rellena y recién entonces se vuelve obligatoria con su valor por defecto.
            migrationBuilder.AddColumn<string>(name: "Codigo", table: "Categorias", type: "nvarchar(30)", maxLength: 30, nullable: true);
            migrationBuilder.Sql("UPDATE dbo.Categorias SET Codigo = 'CAT-' + RIGHT('000000' + CAST(Id AS varchar(10)), 6)");
            migrationBuilder.AlterColumn<string>(
                name: "Codigo",
                table: "Categorias",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValueSql: "'CAT-' + RIGHT('000000' + CAST(NEXT VALUE FOR dbo.SecuenciaCategorias AS varchar(10)), 6)",
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImagenClave",
                table: "Categorias",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Productos_Codigo",
                table: "Productos",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categorias_Codigo",
                table: "Categorias",
                column: "Codigo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Productos_Codigo",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Categorias_Codigo",
                table: "Categorias");

            migrationBuilder.DropColumn(
                name: "Codigo",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "ImagenClave",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "Codigo",
                table: "Categorias");

            migrationBuilder.DropColumn(
                name: "ImagenClave",
                table: "Categorias");

            migrationBuilder.DropSequence(
                name: "SecuenciaCategorias");

            migrationBuilder.DropSequence(
                name: "SecuenciaProductos");
        }
    }
}
