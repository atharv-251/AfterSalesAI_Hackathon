-- AI Core configuration and chat persistence only. No business-data copies or cross-database reads.
-- SQL Server 2019+. Execute entire file in SSMS. Existing data is preserved.
USE master;
GO
IF DB_ID(N'MultiTenantAICoreDb') IS NULL EXEC(N'CREATE DATABASE MultiTenantAICoreDb');
GO
USE MultiTenantAICoreDb;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME()<>N'MultiTenantAICoreDb' THROW 51300,'Wrong database selected.',1;
BEGIN TRY
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.CoreTenants',N'U') IS NULL
CREATE TABLE dbo.CoreTenants (
    TenantId uniqueidentifier NOT NULL PRIMARY KEY,
    TenantCode nvarchar(30) NOT NULL UNIQUE,
    TenantName nvarchar(200) NOT NULL,
    ProductName nvarchar(200) NOT NULL,
    Description nvarchar(2000) NOT NULL,
    TenantSystemPrompt nvarchar(max) NOT NULL,
    DatabaseConfigurationId uniqueidentifier NOT NULL UNIQUE,
    IsWrapperApiEnabled bit NOT NULL,
    IsActive bit NOT NULL DEFAULT 1,
    CreatedDate datetimeoffset NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    UpdatedDate datetimeoffset NOT NULL DEFAULT SYSDATETIMEOFFSET()
);
IF OBJECT_ID(N'dbo.CoreDatabaseConfigurations',N'U') IS NULL
CREATE TABLE dbo.CoreDatabaseConfigurations (
    DatabaseConfigurationId uniqueidentifier NOT NULL PRIMARY KEY,
    TenantId uniqueidentifier NOT NULL UNIQUE REFERENCES dbo.CoreTenants(TenantId),
    ConfigurationReference nvarchar(200) NOT NULL,
    DatabaseName nvarchar(128) NOT NULL,
    IsActive bit NOT NULL DEFAULT 1,
    UNIQUE(TenantId,DatabaseConfigurationId)
);
IF OBJECT_ID(N'dbo.CoreOperations',N'U') IS NULL
CREATE TABLE dbo.CoreOperations (
    OperationId uniqueidentifier NOT NULL PRIMARY KEY,
    TenantId uniqueidentifier NOT NULL REFERENCES dbo.CoreTenants(TenantId),
    OperationName nvarchar(100) NOT NULL,
    Description nvarchar(2000) NOT NULL,
    Kind nvarchar(20) NOT NULL CHECK(Kind IN (N'Api',N'Wrapper',N'Database',N'Document')),
    BaseUrl nvarchar(500) NOT NULL,
    RelativeUrl nvarchar(500) NOT NULL,
    HttpMethod nvarchar(10) NOT NULL,
    Headers nvarchar(max) NOT NULL DEFAULT N'{}',
    RequestParameters nvarchar(max) NOT NULL DEFAULT N'{}',
    RequestBodyTemplate nvarchar(max) NOT NULL DEFAULT N'{}',
    ResponseMapping nvarchar(max) NOT NULL DEFAULT N'{}',
    TimeoutSeconds int NOT NULL DEFAULT 10 CHECK(TimeoutSeconds BETWEEN 1 AND 60),
    IsWrapperApi bit NOT NULL DEFAULT 0,
    StoredProcedureName nvarchar(200) NOT NULL,
    AllowedParameters nvarchar(max) NOT NULL,
    IsActive bit NOT NULL DEFAULT 1,
    UNIQUE(TenantId,OperationName),
    CHECK(ISJSON(Headers)=1 AND ISJSON(RequestParameters)=1 AND ISJSON(RequestBodyTemplate)=1 AND ISJSON(ResponseMapping)=1 AND ISJSON(AllowedParameters)=1)
);
IF OBJECT_ID(N'dbo.CoreDocuments',N'U') IS NULL
CREATE TABLE dbo.CoreDocuments (
    DocumentId uniqueidentifier NOT NULL PRIMARY KEY,
    TenantId uniqueidentifier NOT NULL REFERENCES dbo.CoreTenants(TenantId),
    FileName nvarchar(500) NOT NULL,
    FileType nvarchar(20) NOT NULL,
    FilePath nvarchar(1000) NOT NULL,
    ExtractedText nvarchar(max) NOT NULL,
    IsActive bit NOT NULL DEFAULT 1,
    UploadedDate datetimeoffset NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    UNIQUE(TenantId,DocumentId)
);
IF OBJECT_ID(N'dbo.CoreDocumentChunks',N'U') IS NULL
CREATE TABLE dbo.CoreDocumentChunks (
    ChunkId uniqueidentifier NOT NULL PRIMARY KEY,
    TenantId uniqueidentifier NOT NULL,
    DocumentId uniqueidentifier NOT NULL,
    ChunkSequence int NOT NULL CHECK(ChunkSequence>=0),
    ChunkContent nvarchar(max) NOT NULL,
    CreatedDate datetimeoffset NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    UNIQUE(TenantId,DocumentId,ChunkSequence),
    FOREIGN KEY(TenantId,DocumentId) REFERENCES dbo.CoreDocuments(TenantId,DocumentId)
);
IF OBJECT_ID(N'dbo.CoreChatSessions',N'U') IS NULL
CREATE TABLE dbo.CoreChatSessions (
    SessionId uniqueidentifier NOT NULL PRIMARY KEY,
    TenantId uniqueidentifier NOT NULL REFERENCES dbo.CoreTenants(TenantId),
    CreatedDate datetimeoffset NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    UNIQUE(TenantId,SessionId)
);
IF OBJECT_ID(N'dbo.CoreChatMessages',N'U') IS NULL
CREATE TABLE dbo.CoreChatMessages (
    MessageId uniqueidentifier NOT NULL PRIMARY KEY,
    TenantId uniqueidentifier NOT NULL,
    SessionId uniqueidentifier NOT NULL,
    Role nvarchar(20) NOT NULL CHECK(Role IN (N'user',N'assistant')),
    Content nvarchar(max) NOT NULL,
    CreatedDate datetimeoffset NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    FOREIGN KEY(TenantId,SessionId) REFERENCES dbo.CoreChatSessions(TenantId,SessionId)
);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.CoreChatMessages') AND name=N'IX_CoreChatMessages_Scope')
CREATE INDEX IX_CoreChatMessages_Scope ON dbo.CoreChatMessages(TenantId,SessionId,CreatedDate);

