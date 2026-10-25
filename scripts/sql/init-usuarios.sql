-- Inicialización de la base y de los usuarios con mínimo privilegio ([BaseDatos.md §7]).
-- Se ejecuta con sqlcmd como 'sa' ANTES de las migraciones; las contraseñas llegan como variables
-- sqlcmd ($(APP_PASSWORD), $(MIGRATOR_PASSWORD)) desde el entorno, nunca escritas aquí. Es idempotente.
SET NOCOUNT ON;
IF DB_ID(N'GestionPedidos') IS NULL CREATE DATABASE GestionPedidos;
GO
ALTER DATABASE GestionPedidos SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO
IF SUSER_ID(N'pedidos_migrator') IS NULL
    CREATE LOGIN pedidos_migrator WITH PASSWORD = N'$(MIGRATOR_PASSWORD)';
ELSE
    ALTER LOGIN pedidos_migrator WITH PASSWORD = N'$(MIGRATOR_PASSWORD)';
IF SUSER_ID(N'pedidos_app') IS NULL
    CREATE LOGIN pedidos_app WITH PASSWORD = N'$(APP_PASSWORD)';
ELSE
    ALTER LOGIN pedidos_app WITH PASSWORD = N'$(APP_PASSWORD)';
GO
USE GestionPedidos;
GO
IF USER_ID(N'pedidos_migrator') IS NULL CREATE USER pedidos_migrator FOR LOGIN pedidos_migrator;
IF USER_ID(N'pedidos_app') IS NULL CREATE USER pedidos_app FOR LOGIN pedidos_app;
-- El migrador es dueño del esquema (DDL); la aplicación solo lee y escribe datos.
ALTER ROLE db_owner ADD MEMBER pedidos_migrator;
ALTER ROLE db_datareader ADD MEMBER pedidos_app;
ALTER ROLE db_datawriter ADD MEMBER pedidos_app;
GO
