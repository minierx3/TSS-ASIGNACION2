-- ============================================================
--  DatabaseSetup.sql
--  Servidor destino : (localdb)\MSSQLLocalDB
--  Descripción      : Crea BD_Ferreteria y BD_TSS con sus
--                     tablas y datos de prueba iniciales.
--  Ejecución        : sqlcmd -S "(localdb)\MSSQLLocalDB" -i DatabaseSetup.sql
--                     o directamente desde SQL Server Management Studio.
-- ============================================================

-- ============================================================
-- 1. BASE DE DATOS: BD_Ferreteria
-- ============================================================
USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'BD_Ferreteria')
BEGIN
    CREATE DATABASE BD_Ferreteria;
    PRINT 'Base de datos BD_Ferreteria creada exitosamente.';
END
ELSE
BEGIN
    PRINT 'BD_Ferreteria ya existe. Se omite la creación.';
END
GO

USE BD_Ferreteria;
GO

-- --------------------------------------------------------
-- Tabla: Empleados
-- --------------------------------------------------------
IF NOT EXISTS (
    SELECT * FROM sys.tables WHERE name = N'Empleados'
)
BEGIN
    CREATE TABLE Empleados (
        Cedula      VARCHAR(11)     NOT NULL PRIMARY KEY,
        Nombres     VARCHAR(50)     NOT NULL,
        Apellidos   VARCHAR(50)     NOT NULL,
        SalarioBase DECIMAL(18, 2)  NOT NULL
    );
    PRINT 'Tabla Empleados creada en BD_Ferreteria.';
END
ELSE
BEGIN
    PRINT 'Tabla Empleados ya existe en BD_Ferreteria. Se omite la creación.';
END
GO

-- --------------------------------------------------------
-- Datos de prueba: 5 empleados realistas (República Dominicana)
-- --------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM Empleados)
BEGIN
    INSERT INTO Empleados (Cedula, Nombres, Apellidos, SalarioBase) VALUES
        ('00112345678', 'Carlos Alberto',  'Martínez Pérez',  28500.00),
        ('00298765432', 'María José',      'Reyes Sánchez',   35000.00),
        ('00187654321', 'Juan Francisco',  'López Guzmán',    22750.00),
        ('00345678901', 'Ana Patricia',    'Hernández Díaz',  41000.00),
        ('00423456789', 'Pedro Ramón',     'García Familia',  19800.00);
    PRINT '5 registros de prueba insertados en Empleados (BD_Ferreteria).';
END
ELSE
BEGIN
    PRINT 'La tabla Empleados ya contiene datos. No se insertaron duplicados.';
END
GO


-- ============================================================
-- 2. BASE DE DATOS: BD_TSS
-- ============================================================
USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'BD_TSS')
BEGIN
    CREATE DATABASE BD_TSS;
    PRINT 'Base de datos BD_TSS creada exitosamente.';
END
ELSE
BEGIN
    PRINT 'BD_TSS ya existe. Se omite la creación.';
END
GO

USE BD_TSS;
GO

-- --------------------------------------------------------
-- Tabla: Recepcion_Autodeterminacion
-- Extiende la estructura de Empleados con TipoNovedad
-- --------------------------------------------------------
IF NOT EXISTS (
    SELECT * FROM sys.tables WHERE name = N'Recepcion_Autodeterminacion'
)
BEGIN
    CREATE TABLE Recepcion_Autodeterminacion (
        Id           INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        Cedula       VARCHAR(11)     NOT NULL,
        Nombres      VARCHAR(50)     NOT NULL,
        Apellidos    VARCHAR(50)     NOT NULL,
        SalarioBase  DECIMAL(18, 2)  NOT NULL,
        TipoNovedad  VARCHAR(20)     NOT NULL  -- Ej: 'ALTA', 'BAJA', 'MODIFICACION'
    );
    PRINT 'Tabla Recepcion_Autodeterminacion creada en BD_TSS.';
END
ELSE
BEGIN
    PRINT 'Tabla Recepcion_Autodeterminacion ya existe en BD_TSS. Se omite.';
END
GO

-- --------------------------------------------------------
-- Tabla: Facturacion_SDSS
-- Resumen de facturación mensual al SDSS
-- --------------------------------------------------------
IF NOT EXISTS (
    SELECT * FROM sys.tables WHERE name = N'Facturacion_SDSS'
)
BEGIN
    CREATE TABLE Facturacion_SDSS (
        Id              INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        Fecha           DATETIME        NOT NULL DEFAULT GETDATE(),
        TotalSalarios   DECIMAL(18, 2)  NOT NULL,
        CobroSFS        DECIMAL(18, 2)  NOT NULL,   -- Seguro Familiar de Salud
        CobroPensiones  DECIMAL(18, 2)  NOT NULL,   -- Fondo de Pensiones
        CobroRiesgos    DECIMAL(18, 2)  NOT NULL,   -- Seguro de Riesgos Laborales
        TotalPagar      DECIMAL(18, 2)  NOT NULL    -- SFS + Pensiones + Riesgos
    );
    PRINT 'Tabla Facturacion_SDSS creada en BD_TSS.';
END
ELSE
BEGIN
    PRINT 'Tabla Facturacion_SDSS ya existe en BD_TSS. Se omite.';
END
GO

PRINT '============================================================';
PRINT 'Script DatabaseSetup.sql finalizado correctamente.';
PRINT 'Bases de datos disponibles: BD_Ferreteria, BD_TSS';
PRINT '============================================================';
GO


SELECT * FROM BD_TSS.dbo.Recepcion_Autodeterminacion;
SELECT * FROM BD_TSS.dbo.Facturacion_SDSS;
SELECT * FROM BD_Ferreteria.dbo.Empleados;