INSERT dbo.CoreTenants(TenantId,TenantCode,TenantName,ProductName,Description,TenantSystemPrompt,DatabaseConfigurationId,IsWrapperApiEnabled)
SELECT s.Id,s.Code,s.Name,s.Product,s.Description,s.Prompt,s.DatabaseId,s.Wrapper
FROM (VALUES
(CONVERT(uniqueidentifier,'00000000-0000-0000-0000-000000000001'),N'TENANT1',N'Parts and orders',N'After-sales parts',N'Existing parts ordering application',N'Answer from Tenant 1 sources. Combined service information is allowed only from an approved Tenant 1 wrapper response. Correlate by DealerId only, never assert order-to-repair matches.',CONVERT(uniqueidentifier,'40000000-0000-0000-0000-000000000001'),1),
(CONVERT(uniqueidentifier,'00000000-0000-0000-0000-000000000002'),N'TENANT2',N'Vehicle service and warranty',N'After-sales workshop',N'Vehicle repairs and warranty decisions',N'Answer only from Tenant 2 service, repair and warranty sources. Never use Tenant 1 data or wrapper operations.',CONVERT(uniqueidentifier,'40000000-0000-0000-0000-000000000002'),0)
) s(Id,Code,Name,Product,Description,Prompt,DatabaseId,Wrapper)
WHERE NOT EXISTS(SELECT 1 FROM dbo.CoreTenants t WHERE t.TenantId=s.Id);
INSERT dbo.CoreDatabaseConfigurations(DatabaseConfigurationId,TenantId,ConfigurationReference,DatabaseName)
SELECT DatabaseConfigurationId,TenantId,CASE TenantCode WHEN N'TENANT1' THEN N'ConnectionStrings:AfterSalesAI' ELSE N'ConnectionStrings:Tenant2' END,
CASE TenantCode WHEN N'TENANT1' THEN N'AfterSalesAI_Demo' ELSE N'Tenant2DemoDb' END
FROM dbo.CoreTenants t WHERE NOT EXISTS(SELECT 1 FROM dbo.CoreDatabaseConfigurations c WHERE c.DatabaseConfigurationId=t.DatabaseConfigurationId);
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_CoreTenant_DatabaseReference')
ALTER TABLE dbo.CoreTenants ADD CONSTRAINT FK_CoreTenant_DatabaseReference FOREIGN KEY(TenantId,DatabaseConfigurationId)
REFERENCES dbo.CoreDatabaseConfigurations(TenantId,DatabaseConfigurationId);

