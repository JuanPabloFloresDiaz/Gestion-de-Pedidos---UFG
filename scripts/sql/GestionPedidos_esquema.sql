-- ============================================================================================
-- Gestión de Pedidos (UFG) — Esquema COMPLETO de la base de datos GestionPedidos (SQL Server)
-- Generado con:  dotnet ef migrations script --idempotent   (a partir de las migraciones de EF Core)
-- NO editar a mano: si cambia el modelo, se crea una migración nueva y se regenera este archivo
-- (scripts/generar-script-sql.sh).  Es IDEMPOTENTE: se puede ejecutar varias veces sin duplicar nada.
--
-- Contiene: 11 tablas, restricciones (PK, FK, UNIQUE, CHECK), índices, columna calculada,
--           semillas (roles, 25 permisos, matriz de permisos, 14 departamentos y 262 distritos),
--           vista vw_PedidosResumen y procedimiento usp_Pedidos_Buscar.
-- Requisito: la base de datos GestionPedidos ya existe (ver scripts/sql/init-usuarios.sql).
-- ============================================================================================

﻿IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE TABLE [Departamentos] (
        [Id] int NOT NULL IDENTITY,
        [Codigo] nvarchar(3) NOT NULL,
        [Nombre] nvarchar(60) NOT NULL,
        CONSTRAINT [PK_Departamentos] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE TABLE [Permisos] (
        [Id] int NOT NULL IDENTITY,
        [Codigo] nvarchar(60) NOT NULL,
        [Descripcion] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_Permisos] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE TABLE [Productos] (
        [Id] int NOT NULL IDENTITY,
        [Nombre] nvarchar(150) NOT NULL,
        [Descripcion] nvarchar(500) NOT NULL,
        [CantidadStock] int NOT NULL,
        [PrecioVenta] decimal(18,2) NOT NULL,
        [FechaIngreso] date NOT NULL DEFAULT (CONVERT(date, SYSUTCDATETIME())),
        [MostrarStock] bit NOT NULL DEFAULT CAST(0 AS bit),
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreadoEn] datetimeoffset(0) NOT NULL,
        [ActualizadoEn] datetimeoffset(0) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Productos] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Productos_CantidadStock] CHECK ([CantidadStock] >= 0),
        CONSTRAINT [CK_Productos_PrecioVenta] CHECK ([PrecioVenta] >= 0)
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] int NOT NULL IDENTITY,
        [Nombre] nvarchar(50) NOT NULL,
        [Descripcion] nvarchar(200) NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE TABLE [Ciudades] (
        [Id] int NOT NULL IDENTITY,
        [DepartamentoId] int NOT NULL,
        [Municipio] nvarchar(80) NOT NULL,
        [Nombre] nvarchar(80) NOT NULL,
        CONSTRAINT [PK_Ciudades] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Ciudades_Departamentos_DepartamentoId] FOREIGN KEY ([DepartamentoId]) REFERENCES [Departamentos] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE TABLE [RolesPermisos] (
        [RolId] int NOT NULL,
        [PermisoId] int NOT NULL,
        CONSTRAINT [PK_RolesPermisos] PRIMARY KEY ([RolId], [PermisoId]),
        CONSTRAINT [FK_RolesPermisos_Permisos_PermisoId] FOREIGN KEY ([PermisoId]) REFERENCES [Permisos] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_RolesPermisos_Roles_RolId] FOREIGN KEY ([RolId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE TABLE [Usuarios] (
        [Id] int NOT NULL IDENTITY,
        [NombreUsuario] nvarchar(150) NOT NULL,
        [NombreCompleto] nvarchar(150) NOT NULL,
        [Correo] nvarchar(150) NOT NULL,
        [ClaveHash] nvarchar(255) NOT NULL,
        [RolId] int NOT NULL,
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        [DebeCambiarClave] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IntentosFallidos] int NOT NULL DEFAULT 0,
        [BloqueadoHasta] datetimeoffset(0) NULL,
        [UltimoAcceso] datetimeoffset(0) NULL,
        [CreadoEn] datetimeoffset(0) NOT NULL,
        [ActualizadoEn] datetimeoffset(0) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Usuarios] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Usuarios_Roles_RolId] FOREIGN KEY ([RolId]) REFERENCES [Roles] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE TABLE [Actividades] (
        [Id] bigint NOT NULL IDENTITY,
        [UsuarioId] int NULL,
        [Fecha] datetimeoffset(0) NOT NULL,
        [Accion] nvarchar(40) NOT NULL,
        [Entidad] nvarchar(40) NOT NULL,
        [EntidadId] int NULL,
        [Detalle] nvarchar(500) NULL,
        CONSTRAINT [PK_Actividades] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Actividades_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE TABLE [Clientes] (
        [Id] int NOT NULL IDENTITY,
        [Nombres] nvarchar(100) NOT NULL,
        [Apellidos] nvarchar(100) NOT NULL,
        [Telefono] nvarchar(20) NOT NULL,
        [Direccion] nvarchar(250) NOT NULL,
        [Correo] nvarchar(150) NULL,
        [CiudadId] int NOT NULL,
        [UsuarioId] int NULL,
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreadoEn] datetimeoffset(0) NOT NULL,
        [ActualizadoEn] datetimeoffset(0) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Clientes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Clientes_Ciudades_CiudadId] FOREIGN KEY ([CiudadId]) REFERENCES [Ciudades] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Clientes_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE TABLE [Pedidos] (
        [Id] int NOT NULL IDENTITY,
        [ClienteId] int NOT NULL,
        [UsuarioId] int NOT NULL,
        [FechaPedido] datetimeoffset(0) NOT NULL,
        [FechaEntrega] date NOT NULL,
        [Comentarios] nvarchar(500) NULL,
        [Estado] nvarchar(20) NOT NULL DEFAULT N'Pendiente',
        [Total] decimal(18,2) NOT NULL,
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreadoEn] datetimeoffset(0) NOT NULL,
        [ActualizadoEn] datetimeoffset(0) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Pedidos] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Pedidos_Estado] CHECK ([Estado] IN (N'Pendiente', N'Entregado', N'Cancelado')),
        CONSTRAINT [CK_Pedidos_Total] CHECK ([Total] >= 0),
        CONSTRAINT [FK_Pedidos_Clientes_ClienteId] FOREIGN KEY ([ClienteId]) REFERENCES [Clientes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Pedidos_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE TABLE [DetallesPedido] (
        [Id] int NOT NULL IDENTITY,
        [PedidoId] int NOT NULL,
        [ProductoId] int NOT NULL,
        [Cantidad] int NOT NULL,
        [PrecioUnitario] decimal(18,2) NOT NULL,
        [Subtotal] AS [Cantidad] * [PrecioUnitario] PERSISTED,
        CONSTRAINT [PK_DetallesPedido] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_DetallesPedido_Cantidad] CHECK ([Cantidad] > 0),
        CONSTRAINT [CK_DetallesPedido_PrecioUnitario] CHECK ([PrecioUnitario] >= 0),
        CONSTRAINT [FK_DetallesPedido_Pedidos_PedidoId] FOREIGN KEY ([PedidoId]) REFERENCES [Pedidos] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_DetallesPedido_Productos_ProductoId] FOREIGN KEY ([ProductoId]) REFERENCES [Productos] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Codigo', N'Nombre') AND [object_id] = OBJECT_ID(N'[Departamentos]'))
        SET IDENTITY_INSERT [Departamentos] ON;
    EXEC(N'INSERT INTO [Departamentos] ([Id], [Codigo], [Nombre])
    VALUES (1, N''AH'', N''Ahuachapán''),
    (2, N''SS'', N''San Salvador''),
    (3, N''LI'', N''La Libertad''),
    (4, N''CH'', N''Chalatenango''),
    (5, N''CU'', N''Cuscatlán''),
    (6, N''CA'', N''Cabañas''),
    (7, N''PA'', N''La Paz''),
    (8, N''UN'', N''La Unión''),
    (9, N''US'', N''Usulután''),
    (10, N''SO'', N''Sonsonate''),
    (11, N''SA'', N''Santa Ana''),
    (12, N''SV'', N''San Vicente''),
    (13, N''SM'', N''San Miguel''),
    (14, N''MO'', N''Morazán'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Codigo', N'Nombre') AND [object_id] = OBJECT_ID(N'[Departamentos]'))
        SET IDENTITY_INSERT [Departamentos] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Codigo', N'Descripcion') AND [object_id] = OBJECT_ID(N'[Permisos]'))
        SET IDENTITY_INSERT [Permisos] ON;
    EXEC(N'INSERT INTO [Permisos] ([Id], [Codigo], [Descripcion])
    VALUES (1, N''admin.acceso'', N''Entrar al área de Administración''),
    (2, N''clientes.ver'', N''Ver clientes''),
    (3, N''clientes.crear'', N''Crear clientes''),
    (4, N''clientes.editar'', N''Editar clientes''),
    (5, N''clientes.desactivar'', N''Dar de baja o reactivar clientes''),
    (6, N''productos.ver'', N''Ver productos''),
    (7, N''productos.crear'', N''Crear productos''),
    (8, N''productos.editar'', N''Editar productos''),
    (9, N''productos.desactivar'', N''Dar de baja o reactivar productos''),
    (10, N''pedidos.ver'', N''Ver pedidos''),
    (11, N''pedidos.crear'', N''Crear pedidos''),
    (12, N''pedidos.editar'', N''Editar pedidos''),
    (13, N''pedidos.cambiar_estado'', N''Cambiar el estado de un pedido''),
    (14, N''pedidos.desactivar'', N''Dar de baja pedidos''),
    (15, N''usuarios.ver'', N''Ver usuarios''),
    (16, N''usuarios.crear'', N''Crear usuarios''),
    (17, N''usuarios.editar'', N''Editar usuarios''),
    (18, N''usuarios.desactivar'', N''Dar de baja o reactivar usuarios''),
    (19, N''roles.ver'', N''Ver roles y permisos''),
    (20, N''historial.ver'', N''Ver el historial de actividades''),
    (21, N''catalogo.ver'', N''Ver el catálogo de la tienda''),
    (22, N''mispedidos.ver'', N''Ver los pedidos propios''),
    (23, N''mispedidos.crear'', N''Crear pedidos propios''),
    (24, N''mispedidos.cancelar'', N''Cancelar pedidos propios''),
    (25, N''perfil.editar'', N''Editar el perfil propio'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Codigo', N'Descripcion') AND [object_id] = OBJECT_ID(N'[Permisos]'))
        SET IDENTITY_INSERT [Permisos] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Descripcion', N'Nombre') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] ON;
    EXEC(N'INSERT INTO [Roles] ([Id], [Descripcion], [Nombre])
    VALUES (1, N''Gestión completa, usuarios e historial'', N''Administrador''),
    (2, N''Trabajo diario con clientes, productos (consulta) y pedidos'', N''Operador''),
    (3, N''Cliente de la tienda web'', N''Cliente'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Descripcion', N'Nombre') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DepartamentoId', N'Municipio', N'Nombre') AND [object_id] = OBJECT_ID(N'[Ciudades]'))
        SET IDENTITY_INSERT [Ciudades] ON;
    EXEC(N'INSERT INTO [Ciudades] ([Id], [DepartamentoId], [Municipio], [Nombre])
    VALUES (1, 1, N''Ahuachapán Norte'', N''Atiquizaya''),
    (2, 1, N''Ahuachapán Norte'', N''El Refugio''),
    (3, 1, N''Ahuachapán Norte'', N''San Lorenzo''),
    (4, 1, N''Ahuachapán Norte'', N''Turín''),
    (5, 1, N''Ahuachapán Centro'', N''Ahuachapán''),
    (6, 1, N''Ahuachapán Centro'', N''Apaneca''),
    (7, 1, N''Ahuachapán Centro'', N''Concepción de Ataco''),
    (8, 1, N''Ahuachapán Centro'', N''Tacuba''),
    (9, 1, N''Ahuachapán Sur'', N''Guaymango''),
    (10, 1, N''Ahuachapán Sur'', N''Jujutla''),
    (11, 1, N''Ahuachapán Sur'', N''San Francisco Menéndez''),
    (12, 1, N''Ahuachapán Sur'', N''San Pedro Puxtla''),
    (13, 2, N''San Salvador Norte'', N''Aguilares''),
    (14, 2, N''San Salvador Norte'', N''El Paisnal''),
    (15, 2, N''San Salvador Norte'', N''Guazapa''),
    (16, 2, N''San Salvador Oeste'', N''Apopa''),
    (17, 2, N''San Salvador Oeste'', N''Nejapa''),
    (18, 2, N''San Salvador Este'', N''Ilopango''),
    (19, 2, N''San Salvador Este'', N''San Martín''),
    (20, 2, N''San Salvador Este'', N''Soyapango''),
    (21, 2, N''San Salvador Este'', N''Tonacatepeque''),
    (22, 2, N''San Salvador Centro'', N''Ayutuxtepeque''),
    (23, 2, N''San Salvador Centro'', N''Mejicanos''),
    (24, 2, N''San Salvador Centro'', N''San Salvador''),
    (25, 2, N''San Salvador Centro'', N''Cuscatancingo''),
    (26, 2, N''San Salvador Centro'', N''Ciudad Delgado''),
    (27, 2, N''San Salvador Sur'', N''Panchimalco''),
    (28, 2, N''San Salvador Sur'', N''Rosario de Mora''),
    (29, 2, N''San Salvador Sur'', N''San Marcos''),
    (30, 2, N''San Salvador Sur'', N''Santo Tomás''),
    (31, 2, N''San Salvador Sur'', N''Santiago Texacuangos''),
    (32, 3, N''La Libertad Norte'', N''Quezaltepeque''),
    (33, 3, N''La Libertad Norte'', N''San Matías''),
    (34, 3, N''La Libertad Norte'', N''San Pablo Tacachico''),
    (35, 3, N''La Libertad Centro'', N''San Juan Opico''),
    (36, 3, N''La Libertad Centro'', N''Ciudad Arce''),
    (37, 3, N''La Libertad Oeste'', N''Colón''),
    (38, 3, N''La Libertad Oeste'', N''Jayaque''),
    (39, 3, N''La Libertad Oeste'', N''Sacacoyo''),
    (40, 3, N''La Libertad Oeste'', N''Tepecoyo''),
    (41, 3, N''La Libertad Oeste'', N''Talnique''),
    (42, 3, N''La Libertad Este'', N''Antiguo Cuscatlán'');
    INSERT INTO [Ciudades] ([Id], [DepartamentoId], [Municipio], [Nombre])
    VALUES (43, 3, N''La Libertad Este'', N''Huizúcar''),
    (44, 3, N''La Libertad Este'', N''Nuevo Cuscatlán''),
    (45, 3, N''La Libertad Este'', N''San José Villanueva''),
    (46, 3, N''La Libertad Este'', N''Zaragoza''),
    (47, 3, N''La Libertad Costa'', N''Chiltiupán''),
    (48, 3, N''La Libertad Costa'', N''Jicalapa''),
    (49, 3, N''La Libertad Costa'', N''La Libertad''),
    (50, 3, N''La Libertad Costa'', N''Tamanique''),
    (51, 3, N''La Libertad Costa'', N''Teotepeque''),
    (52, 3, N''La Libertad Sur'', N''Comasagua''),
    (53, 3, N''La Libertad Sur'', N''Santa Tecla''),
    (54, 4, N''Chalatenango Norte'', N''La Palma''),
    (55, 4, N''Chalatenango Norte'', N''Citalá''),
    (56, 4, N''Chalatenango Norte'', N''San Ignacio''),
    (57, 4, N''Chalatenango Centro'', N''Nueva Concepción''),
    (58, 4, N''Chalatenango Centro'', N''Tejutla''),
    (59, 4, N''Chalatenango Centro'', N''La Reina''),
    (60, 4, N''Chalatenango Centro'', N''Agua Caliente''),
    (61, 4, N''Chalatenango Centro'', N''Dulce Nombre de María''),
    (62, 4, N''Chalatenango Centro'', N''El Paraíso''),
    (63, 4, N''Chalatenango Centro'', N''San Fernando''),
    (64, 4, N''Chalatenango Centro'', N''San Francisco Morazán''),
    (65, 4, N''Chalatenango Centro'', N''San Rafael''),
    (66, 4, N''Chalatenango Centro'', N''Santa Rita''),
    (67, 4, N''Chalatenango Sur'', N''Chalatenango''),
    (68, 4, N''Chalatenango Sur'', N''Arcatao''),
    (69, 4, N''Chalatenango Sur'', N''Azacualpa''),
    (70, 4, N''Chalatenango Sur'', N''Comalapa''),
    (71, 4, N''Chalatenango Sur'', N''Concepción Quezaltepeque''),
    (72, 4, N''Chalatenango Sur'', N''El Carrizal''),
    (73, 4, N''Chalatenango Sur'', N''La Laguna''),
    (74, 4, N''Chalatenango Sur'', N''Las Vueltas''),
    (75, 4, N''Chalatenango Sur'', N''Nombre de Jesús''),
    (76, 4, N''Chalatenango Sur'', N''Nueva Trinidad''),
    (77, 4, N''Chalatenango Sur'', N''Ojos de Agua''),
    (78, 4, N''Chalatenango Sur'', N''Potónico''),
    (79, 4, N''Chalatenango Sur'', N''San Antonio de La Cruz''),
    (80, 4, N''Chalatenango Sur'', N''San Antonio Los Ranchos''),
    (81, 4, N''Chalatenango Sur'', N''San Francisco Lempa''),
    (82, 4, N''Chalatenango Sur'', N''San Isidro Labrador''),
    (83, 4, N''Chalatenango Sur'', N''San José Cancasque''),
    (84, 4, N''Chalatenango Sur'', N''San Miguel de Mercedes'');
    INSERT INTO [Ciudades] ([Id], [DepartamentoId], [Municipio], [Nombre])
    VALUES (85, 4, N''Chalatenango Sur'', N''San José Las Flores''),
    (86, 4, N''Chalatenango Sur'', N''San Luis del Carmen''),
    (87, 5, N''Cuscatlán Norte'', N''Suchitoto''),
    (88, 5, N''Cuscatlán Norte'', N''San José Guayabal''),
    (89, 5, N''Cuscatlán Norte'', N''Oratorio de Concepción''),
    (90, 5, N''Cuscatlán Norte'', N''San Bartolomé Perulapía''),
    (91, 5, N''Cuscatlán Norte'', N''San Pedro Perulapán''),
    (92, 5, N''Cuscatlán Sur'', N''Cojutepeque''),
    (93, 5, N''Cuscatlán Sur'', N''San Rafael Cedros''),
    (94, 5, N''Cuscatlán Sur'', N''Candelaria''),
    (95, 5, N''Cuscatlán Sur'', N''Monte San Juan''),
    (96, 5, N''Cuscatlán Sur'', N''El Carmen''),
    (97, 5, N''Cuscatlán Sur'', N''San Cristóbal''),
    (98, 5, N''Cuscatlán Sur'', N''Santa Cruz Michapa''),
    (99, 5, N''Cuscatlán Sur'', N''San Ramón''),
    (100, 5, N''Cuscatlán Sur'', N''El Rosario''),
    (101, 5, N''Cuscatlán Sur'', N''Santa Cruz Analquito''),
    (102, 5, N''Cuscatlán Sur'', N''Tenancingo''),
    (103, 6, N''Cabañas Este'', N''Sensuntepeque''),
    (104, 6, N''Cabañas Este'', N''Victoria''),
    (105, 6, N''Cabañas Este'', N''Dolores''),
    (106, 6, N''Cabañas Este'', N''Guacotecti''),
    (107, 6, N''Cabañas Este'', N''San Isidro''),
    (108, 6, N''Cabañas Oeste'', N''Ilobasco''),
    (109, 6, N''Cabañas Oeste'', N''Tejutepeque''),
    (110, 6, N''Cabañas Oeste'', N''Jutiapa''),
    (111, 6, N''Cabañas Oeste'', N''Cinquera''),
    (112, 7, N''La Paz Oeste'', N''Cuyultitán''),
    (113, 7, N''La Paz Oeste'', N''Olocuilta''),
    (114, 7, N''La Paz Oeste'', N''San Juan Talpa''),
    (115, 7, N''La Paz Oeste'', N''San Luis Talpa''),
    (116, 7, N''La Paz Oeste'', N''San Pedro Masahuat''),
    (117, 7, N''La Paz Oeste'', N''Tapalhuaca''),
    (118, 7, N''La Paz Oeste'', N''San Francisco Chinameca''),
    (119, 7, N''La Paz Centro'', N''El Rosario''),
    (120, 7, N''La Paz Centro'', N''Jerusalén''),
    (121, 7, N''La Paz Centro'', N''Mercedes La Ceiba''),
    (122, 7, N''La Paz Centro'', N''Paraíso de Osorio''),
    (123, 7, N''La Paz Centro'', N''San Antonio Masahuat''),
    (124, 7, N''La Paz Centro'', N''San Emigdio''),
    (125, 7, N''La Paz Centro'', N''San Juan Tepezontes''),
    (126, 7, N''La Paz Centro'', N''San Luis La Herradura'');
    INSERT INTO [Ciudades] ([Id], [DepartamentoId], [Municipio], [Nombre])
    VALUES (127, 7, N''La Paz Centro'', N''San Miguel Tepezontes''),
    (128, 7, N''La Paz Centro'', N''San Pedro Nonualco''),
    (129, 7, N''La Paz Centro'', N''Santa María Ostuma''),
    (130, 7, N''La Paz Centro'', N''Santiago Nonualco''),
    (131, 7, N''La Paz Este'', N''San Juan Nonualco''),
    (132, 7, N''La Paz Este'', N''San Rafael Obrajuelo''),
    (133, 7, N''La Paz Este'', N''Zacatecoluca''),
    (134, 8, N''La Unión Norte'', N''Anamorós''),
    (135, 8, N''La Unión Norte'', N''Bolívar''),
    (136, 8, N''La Unión Norte'', N''Concepción de Oriente''),
    (137, 8, N''La Unión Norte'', N''El Sauce''),
    (138, 8, N''La Unión Norte'', N''Lislique''),
    (139, 8, N''La Unión Norte'', N''Nueva Esparta''),
    (140, 8, N''La Unión Norte'', N''Pasaquina''),
    (141, 8, N''La Unión Norte'', N''Polorós''),
    (142, 8, N''La Unión Norte'', N''San José La Fuente''),
    (143, 8, N''La Unión Norte'', N''Santa Rosa de Lima''),
    (144, 8, N''La Unión Sur'', N''Conchagua''),
    (145, 8, N''La Unión Sur'', N''El Carmen''),
    (146, 8, N''La Unión Sur'', N''Intipucá''),
    (147, 8, N''La Unión Sur'', N''La Unión''),
    (148, 8, N''La Unión Sur'', N''Meanguera del Golfo''),
    (149, 8, N''La Unión Sur'', N''San Alejo''),
    (150, 8, N''La Unión Sur'', N''Yayantique''),
    (151, 8, N''La Unión Sur'', N''Yucuaiquín''),
    (152, 9, N''Usulután Norte'', N''Santiago de María''),
    (153, 9, N''Usulután Norte'', N''Alegría''),
    (154, 9, N''Usulután Norte'', N''Berlín''),
    (155, 9, N''Usulután Norte'', N''Mercedes Umaña''),
    (156, 9, N''Usulután Norte'', N''Jucuapa''),
    (157, 9, N''Usulután Norte'', N''El Triunfo''),
    (158, 9, N''Usulután Norte'', N''Estanzuelas''),
    (159, 9, N''Usulután Norte'', N''San Buenaventura''),
    (160, 9, N''Usulután Norte'', N''Nueva Granada''),
    (161, 9, N''Usulután Este'', N''Usulután''),
    (162, 9, N''Usulután Este'', N''Jucuarán''),
    (163, 9, N''Usulután Este'', N''San Dionisio''),
    (164, 9, N''Usulután Este'', N''Concepción Batres''),
    (165, 9, N''Usulután Este'', N''Santa María''),
    (166, 9, N''Usulután Este'', N''Ozatlán''),
    (167, 9, N''Usulután Este'', N''Tecapán''),
    (168, 9, N''Usulután Este'', N''Santa Elena'');
    INSERT INTO [Ciudades] ([Id], [DepartamentoId], [Municipio], [Nombre])
    VALUES (169, 9, N''Usulután Este'', N''California''),
    (170, 9, N''Usulután Este'', N''Ereguayquín''),
    (171, 9, N''Usulután Oeste'', N''Jiquilisco''),
    (172, 9, N''Usulután Oeste'', N''Puerto El Triunfo''),
    (173, 9, N''Usulután Oeste'', N''San Agustín''),
    (174, 9, N''Usulután Oeste'', N''San Francisco Javier''),
    (175, 10, N''Sonsonate Norte'', N''Juayúa''),
    (176, 10, N''Sonsonate Norte'', N''Nahuizalco''),
    (177, 10, N''Sonsonate Norte'', N''Salcoatitán''),
    (178, 10, N''Sonsonate Norte'', N''Santa Catarina Masahuat''),
    (179, 10, N''Sonsonate Centro'', N''Sonsonate''),
    (180, 10, N''Sonsonate Centro'', N''Sonzacate''),
    (181, 10, N''Sonsonate Centro'', N''Nahulingo''),
    (182, 10, N''Sonsonate Centro'', N''San Antonio del Monte''),
    (183, 10, N''Sonsonate Centro'', N''Santo Domingo de Guzmán''),
    (184, 10, N''Sonsonate Este'', N''Izalco''),
    (185, 10, N''Sonsonate Este'', N''Armenia''),
    (186, 10, N''Sonsonate Este'', N''Caluco''),
    (187, 10, N''Sonsonate Este'', N''San Julián''),
    (188, 10, N''Sonsonate Este'', N''Cuisnahuat''),
    (189, 10, N''Sonsonate Este'', N''Santa Isabel Ishuatán''),
    (190, 10, N''Sonsonate Oeste'', N''Acajutla''),
    (191, 11, N''Santa Ana Norte'', N''Masahuat''),
    (192, 11, N''Santa Ana Norte'', N''Metapán''),
    (193, 11, N''Santa Ana Norte'', N''Santa Rosa Guachipilín''),
    (194, 11, N''Santa Ana Norte'', N''Texistepeque''),
    (195, 11, N''Santa Ana Centro'', N''Santa Ana''),
    (196, 11, N''Santa Ana Este'', N''Coatepeque''),
    (197, 11, N''Santa Ana Este'', N''El Congo''),
    (198, 11, N''Santa Ana Oeste'', N''Candelaria de la Frontera''),
    (199, 11, N''Santa Ana Oeste'', N''Chalchuapa''),
    (200, 11, N''Santa Ana Oeste'', N''El Porvenir''),
    (201, 11, N''Santa Ana Oeste'', N''San Antonio Pajonal''),
    (202, 11, N''Santa Ana Oeste'', N''San Sebastián Salitrillo''),
    (203, 11, N''Santa Ana Oeste'', N''Santiago de La Frontera''),
    (204, 12, N''San Vicente Norte'', N''Apastepeque''),
    (205, 12, N''San Vicente Norte'', N''Santa Clara''),
    (206, 12, N''San Vicente Norte'', N''San Ildefonso''),
    (207, 12, N''San Vicente Norte'', N''San Esteban Catarina''),
    (208, 12, N''San Vicente Norte'', N''San Sebastián''),
    (209, 12, N''San Vicente Norte'', N''San Lorenzo''),
    (210, 12, N''San Vicente Norte'', N''Santo Domingo'');
    INSERT INTO [Ciudades] ([Id], [DepartamentoId], [Municipio], [Nombre])
    VALUES (211, 12, N''San Vicente Sur'', N''San Vicente''),
    (212, 12, N''San Vicente Sur'', N''Guadalupe''),
    (213, 12, N''San Vicente Sur'', N''Verapaz''),
    (214, 12, N''San Vicente Sur'', N''Tepetitán''),
    (215, 12, N''San Vicente Sur'', N''Tecoluca''),
    (216, 12, N''San Vicente Sur'', N''San Cayetano Istepeque''),
    (217, 13, N''San Miguel Norte'', N''Ciudad Barrios''),
    (218, 13, N''San Miguel Norte'', N''Sesori''),
    (219, 13, N''San Miguel Norte'', N''Nuevo Edén de San Juan''),
    (220, 13, N''San Miguel Norte'', N''San Gerardo''),
    (221, 13, N''San Miguel Norte'', N''San Luis de La Reina''),
    (222, 13, N''San Miguel Norte'', N''Carolina''),
    (223, 13, N''San Miguel Norte'', N''San Antonio del Mosco''),
    (224, 13, N''San Miguel Norte'', N''Chapeltique''),
    (225, 13, N''San Miguel Centro'', N''San Miguel''),
    (226, 13, N''San Miguel Centro'', N''Comacarán''),
    (227, 13, N''San Miguel Centro'', N''Uluazapa''),
    (228, 13, N''San Miguel Centro'', N''Moncagua''),
    (229, 13, N''San Miguel Centro'', N''Quelepa''),
    (230, 13, N''San Miguel Centro'', N''Chirilagua''),
    (231, 13, N''San Miguel Oeste'', N''Chinameca''),
    (232, 13, N''San Miguel Oeste'', N''Nueva Guadalupe''),
    (233, 13, N''San Miguel Oeste'', N''Lolotique''),
    (234, 13, N''San Miguel Oeste'', N''San Jorge''),
    (235, 13, N''San Miguel Oeste'', N''San Rafael Oriente''),
    (236, 13, N''San Miguel Oeste'', N''El Tránsito''),
    (237, 14, N''Morazán Norte'', N''Arambala''),
    (238, 14, N''Morazán Norte'', N''Cacaopera''),
    (239, 14, N''Morazán Norte'', N''Corinto''),
    (240, 14, N''Morazán Norte'', N''El Rosario''),
    (241, 14, N''Morazán Norte'', N''Joateca''),
    (242, 14, N''Morazán Norte'', N''Jocoaitique''),
    (243, 14, N''Morazán Norte'', N''Meanguera''),
    (244, 14, N''Morazán Norte'', N''Perquín''),
    (245, 14, N''Morazán Norte'', N''San Fernando''),
    (246, 14, N''Morazán Norte'', N''San Isidro''),
    (247, 14, N''Morazán Norte'', N''Torola''),
    (248, 14, N''Morazán Sur'', N''Chilanga''),
    (249, 14, N''Morazán Sur'', N''Delicias de Concepción''),
    (250, 14, N''Morazán Sur'', N''El Divisadero''),
    (251, 14, N''Morazán Sur'', N''Gualococti''),
    (252, 14, N''Morazán Sur'', N''Guatajiagua'');
    INSERT INTO [Ciudades] ([Id], [DepartamentoId], [Municipio], [Nombre])
    VALUES (253, 14, N''Morazán Sur'', N''Jocoro''),
    (254, 14, N''Morazán Sur'', N''Lolotiquillo''),
    (255, 14, N''Morazán Sur'', N''Osicala''),
    (256, 14, N''Morazán Sur'', N''San Carlos''),
    (257, 14, N''Morazán Sur'', N''San Francisco Gotera''),
    (258, 14, N''Morazán Sur'', N''San Simón''),
    (259, 14, N''Morazán Sur'', N''Sensembra''),
    (260, 14, N''Morazán Sur'', N''Sociedad''),
    (261, 14, N''Morazán Sur'', N''Yamabal''),
    (262, 14, N''Morazán Sur'', N''Yoloaiquín'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DepartamentoId', N'Municipio', N'Nombre') AND [object_id] = OBJECT_ID(N'[Ciudades]'))
        SET IDENTITY_INSERT [Ciudades] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'PermisoId', N'RolId') AND [object_id] = OBJECT_ID(N'[RolesPermisos]'))
        SET IDENTITY_INSERT [RolesPermisos] ON;
    EXEC(N'INSERT INTO [RolesPermisos] ([PermisoId], [RolId])
    VALUES (1, 1),
    (2, 1),
    (3, 1),
    (4, 1),
    (5, 1),
    (6, 1),
    (7, 1),
    (8, 1),
    (9, 1),
    (10, 1),
    (11, 1),
    (12, 1),
    (13, 1),
    (14, 1),
    (15, 1),
    (16, 1),
    (17, 1),
    (18, 1),
    (19, 1),
    (20, 1),
    (21, 1),
    (1, 2),
    (2, 2),
    (3, 2),
    (4, 2),
    (6, 2),
    (10, 2),
    (11, 2),
    (12, 2),
    (13, 2),
    (21, 2),
    (21, 3),
    (22, 3),
    (23, 3),
    (24, 3),
    (25, 3)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'PermisoId', N'RolId') AND [object_id] = OBJECT_ID(N'[RolesPermisos]'))
        SET IDENTITY_INSERT [RolesPermisos] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE INDEX [IX_Actividades_Fecha] ON [Actividades] ([Fecha]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE INDEX [IX_Actividades_UsuarioId] ON [Actividades] ([UsuarioId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Ciudades_DepartamentoId_Nombre] ON [Ciudades] ([DepartamentoId], [Nombre]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE INDEX [IX_Clientes_Activo] ON [Clientes] ([Activo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE INDEX [IX_Clientes_Apellidos_Nombres] ON [Clientes] ([Apellidos], [Nombres]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE INDEX [IX_Clientes_CiudadId] ON [Clientes] ([CiudadId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Clientes_Correo] ON [Clientes] ([Correo]) WHERE [Correo] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Clientes_UsuarioId] ON [Clientes] ([UsuarioId]) WHERE [UsuarioId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Departamentos_Codigo] ON [Departamentos] ([Codigo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Departamentos_Nombre] ON [Departamentos] ([Nombre]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DetallesPedido_PedidoId_ProductoId] ON [DetallesPedido] ([PedidoId], [ProductoId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE INDEX [IX_DetallesPedido_ProductoId] ON [DetallesPedido] ([ProductoId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE INDEX [IX_Pedidos_ClienteId] ON [Pedidos] ([ClienteId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE INDEX [IX_Pedidos_Estado_FechaPedido] ON [Pedidos] ([Estado], [FechaPedido]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE INDEX [IX_Pedidos_FechaEntrega] ON [Pedidos] ([FechaEntrega]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE INDEX [IX_Pedidos_UsuarioId] ON [Pedidos] ([UsuarioId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Permisos_Codigo] ON [Permisos] ([Codigo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE INDEX [IX_Productos_Activo] ON [Productos] ([Activo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE INDEX [IX_Productos_Nombre] ON [Productos] ([Nombre]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE INDEX [IX_Productos_PrecioVenta] ON [Productos] ([PrecioVenta]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Roles_Nombre] ON [Roles] ([Nombre]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE INDEX [IX_RolesPermisos_PermisoId] ON [RolesPermisos] ([PermisoId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Usuarios_Correo] ON [Usuarios] ([Correo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Usuarios_NombreUsuario] ON [Usuarios] ([NombreUsuario]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    CREATE INDEX [IX_Usuarios_RolId] ON [Usuarios] ([RolId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225147_CrearEsquemaInicial'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261025225147_CrearEsquemaInicial', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225217_AgregarBusquedaDePedidosYPermisosDeBD'
)
BEGIN

    CREATE OR ALTER VIEW dbo.vw_PedidosResumen AS
    SELECT p.Id, p.ClienteId,
           c.Nombres + N' ' + c.Apellidos AS Cliente,
           c.Apellidos AS ClienteApellidos,
           p.UsuarioId, u.NombreCompleto AS Usuario,
           p.FechaPedido, p.FechaEntrega, p.Estado, p.Total, p.Activo, p.Comentarios,
           (SELECT COUNT(*) FROM dbo.DetallesPedido d WHERE d.PedidoId = p.Id) AS Lineas
    FROM dbo.Pedidos p
    JOIN dbo.Clientes c ON c.Id = p.ClienteId
    JOIN dbo.Usuarios u ON u.Id = p.UsuarioId;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225217_AgregarBusquedaDePedidosYPermisosDeBD'
)
BEGIN

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
    END;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225217_AgregarBusquedaDePedidosYPermisosDeBD'
)
BEGIN

    IF USER_ID(N'pedidos_app') IS NOT NULL
    BEGIN
        GRANT EXECUTE ON OBJECT::dbo.usp_Pedidos_Buscar TO pedidos_app;
        GRANT SELECT ON OBJECT::dbo.vw_PedidosResumen TO pedidos_app;
        -- El historial es inalterable: la aplicación solo inserta y consulta.
        DENY UPDATE, DELETE ON OBJECT::dbo.Actividades TO pedidos_app;
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025225217_AgregarBusquedaDePedidosYPermisosDeBD'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261025225217_AgregarBusquedaDePedidosYPermisosDeBD', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026043346_AgregarEliminadoEnYPermisosDeAdministracion'
)
BEGIN
    ALTER TABLE [Actividades] DROP CONSTRAINT [FK_Actividades_Usuarios_UsuarioId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026043346_AgregarEliminadoEnYPermisosDeAdministracion'
)
BEGIN
    ALTER TABLE [Clientes] DROP CONSTRAINT [FK_Clientes_Usuarios_UsuarioId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026043346_AgregarEliminadoEnYPermisosDeAdministracion'
)
BEGIN
    ALTER TABLE [Usuarios] ADD [EliminadoEn] datetimeoffset(0) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026043346_AgregarEliminadoEnYPermisosDeAdministracion'
)
BEGIN
    ALTER TABLE [Productos] ADD [EliminadoEn] datetimeoffset(0) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026043346_AgregarEliminadoEnYPermisosDeAdministracion'
)
BEGIN
    ALTER TABLE [Pedidos] ADD [EliminadoEn] datetimeoffset(0) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026043346_AgregarEliminadoEnYPermisosDeAdministracion'
)
BEGIN
    ALTER TABLE [Clientes] ADD [EliminadoEn] datetimeoffset(0) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026043346_AgregarEliminadoEnYPermisosDeAdministracion'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Codigo', N'Descripcion') AND [object_id] = OBJECT_ID(N'[Permisos]'))
        SET IDENTITY_INSERT [Permisos] ON;
    EXEC(N'INSERT INTO [Permisos] ([Id], [Codigo], [Descripcion])
    VALUES (26, N''roles.editar'', N''Editar los permisos de un rol''),
    (27, N''catalogos.ver'', N''Ver los catálogos de departamentos y ciudades''),
    (28, N''usuarios.eliminar'', N''Eliminar definitivamente usuarios dados de baja'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Codigo', N'Descripcion') AND [object_id] = OBJECT_ID(N'[Permisos]'))
        SET IDENTITY_INSERT [Permisos] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026043346_AgregarEliminadoEnYPermisosDeAdministracion'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'PermisoId', N'RolId') AND [object_id] = OBJECT_ID(N'[RolesPermisos]'))
        SET IDENTITY_INSERT [RolesPermisos] ON;
    EXEC(N'INSERT INTO [RolesPermisos] ([PermisoId], [RolId])
    VALUES (26, 1),
    (27, 1),
    (28, 1),
    (27, 2)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'PermisoId', N'RolId') AND [object_id] = OBJECT_ID(N'[RolesPermisos]'))
        SET IDENTITY_INSERT [RolesPermisos] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026043346_AgregarEliminadoEnYPermisosDeAdministracion'
)
BEGIN
    ALTER TABLE [Actividades] ADD CONSTRAINT [FK_Actividades_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE SET NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026043346_AgregarEliminadoEnYPermisosDeAdministracion'
)
BEGIN
    ALTER TABLE [Clientes] ADD CONSTRAINT [FK_Clientes_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE SET NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026043346_AgregarEliminadoEnYPermisosDeAdministracion'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261026043346_AgregarEliminadoEnYPermisosDeAdministracion', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026142622_AgregarCatalogoDeProductos'
)
BEGIN
    ALTER TABLE [Productos] ADD [SubcategoriaId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026142622_AgregarCatalogoDeProductos'
)
BEGIN
    CREATE TABLE [Categorias] (
        [Id] int NOT NULL IDENTITY,
        [Nombre] nvarchar(100) NOT NULL,
        [Descripcion] nvarchar(300) NULL,
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        [EliminadoEn] datetimeoffset(0) NULL,
        [CreadoEn] datetimeoffset(0) NOT NULL,
        [ActualizadoEn] datetimeoffset(0) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Categorias] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026142622_AgregarCatalogoDeProductos'
)
BEGIN
    CREATE TABLE [Subcategorias] (
        [Id] int NOT NULL IDENTITY,
        [CategoriaId] int NOT NULL,
        [Nombre] nvarchar(100) NOT NULL,
        [Descripcion] nvarchar(300) NULL,
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        [EliminadoEn] datetimeoffset(0) NULL,
        [CreadoEn] datetimeoffset(0) NOT NULL,
        [ActualizadoEn] datetimeoffset(0) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Subcategorias] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Subcategorias_Categorias_CategoriaId] FOREIGN KEY ([CategoriaId]) REFERENCES [Categorias] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026142622_AgregarCatalogoDeProductos'
)
BEGIN
    CREATE TABLE [Filtros] (
        [Id] int NOT NULL IDENTITY,
        [SubcategoriaId] int NOT NULL,
        [Nombre] nvarchar(100) NOT NULL,
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        [EliminadoEn] datetimeoffset(0) NULL,
        [CreadoEn] datetimeoffset(0) NOT NULL,
        [ActualizadoEn] datetimeoffset(0) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Filtros] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Filtros_Subcategorias_SubcategoriaId] FOREIGN KEY ([SubcategoriaId]) REFERENCES [Subcategorias] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026142622_AgregarCatalogoDeProductos'
)
BEGIN
    CREATE TABLE [OpcionesFiltro] (
        [Id] int NOT NULL IDENTITY,
        [FiltroId] int NOT NULL,
        [Valor] nvarchar(100) NOT NULL,
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        [EliminadoEn] datetimeoffset(0) NULL,
        [CreadoEn] datetimeoffset(0) NOT NULL,
        [ActualizadoEn] datetimeoffset(0) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_OpcionesFiltro] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_OpcionesFiltro_Id_FiltroId] UNIQUE ([Id], [FiltroId]),
        CONSTRAINT [FK_OpcionesFiltro_Filtros_FiltroId] FOREIGN KEY ([FiltroId]) REFERENCES [Filtros] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026142622_AgregarCatalogoDeProductos'
)
BEGIN
    CREATE TABLE [ProductosOpciones] (
        [ProductoId] int NOT NULL,
        [FiltroId] int NOT NULL,
        [OpcionFiltroId] int NOT NULL,
        CONSTRAINT [PK_ProductosOpciones] PRIMARY KEY ([ProductoId], [FiltroId]),
        CONSTRAINT [FK_ProductosOpciones_Filtros_FiltroId] FOREIGN KEY ([FiltroId]) REFERENCES [Filtros] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProductosOpciones_OpcionesFiltro_OpcionFiltroId_FiltroId] FOREIGN KEY ([OpcionFiltroId], [FiltroId]) REFERENCES [OpcionesFiltro] ([Id], [FiltroId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProductosOpciones_Productos_ProductoId] FOREIGN KEY ([ProductoId]) REFERENCES [Productos] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026142622_AgregarCatalogoDeProductos'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Codigo', N'Descripcion') AND [object_id] = OBJECT_ID(N'[Permisos]'))
        SET IDENTITY_INSERT [Permisos] ON;
    EXEC(N'INSERT INTO [Permisos] ([Id], [Codigo], [Descripcion])
    VALUES (29, N''categorias.ver'', N''Ver categorías, subcategorías, filtros y opciones''),
    (30, N''categorias.crear'', N''Crear categorías, subcategorías, filtros y opciones''),
    (31, N''categorias.editar'', N''Editar categorías, subcategorías, filtros y opciones''),
    (32, N''categorias.desactivar'', N''Dar de baja o reactivar categorías, subcategorías, filtros y opciones''),
    (33, N''categorias.eliminar'', N''Eliminar definitivamente elementos inactivos y sin uso de la clasificación'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Codigo', N'Descripcion') AND [object_id] = OBJECT_ID(N'[Permisos]'))
        SET IDENTITY_INSERT [Permisos] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026142622_AgregarCatalogoDeProductos'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'PermisoId', N'RolId') AND [object_id] = OBJECT_ID(N'[RolesPermisos]'))
        SET IDENTITY_INSERT [RolesPermisos] ON;
    EXEC(N'INSERT INTO [RolesPermisos] ([PermisoId], [RolId])
    VALUES (29, 1),
    (30, 1),
    (31, 1),
    (32, 1),
    (33, 1),
    (29, 2)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'PermisoId', N'RolId') AND [object_id] = OBJECT_ID(N'[RolesPermisos]'))
        SET IDENTITY_INSERT [RolesPermisos] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026142622_AgregarCatalogoDeProductos'
)
BEGIN
    CREATE INDEX [IX_Productos_SubcategoriaId] ON [Productos] ([SubcategoriaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026142622_AgregarCatalogoDeProductos'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Categorias_Nombre] ON [Categorias] ([Nombre]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026142622_AgregarCatalogoDeProductos'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Filtros_SubcategoriaId_Nombre] ON [Filtros] ([SubcategoriaId], [Nombre]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026142622_AgregarCatalogoDeProductos'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OpcionesFiltro_FiltroId_Valor] ON [OpcionesFiltro] ([FiltroId], [Valor]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026142622_AgregarCatalogoDeProductos'
)
BEGIN
    CREATE INDEX [IX_ProductosOpciones_FiltroId] ON [ProductosOpciones] ([FiltroId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026142622_AgregarCatalogoDeProductos'
)
BEGIN
    CREATE INDEX [IX_ProductosOpciones_OpcionFiltroId] ON [ProductosOpciones] ([OpcionFiltroId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026142622_AgregarCatalogoDeProductos'
)
BEGIN
    CREATE INDEX [IX_ProductosOpciones_OpcionFiltroId_FiltroId] ON [ProductosOpciones] ([OpcionFiltroId], [FiltroId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026142622_AgregarCatalogoDeProductos'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Subcategorias_CategoriaId_Nombre] ON [Subcategorias] ([CategoriaId], [Nombre]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026142622_AgregarCatalogoDeProductos'
)
BEGIN
    ALTER TABLE [Productos] ADD CONSTRAINT [FK_Productos_Subcategorias_SubcategoriaId] FOREIGN KEY ([SubcategoriaId]) REFERENCES [Subcategorias] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026142622_AgregarCatalogoDeProductos'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261026142622_AgregarCatalogoDeProductos', N'8.0.11');
END;
GO

COMMIT;
GO

