-- Read-only preflight for SQL Server 2019 and later.
-- Connect to the intended server in SSMS and select the database to inspect.
-- This script does not create, alter, migrate, or delete any database or data.
SET NOCOUNT ON;

SELECT
    CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')) AS ServerName,
    CONVERT(nvarchar(128), SERVERPROPERTY('ProductVersion')) AS ProductVersion,
    CONVERT(nvarchar(128), SERVERPROPERTY('Edition')) AS Edition,
    DB_NAME() AS SelectedDatabase,
    HAS_PERMS_BY_NAME(NULL, NULL, 'CREATE ANY DATABASE') AS CanCreateDatabase;

SELECT name AS DatabaseName, compatibility_level AS CompatibilityLevel,
    state_desc AS DatabaseState,
    HAS_DBACCESS(name) AS HasDatabaseAccess
FROM sys.databases
WHERE name IN (N'AfterSalesAI', N'AfterSalesAI_Demo', N'MultiTenantAICoreDb', N'Tenant2DemoDb')
ORDER BY name;

SELECT s.name AS SchemaName, o.name AS ObjectName, o.type_desc AS ObjectType
FROM sys.objects AS o
JOIN sys.schemas AS s ON s.schema_id = o.schema_id
WHERE o.is_ms_shipped = 0 AND o.type IN ('U', 'V', 'P')
ORDER BY s.name, o.type, o.name;

SELECT TABLE_SCHEMA AS SchemaName, TABLE_NAME AS TableName,
    COLUMN_NAME AS ColumnName, DATA_TYPE AS DataType,
    CHARACTER_MAXIMUM_LENGTH AS MaximumLength,
    NUMERIC_PRECISION AS NumericPrecision, NUMERIC_SCALE AS NumericScale,
    IS_NULLABLE AS IsNullable
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = N'demo'
ORDER BY TABLE_NAME, ORDINAL_POSITION;

SELECT s.name AS SchemaName, t.name AS TableName, i.name AS IndexName,
    i.is_primary_key AS IsPrimaryKey, i.is_unique AS IsUnique,
    c.name AS ColumnName, ic.key_ordinal AS KeyOrdinal
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.indexes AS i ON i.object_id = t.object_id
JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE s.name = N'demo' AND i.index_id > 0
ORDER BY t.name, i.name, ic.key_ordinal;

SELECT fk.name AS ForeignKeyName,
    OBJECT_SCHEMA_NAME(fk.parent_object_id) AS SchemaName,
    OBJECT_NAME(fk.parent_object_id) AS TableName,
    OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable
FROM sys.foreign_keys AS fk
WHERE OBJECT_SCHEMA_NAME(fk.parent_object_id) = N'demo'
ORDER BY TableName, ForeignKeyName;

IF OBJECT_ID(N'demo.Dealers', N'U') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'SELECT COUNT_BIG(*) AS DealerCount FROM demo.Dealers;
        SELECT TOP (5) DealerId AS ExistingIntegrationKey FROM demo.Dealers ORDER BY DealerId;';
END;
ELSE
BEGIN
    SELECT N'The selected database does not contain demo.Dealers. The inspected application uses AfterSalesAI_Demo on (localdb)\MSSQLLocalDB. Do not create replacement Tenant 1 tables in another instance.' AS InspectionNote;
END;