INSERT dbo.CoreOperations(OperationId,TenantId,OperationName,Description,Kind,BaseUrl,RelativeUrl,HttpMethod,IsWrapperApi,StoredProcedureName,AllowedParameters)
SELECT NEWID(),t.TenantId,s.Name,s.Description,s.Kind,N'http://127.0.0.1:5088/',s.Route,N'GET',s.Wrapper,s.ProcedureName,N'{"dealerId":"uppercase business key"}'
FROM (VALUES
(N'TENANT1',N'local',N'Existing approved Tenant 1 operational queries',N'Database',N'',0,N''),
(N'TENANT1',N'wrapper-service-overview',N'Dealer parts orders and vehicle repairs',N'Wrapper',N'api/tenant1/wrapper/dealers/{dealerId}/aftersales-overview',1,N''),
(N'TENANT1',N'wrapper-repair-status',N'Deliveries and repair readiness',N'Wrapper',N'api/tenant1/wrapper/dealers/{dealerId}/repair-readiness',1,N''),
(N'TENANT1',N'wrapper-warranty-summary',N'Parts claims alongside warranty decisions',N'Wrapper',N'api/tenant1/wrapper/dealers/{dealerId}/claims-warranty-summary',1,N''),
(N'TENANT1',N'documents',N'Tenant 1 document retrieval',N'Document',N'',0,N''),
(N'TENANT2',N'service-overview',N'Vehicle service overview',N'Api',N'api/tenant2/dealers/{dealerId}/service-overview',0,N'aftersales.usp_GetDealerServiceOverview'),
(N'TENANT2',N'repair-status',N'Repair status and events',N'Api',N'api/tenant2/dealers/{dealerId}/repair-status',0,N'aftersales.usp_GetDealerRepairStatus'),
(N'TENANT2',N'warranty-summary',N'Warranty cases and summary',N'Api',N'api/tenant2/dealers/{dealerId}/warranty-summary',0,N'aftersales.usp_GetDealerWarrantySummary'),
(N'TENANT2',N'db-service-overview',N'Approved parameterized service projection',N'Database',N'',0,N'aftersales.usp_GetDealerServiceOverview'),
(N'TENANT2',N'db-repair-status',N'Approved parameterized repair projection',N'Database',N'',0,N'aftersales.usp_GetDealerRepairStatus'),
(N'TENANT2',N'db-warranty-summary',N'Approved parameterized warranty projection',N'Database',N'',0,N'aftersales.usp_GetDealerWarrantySummary'),
(N'TENANT2',N'documents',N'Tenant 2 document retrieval',N'Document',N'',0,N'')
) s(Code,Name,Description,Kind,Route,Wrapper,ProcedureName)
JOIN dbo.CoreTenants t ON t.TenantCode=s.Code
WHERE NOT EXISTS(SELECT 1 FROM dbo.CoreOperations o WHERE o.TenantId=t.TenantId AND o.OperationName=s.Name);

INSERT dbo.CoreDocuments(DocumentId,TenantId,FileName,FileType,FilePath,ExtractedText)
SELECT CONVERT(uniqueidentifier,s.Id),t.TenantId,s.Name,N'.md',s.Name,s.Content FROM (VALUES
(N'TENANT1',N'50000000-0000-0000-0000-000000000001',N'parts-integration.md',N'Parts orders and shipments belong to Tenant 1. Compare workshop service data only through the approved dealer wrapper. DealerId is not proof that an order caused a repair delay. Keep parts claims and vehicle warranty amounts separate.'),
(N'TENANT2',N'50000000-0000-0000-0000-000000000002',N'workshop-warranty.md',N'Repair workflow: Booked, Diagnosing, AwaitingParts, InRepair, Completed or Cancelled. Warranty decisions: Submitted, UnderReview, Approved, PartiallyApproved or Rejected. A pending warranty decision is not approval. An overdue repair has passed its promised date and is neither Completed nor Cancelled. Tenant 2 cannot use Tenant 1 tools.')
) s(Code,Id,Name,Content) JOIN dbo.CoreTenants t ON t.TenantCode=s.Code
WHERE NOT EXISTS(SELECT 1 FROM dbo.CoreDocuments d WHERE d.DocumentId=CONVERT(uniqueidentifier,s.Id));
INSERT dbo.CoreDocumentChunks(ChunkId,TenantId,DocumentId,ChunkSequence,ChunkContent)
SELECT NEWID(),TenantId,DocumentId,0,ExtractedText FROM dbo.CoreDocuments d
WHERE d.DocumentId IN ('50000000-0000-0000-0000-000000000001','50000000-0000-0000-0000-000000000002')
AND NOT EXISTS(SELECT 1 FROM dbo.CoreDocumentChunks c WHERE c.TenantId=d.TenantId AND c.DocumentId=d.DocumentId);
COMMIT TRANSACTION;
END TRY
BEGIN CATCH
IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
THROW;
END CATCH;
SELECT TenantId,TenantCode,TenantName,IsWrapperApiEnabled FROM dbo.CoreTenants;
SELECT TenantId,OperationName,Kind,IsActive FROM dbo.CoreOperations ORDER BY TenantId,OperationName;
