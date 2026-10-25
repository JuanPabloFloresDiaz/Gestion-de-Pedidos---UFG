using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionPedidos.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Vista y procedimiento de búsqueda de pedidos (solo lectura) y permisos del usuario de la aplicación.
    /// Se versionan con el resto de migraciones ([BaseDatos.md §6 y §7]).
    /// </summary>
    public partial class AgregarBusquedaDePedidosYPermisosDeBD : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE OR ALTER VIEW dbo.vw_PedidosResumen AS
SELECT p.Id, p.ClienteId,
       c.Nombres + N' ' + c.Apellidos AS Cliente,
       c.Apellidos AS ClienteApellidos,
       p.UsuarioId, u.NombreCompleto AS Usuario,
       p.FechaPedido, p.FechaEntrega, p.Estado, p.Total, p.Activo, p.Comentarios,
       (SELECT COUNT(*) FROM dbo.DetallesPedido d WHERE d.PedidoId = p.Id) AS Lineas
FROM dbo.Pedidos p
JOIN dbo.Clientes c ON c.Id = p.ClienteId
JOIN dbo.Usuarios u ON u.Id = p.UsuarioId;");

            migrationBuilder.Sql(@"
CREATE OR ALTER PROCEDURE dbo.usp_Pedidos_Buscar
    @ClienteId INT = NULL,
    @Cliente NVARCHAR(200) = NULL,
    @ProductoId INT = NULL,
    @Producto NVARCHAR(150) = NULL,
    @PrecioMin DECIMAL(18,2) = NULL,
    @PrecioMax DECIMAL(18,2) = NULL,
    @Estado NVARCHAR(20) = NULL,
    @PedidoDesde DATETIMEOFFSET(0) = NULL,
    @PedidoHasta DATETIMEOFFSET(0) = NULL,
    @EntregaDesde DATE = NULL,
    @EntregaHasta DATE = NULL,
    @UsuarioId INT = NULL,
    @IncluirInactivos BIT = 0,
    @Pagina INT = 1,
    @TamanoPagina INT = 20
AS
BEGIN
    SET NOCOUNT ON;
    IF @Pagina < 1 SET @Pagina = 1;
    IF @TamanoPagina < 1 SET @TamanoPagina = 1;
    IF @TamanoPagina > 100 SET @TamanoPagina = 100;

    SELECT v.*, COUNT(*) OVER() AS TotalFilas
    FROM dbo.vw_PedidosResumen v
    WHERE (@IncluirInactivos = 1 OR v.Activo = 1)
      AND (@ClienteId IS NULL OR v.ClienteId = @ClienteId)
      AND (@Cliente IS NULL OR v.Cliente LIKE N'%' + @Cliente + N'%' COLLATE Latin1_General_CI_AI)
      AND (@Estado IS NULL OR v.Estado = @Estado)
      AND (@PedidoDesde IS NULL OR v.FechaPedido >= @PedidoDesde)
      AND (@PedidoHasta IS NULL OR v.FechaPedido <= @PedidoHasta)
      AND (@EntregaDesde IS NULL OR v.FechaEntrega >= @EntregaDesde)
      AND (@EntregaHasta IS NULL OR v.FechaEntrega <= @EntregaHasta)
      AND (@UsuarioId IS NULL OR v.UsuarioId = @UsuarioId)
      AND ((@ProductoId IS NULL AND @Producto IS NULL) OR EXISTS (
            SELECT 1 FROM dbo.DetallesPedido d JOIN dbo.Productos pr ON pr.Id = d.ProductoId
            WHERE d.PedidoId = v.Id
              AND (@ProductoId IS NULL OR d.ProductoId = @ProductoId)
              AND (@Producto IS NULL OR pr.Nombre LIKE N'%' + @Producto + N'%' COLLATE Latin1_General_CI_AI)))
      AND ((@PrecioMin IS NULL AND @PrecioMax IS NULL) OR EXISTS (
            SELECT 1 FROM dbo.DetallesPedido d
            WHERE d.PedidoId = v.Id
              AND (@PrecioMin IS NULL OR d.PrecioUnitario >= @PrecioMin)
              AND (@PrecioMax IS NULL OR d.PrecioUnitario <= @PrecioMax)))
    ORDER BY v.FechaPedido DESC, v.Id DESC
    OFFSET (@Pagina - 1) * @TamanoPagina ROWS FETCH NEXT @TamanoPagina ROWS ONLY;
END;");

            // Permisos del usuario de la aplicación: solo si existe (en pruebas con 'sa' no existe).
            migrationBuilder.Sql(@"
IF USER_ID(N'pedidos_app') IS NOT NULL
BEGIN
    GRANT EXECUTE ON OBJECT::dbo.usp_Pedidos_Buscar TO pedidos_app;
    GRANT SELECT ON OBJECT::dbo.vw_PedidosResumen TO pedidos_app;
    -- El historial es inalterable: la aplicación solo inserta y consulta.
    DENY UPDATE, DELETE ON OBJECT::dbo.Actividades TO pedidos_app;
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_Pedidos_Buscar;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_PedidosResumen;");
            migrationBuilder.Sql(@"
IF USER_ID(N'pedidos_app') IS NOT NULL
    REVOKE UPDATE, DELETE ON OBJECT::dbo.Actividades FROM pedidos_app;");
        }
    }
}
