using GestionPedidos.Domain.Clientes;
using GestionPedidos.Domain.Pedidos;
using GestionPedidos.Domain.Productos;
using GestionPedidos.Domain.Seguridad;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GestionPedidos.Infrastructure.Tests;

[Collection(BaseDeDatosCollection.Nombre)]
public class EsquemaTests
{
    private readonly BaseDeDatosFixture _bd;
    public EsquemaTests(BaseDeDatosFixture bd) => _bd = bd;

    private static string Unico(string prefijo) => $"{prefijo}-{Guid.NewGuid():N}"[..20];

    // ---------- Semillas ----------
    [Fact]
    public async Task Semillas_RolesPermisosYMatriz()
    {
        await using var db = _bd.Contexto();
        Assert.Equal(new[] { "Administrador", "Cliente", "Operador" }, (await db.Roles.Select(r => r.Nombre).OrderBy(n => n).ToListAsync()));
        Assert.Equal(33, await db.Permisos.CountAsync());
        Assert.Equal(29, await db.RolesPermisos.CountAsync(rp => rp.RolId == 1));
        Assert.Equal(12, await db.RolesPermisos.CountAsync(rp => rp.RolId == 2));
        Assert.Equal(5, await db.RolesPermisos.CountAsync(rp => rp.RolId == 3));
    }

    [Fact]
    public async Task Semillas_GeografiaCompletaDeElSalvador()
    {
        await using var db = _bd.Contexto();
        Assert.Equal(14, await db.Departamentos.CountAsync());
        Assert.Equal(262, await db.Ciudades.CountAsync());
        var ss = await db.Departamentos.SingleAsync(d => d.Codigo == "SS");
        Assert.Equal(19, await db.Ciudades.CountAsync(c => c.DepartamentoId == ss.Id));
        Assert.True(await db.Ciudades.AnyAsync(c => c.Nombre == "Soyapango" && c.Municipio == "San Salvador Este"));
    }

    // ---------- Restricciones de la base ----------
    [Fact]
    public async Task Usuarios_NombreDeUsuarioYCorreo_SonUnicos()
    {
        await using var db = _bd.Contexto();
        var nombre = Unico("u");
        db.Usuarios.Add(Usuario.Crear(nombre, "Uno", $"{nombre}@e.com", "h", 2, false));
        await db.SaveChangesAsync();
        await using var db2 = _bd.Contexto();
        db2.Usuarios.Add(Usuario.Crear(nombre, "Dos", $"otro-{nombre}@e.com", "h", 2, false));
        await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
    }

    [Fact]
    public async Task Usuarios_SetearCreadoEnYActualizadoEn_AutomaticamenteYRowVersionCambia()
    {
        await using var db = _bd.Contexto();
        var n = Unico("a");
        var u = Usuario.Crear(n, "Auditoría", $"{n}@e.com", "h", 2, false);
        db.Usuarios.Add(u);
        await db.SaveChangesAsync();
        Assert.True(u.CreadoEn > DateTimeOffset.UtcNow.AddMinutes(-2));
        var version = u.RowVersion.ToArray();
        u.Actualizar("Otro nombre", u.Correo, 2);
        await db.SaveChangesAsync();
        Assert.NotEqual(version, u.RowVersion);
    }

