using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionPedidos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTelefonoSecundarioCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TelefonoSecundario",
                table: "Clientes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TelefonoSecundario",
                table: "Clientes");
        }
    }
}
