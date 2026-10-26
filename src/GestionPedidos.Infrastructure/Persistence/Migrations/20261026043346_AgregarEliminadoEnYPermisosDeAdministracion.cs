using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GestionPedidos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarEliminadoEnYPermisosDeAdministracion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Actividades_Usuarios_UsuarioId",
                table: "Actividades");

            migrationBuilder.DropForeignKey(
                name: "FK_Clientes_Usuarios_UsuarioId",
                table: "Clientes");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EliminadoEn",
                table: "Usuarios",
                type: "datetimeoffset(0)",
                precision: 0,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EliminadoEn",
                table: "Productos",
                type: "datetimeoffset(0)",
                precision: 0,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EliminadoEn",
                table: "Pedidos",
                type: "datetimeoffset(0)",
                precision: 0,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EliminadoEn",
                table: "Clientes",
                type: "datetimeoffset(0)",
                precision: 0,
                nullable: true);

            migrationBuilder.InsertData(
                table: "Permisos",
                columns: new[] { "Id", "Codigo", "Descripcion" },
                values: new object[,]
                {
                    { 26, "roles.editar", "Editar los permisos de un rol" },
                    { 27, "catalogos.ver", "Ver los catálogos de departamentos y ciudades" },
                    { 28, "usuarios.eliminar", "Eliminar definitivamente usuarios dados de baja" }
                });

            migrationBuilder.InsertData(
                table: "RolesPermisos",
                columns: new[] { "PermisoId", "RolId" },
                values: new object[,]
                {
                    { 26, 1 },
                    { 27, 1 },
                    { 28, 1 },
                    { 27, 2 }
                });

            migrationBuilder.AddForeignKey(
                name: "FK_Actividades_Usuarios_UsuarioId",
                table: "Actividades",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Clientes_Usuarios_UsuarioId",
                table: "Clientes",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Actividades_Usuarios_UsuarioId",
                table: "Actividades");

            migrationBuilder.DropForeignKey(
                name: "FK_Clientes_Usuarios_UsuarioId",
                table: "Clientes");

            migrationBuilder.DeleteData(
                table: "RolesPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 26, 1 });

            migrationBuilder.DeleteData(
                table: "RolesPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 27, 1 });

            migrationBuilder.DeleteData(
                table: "RolesPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 28, 1 });

            migrationBuilder.DeleteData(
                table: "RolesPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 27, 2 });

            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DropColumn(
                name: "EliminadoEn",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "EliminadoEn",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "EliminadoEn",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "EliminadoEn",
                table: "Clientes");

            migrationBuilder.AddForeignKey(
                name: "FK_Actividades_Usuarios_UsuarioId",
                table: "Actividades",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Clientes_Usuarios_UsuarioId",
                table: "Clientes",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