    [Fact]
    public async Task Usuarios_ConcurrenciaOptimista_DetectaEdicionSimultanea()
    {
        var n = Unico("c");
        int id;
        await using (var db = _bd.Contexto())
        {
            var u = Usuario.Crear(n, "Conc", $"{n}@e.com", "h", 2, false);
            db.Usuarios.Add(u); await db.SaveChangesAsync(); id = u.Id;
        }
        await using var a = _bd.Contexto();
        await using var b = _bd.Contexto();
        var ua = await a.Usuarios.FindAsync(id); var ub = await b.Usuarios.FindAsync(id);
        ua!.Actualizar("A", ua.Correo, 2); await a.SaveChangesAsync();
        ub!.Actualizar("B", ub.Correo, 2);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync());
    }

    [Fact]
    public async Task Productos_StockYPrecioNoPuedenSerNegativos()
    {
        await using var db = _bd.Contexto();
        db.Productos.Add(new Producto { Nombre = "Malo", Descripcion = "d", CantidadStock = -1, PrecioVenta = 1, FechaIngreso = DateOnly.FromDateTime(DateTime.UtcNow) });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.Productos.Add(new Producto { Nombre = "Malo2", Descripcion = "d", CantidadStock = 1, PrecioVenta = -5, FechaIngreso = DateOnly.FromDateTime(DateTime.UtcNow) });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private async Task<(int clienteId, int usuarioId, int productoId)> Base(GestionPedidos.Infrastructure.Persistence.GestionPedidosDbContext db, string correoCliente)
    {
        var n = Unico("p");
        var u = Usuario.Crear(n, "Operador", $"{n}@e.com", "h", 2, false);
        var c = new Cliente { Nombres = "Ana", Apellidos = "Pérez", Telefono = "7777-0000", Direccion = "X", CiudadId = 1, Correo = correoCliente };
        var p = new Producto { Nombre = "Hamaca", Descripcion = "d", CantidadStock = 10, PrecioVenta = 25.50m, FechaIngreso = DateOnly.FromDateTime(DateTime.UtcNow) };
        db.AddRange(u, c, p);
        await db.SaveChangesAsync();
        return (c.Id, u.Id, p.Id);
    }

    [Fact]
    public async Task Clientes_CorreoNuloSePuedeRepetir_PeroCorreoConValorEsUnico()
    {
        await using var db = _bd.Contexto();
        db.Clientes.AddRange(
            new Cliente { Nombres = "A", Apellidos = "A", Telefono = "1", Direccion = "x", CiudadId = 1, Correo = null },
            new Cliente { Nombres = "B", Apellidos = "B", Telefono = "2", Direccion = "x", CiudadId = 1, Correo = null });
        await db.SaveChangesAsync();   // índice único FILTRADO: varios NULL conviven

        var correo = Unico("dup") + "@e.com";
        db.Clientes.Add(new Cliente { Nombres = "C", Apellidos = "C", Telefono = "3", Direccion = "x", CiudadId = 1, Correo = correo });
        await db.SaveChangesAsync();
        await using var db2 = _bd.Contexto();
        db2.Clientes.Add(new Cliente { Nombres = "D", Apellidos = "D", Telefono = "4", Direccion = "x", CiudadId = 1, Correo = correo });
        await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
    }

    [Fact]
    public async Task Pedidos_EstadoInvalido_YUnProductoDosVecesEnElMismoPedido_SeRechazan()
    {
        await using var db = _bd.Contexto();
        var (cliente, usuario, producto) = await Base(db, Unico("ped") + "@e.com");
        var pedido = new Pedido { ClienteId = cliente, UsuarioId = usuario, FechaPedido = DateTimeOffset.UtcNow, FechaEntrega = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)), Estado = "Inventado", Total = 10 };
        db.Pedidos.Add(pedido);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        db.ChangeTracker.Clear();
        var ok = new Pedido { ClienteId = cliente, UsuarioId = usuario, FechaPedido = DateTimeOffset.UtcNow, FechaEntrega = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)), Total = 51 };
        ok.Detalles.Add(new DetallePedido { ProductoId = producto, Cantidad = 2, PrecioUnitario = 25.50m });
        db.Pedidos.Add(ok);
        await db.SaveChangesAsync();
        await using var lectura = _bd.Contexto();
        Assert.Equal(51.00m, (await lectura.DetallesPedido.SingleAsync(d => d.PedidoId == ok.Id)).Subtotal);   // columna calculada por la BD

        db.Add(new DetallePedido { PedidoId = ok.Id, ProductoId = producto, Cantidad = 1, PrecioUnitario = 25.50m });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task DetallePedido_CantidadCero_SeRechaza()
    {
        await using var db = _bd.Contexto();
        var (cliente, usuario, producto) = await Base(db, Unico("det") + "@e.com");
        var pedido = new Pedido { ClienteId = cliente, UsuarioId = usuario, FechaPedido = DateTimeOffset.UtcNow, FechaEntrega = DateOnly.FromDateTime(DateTime.UtcNow), Total = 0 };
        pedido.Detalles.Add(new DetallePedido { ProductoId = producto, Cantidad = 0, PrecioUnitario = 1 });
        db.Pedidos.Add(pedido);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    // ---------- Procedimiento almacenado ----------
    [Fact]
    public async Task ProcedimientoDeBusqueda_FiltraPorClienteProductoPrecioYPagina()
    {
        await using var db = _bd.Contexto();
        var (cliente, usuario, producto) = await Base(db, Unico("busq") + "@e.com");
        var pedido = new Pedido { ClienteId = cliente, UsuarioId = usuario, FechaPedido = DateTimeOffset.UtcNow, FechaEntrega = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), Total = 51 };
        pedido.Detalles.Add(new DetallePedido { ProductoId = producto, Cantidad = 2, PrecioUnitario = 25.50m });
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();

        async Task<int> Contar(string parametros)
        {
            await using var cn = new SqlConnection(_bd.Conexion("GestionPedidosPruebas"));
            await cn.OpenAsync();
            await using var cmd = new SqlCommand($"EXEC dbo.usp_Pedidos_Buscar {parametros}", cn);
            await using var rd = await cmd.ExecuteReaderAsync();
            var n = 0; while (await rd.ReadAsync()) n++;
            return n;
        }

        Assert.Equal(1, await Contar($"@ClienteId = {cliente}"));
        Assert.Equal(1, await Contar($"@ClienteId = {cliente}, @Cliente = N'perez'"));             // sin distinguir acentos/mayúsculas
        Assert.Equal(1, await Contar($"@ClienteId = {cliente}, @ProductoId = {producto}"));
        Assert.Equal(1, await Contar($"@ClienteId = {cliente}, @PrecioMin = 25, @PrecioMax = 26"));
        Assert.Equal(0, await Contar($"@ClienteId = {cliente}, @PrecioMin = 30"));
        Assert.Equal(0, await Contar($"@ClienteId = {cliente}, @Estado = N'Cancelado'"));
        Assert.Equal(0, await Contar($"@ClienteId = {cliente}, @Cliente = N'''; DROP TABLE Pedidos; --'"));   // texto raro: parámetro, no SQL
    }

    // ---------- Mínimo privilegio y eliminación definitiva ----------

    /// <summary>Crea una base nueva con migraciones y el usuario 'pedidos_app' (como en producción); devuelve la conexión de ese usuario.</summary>
    private async Task<string> BaseConUsuarioDeLaAplicacionAsync(string bd)
    {
        await using (var cn = new SqlConnection(_bd.Servidor))
        {
            await cn.OpenAsync();
            await Ejecutar(cn, $"CREATE DATABASE {bd}");
            await Ejecutar(cn, "IF SUSER_ID('pedidos_app_t') IS NULL CREATE LOGIN pedidos_app_t WITH PASSWORD = 'App_Pruebas_Password_123!'");
        }
        await using (var cn = new SqlConnection(_bd.Conexion(bd)))
        {
            await cn.OpenAsync();
            // El usuario se llama 'pedidos_app', como en producción: la migración le aplica los DENY si existe.
            await Ejecutar(cn, "CREATE USER pedidos_app FOR LOGIN pedidos_app_t; ALTER ROLE db_datareader ADD MEMBER pedidos_app; ALTER ROLE db_datawriter ADD MEMBER pedidos_app;");
        }
        await using (var db = _bd.Contexto(bd)) await db.Database.MigrateAsync();
        return $"{_bd.Servidor.Replace("User Id=sa", "User Id=pedidos_app_t").Replace("Pruebas_Password_123!", "App_Pruebas_Password_123!")};Database={bd}";
    }

    [Fact]
    public async Task UsuarioDeLaAplicacion_NoPuedeModificarNiBorrarElHistorial()
    {
        var conexionApp = await BaseConUsuarioDeLaAplicacionAsync("PermisosPrueba");
        await using var app = new SqlConnection(conexionApp);
        await app.OpenAsync();
        await Ejecutar(app, "INSERT INTO dbo.Actividades (Fecha, Accion, Entidad) VALUES (SYSDATETIMEOFFSET(), N'Prueba', N'Usuario')");   // insertar: permitido
        var ex1 = await Assert.ThrowsAsync<SqlException>(() => Ejecutar(app, "UPDATE dbo.Actividades SET Accion = N'Alterada'"));
        var ex2 = await Assert.ThrowsAsync<SqlException>(() => Ejecutar(app, "DELETE FROM dbo.Actividades"));
        Assert.Equal(229, ex1.Number);   // permiso denegado
        Assert.Equal(229, ex2.Number);
    }

    [Fact]
    public async Task EliminarUnUsuario_ComoUsuarioDeLaAplicacion_ConservaElHistorialYElCliente_PeroUnPedidoLoImpide()
    {
        const string bd = "EliminacionPrueba";
        var conexionApp = await BaseConUsuarioDeLaAplicacionAsync(bd);
        await using var sa = new SqlConnection(_bd.Conexion(bd));
        await sa.OpenAsync();
        await Ejecutar(sa, @"
            INSERT INTO dbo.Usuarios (NombreUsuario, NombreCompleto, Correo, ClaveHash, RolId, CreadoEn, ActualizadoEn) VALUES
              (N'borrable', N'Borrable', N'borrable@e.com', N'h', 2, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()),
              (N'conpedido', N'Con Pedido', N'conpedido@e.com', N'h', 2, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
            DECLARE @u INT = (SELECT Id FROM dbo.Usuarios WHERE NombreUsuario = N'borrable');
            DECLARE @p INT = (SELECT Id FROM dbo.Usuarios WHERE NombreUsuario = N'conpedido');
            INSERT INTO dbo.Actividades (UsuarioId, Fecha, Accion, Entidad, Detalle) VALUES (@u, SYSDATETIMEOFFSET(), N'Login', N'Usuario', N'Usuario intentado: borrable');
            INSERT INTO dbo.Clientes (Nombres, Apellidos, Telefono, Direccion, CiudadId, UsuarioId, CreadoEn, ActualizadoEn) VALUES (N'Cli', N'Ente', N'1', N'x', 1, @u, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
            INSERT INTO dbo.Clientes (Nombres, Apellidos, Telefono, Direccion, CiudadId, CreadoEn, ActualizadoEn) VALUES (N'Otro', N'Cliente', N'2', N'x', 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
            INSERT INTO dbo.Pedidos (ClienteId, UsuarioId, FechaPedido, FechaEntrega, Total, CreadoEn, ActualizadoEn)
              VALUES ((SELECT TOP 1 Id FROM dbo.Clientes WHERE UsuarioId IS NULL), @p, SYSDATETIMEOFFSET(), SYSDATETIME(), 0, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());");

        await using var app = new SqlConnection(conexionApp);
        await app.OpenAsync();

        // Un usuario con un pedido a su nombre NO se puede eliminar (clave foránea restrictiva).
        var bloqueado = await Assert.ThrowsAsync<SqlException>(() => Ejecutar(app, "DELETE FROM dbo.Usuarios WHERE NombreUsuario = N'conpedido'"));
        Assert.Equal(547, bloqueado.Number);

        // Uno sin pedidos SÍ, aunque tenga historial y un cliente enlazado, y aunque pedidos_app no pueda actualizar Actividades.
        await Ejecutar(app, "DELETE FROM dbo.Usuarios WHERE NombreUsuario = N'borrable'");

        Assert.Equal(0, await Escalar(sa, "SELECT COUNT(*) FROM dbo.Usuarios WHERE NombreUsuario = N'borrable'"));
        Assert.Equal(1, await Escalar(sa, "SELECT COUNT(*) FROM dbo.Actividades WHERE Detalle LIKE N'%borrable%' AND UsuarioId IS NULL"));   // el historial queda, sin usuario
        Assert.Equal(1, await Escalar(sa, "SELECT COUNT(*) FROM dbo.Clientes WHERE Nombres = N'Cli' AND UsuarioId IS NULL"));                 // el cliente queda, sin cuenta
    }

    [Fact]
    public async Task LasTablasConBajaLogica_TienenLaColumnaEliminadoEn()
    {
        await using var cn = new SqlConnection(_bd.Conexion("GestionPedidosPruebas"));
        await cn.OpenAsync();
        foreach (var tabla in new[] { "Usuarios", "Clientes", "Productos", "Pedidos" })
            Assert.Equal(1, await Escalar(cn, $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tabla}' AND COLUMN_NAME = 'EliminadoEn' AND IS_NULLABLE = 'YES'"));
    }

    private static async Task<int> Escalar(SqlConnection cn, string sql)
    {
        await using var cmd = new SqlCommand(sql, cn);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static async Task Ejecutar(SqlConnection cn, string sql)
    {
        await using var cmd = new SqlCommand(sql, cn);
        await cmd.ExecuteNonQueryAsync();
    }
}
