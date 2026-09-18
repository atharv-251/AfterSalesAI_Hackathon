-- Adds local demo-user storage to the existing AI Core database.
-- Execute after 03_CreateMultiTenantAICoreDb.sql. User rows are seeded by the API at startup,
-- so plaintext demo passwords never appear in this script or the database.
USE MultiTenantAICoreDb;
GO
SET XACT_ABORT ON;
IF DB_NAME() <> N'MultiTenantAICoreDb' THROW 51400, 'Wrong database selected.', 1;
IF OBJECT_ID(N'dbo.CoreDemoUsers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CoreDemoUsers
    (
        UserId uniqueidentifier NOT NULL CONSTRAINT PK_CoreDemoUsers PRIMARY KEY,
        TenantId uniqueidentifier NOT NULL,
        UserName nvarchar(100) NOT NULL CONSTRAINT UQ_CoreDemoUsers_UserName UNIQUE,
        DisplayName nvarchar(200) NOT NULL,
        PasswordHash nvarchar(100) NOT NULL,
        PasswordSalt nvarchar(100) NOT NULL,
        PasswordIterations int NOT NULL CONSTRAINT CK_CoreDemoUsers_Iterations CHECK (PasswordIterations >= 100000),
        IsActive bit NOT NULL CONSTRAINT DF_CoreDemoUsers_Active DEFAULT (1),
        CreatedDate datetimeoffset NOT NULL CONSTRAINT DF_CoreDemoUsers_Created DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT FK_CoreDemoUsers_Tenant FOREIGN KEY (TenantId) REFERENCES dbo.CoreTenants(TenantId)
    );
END;
GO
