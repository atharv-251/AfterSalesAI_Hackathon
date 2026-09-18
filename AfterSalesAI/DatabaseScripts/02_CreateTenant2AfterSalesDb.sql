-- COMPLETE Tenant 2 AFTER-SALES setup. Run this file alone, in full, in SSMS.
-- SQL Server 2019+; no SQLCMD mode, substitutions, external files or Tenant 1 access.
-- Database: Tenant2DemoDb. Domain: vehicle repairs, service work and warranty decisions.
-- Shared DealerId values were verified in Tenant 1; correlation is ONLY through HTTP APIs.
-- No Tenant 1 changes, foreign keys to Tenant 1, cross-database queries or linked servers.
-- Supersedes the training-domain design in 01_CreateTenant2DemoDb.sql.
-- Existing training objects and any existing business rows are preserved, never dropped.
-- First run requires CREATE DATABASE permission; subsequent runs require DDL/DML rights.
-- Recommended available server: (localdb)\MSSQLLocalDB. localhost requires DBA permission.
USE [master];
GO
SET NOCOUNT ON;
IF DB_ID(N'Tenant2DemoDb') IS NULL
BEGIN
    IF ISNULL(HAS_PERMS_BY_NAME(NULL, NULL, 'CREATE ANY DATABASE'), 0) <> 1
        THROW 51100, 'Cannot create Tenant2DemoDb. Execute with database-creation permission or use your permitted LocalDB instance.', 1;
    EXEC(N'CREATE DATABASE [Tenant2DemoDb];');
END;
GO
USE [Tenant2DemoDb];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
IF DB_NAME() <> N'Tenant2DemoDb'
    THROW 51101, 'Wrong database selected. No setup is allowed outside Tenant2DemoDb.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    -- Serialize this installer without changing the connection isolation level.
    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock @Resource=N'AfterSalesDemoSetup', @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=15000;
    IF @LockResult < 0 THROW 51102, 'Another setup is running. Retry later.', 1;
    IF SCHEMA_ID(N'aftersales') IS NULL EXEC(N'CREATE SCHEMA aftersales AUTHORIZATION dbo;');

    IF OBJECT_ID(N'aftersales.DealerAccounts', N'U') IS NULL
    CREATE TABLE aftersales.DealerAccounts
    (
        DealerId nvarchar(50) NOT NULL CONSTRAINT PK_AS_DealerAccounts PRIMARY KEY,
        DealerName nvarchar(200) NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_AS_DealerActive DEFAULT (1),
        CreatedUtc datetime2(0) NOT NULL CONSTRAINT DF_AS_DealerCreated DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_AS_DealerId CHECK (DATALENGTH(DealerId) BETWEEN 2 AND 100
            AND DealerId COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9-]%'),
        CONSTRAINT CK_AS_DealerName CHECK (LEN(LTRIM(RTRIM(DealerName))) > 0)
    );

    IF OBJECT_ID(N'aftersales.Vehicles', N'U') IS NULL
    CREATE TABLE aftersales.Vehicles
    (
        VehicleId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_AS_Vehicles PRIMARY KEY,
        VehicleReference nvarchar(30) NOT NULL CONSTRAINT UQ_AS_VehicleReference UNIQUE,
        DealerId nvarchar(50) NOT NULL,
        ModelName nvarchar(100) NOT NULL,
        ModelYear smallint NOT NULL,
        WarrantyStart date NOT NULL,
        WarrantyEnd date NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_AS_VehicleActive DEFAULT (1),
        CONSTRAINT FK_AS_VehicleDealer FOREIGN KEY (DealerId) REFERENCES aftersales.DealerAccounts(DealerId),
        CONSTRAINT UQ_AS_VehicleDealer UNIQUE (VehicleId, DealerId),
        CONSTRAINT CK_AS_ModelYear CHECK (ModelYear BETWEEN 1990 AND 2100),
        CONSTRAINT CK_AS_VehicleWarranty CHECK (WarrantyEnd >= WarrantyStart)
    );

    IF OBJECT_ID(N'aftersales.ServiceOperations', N'U') IS NULL
    CREATE TABLE aftersales.ServiceOperations
    (
        OperationCode nvarchar(30) NOT NULL CONSTRAINT PK_AS_ServiceOperations PRIMARY KEY,
        Description nvarchar(200) NOT NULL,
        Category nvarchar(30) NOT NULL,
        StandardHours decimal(5,2) NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_AS_OperationActive DEFAULT (1),
        CONSTRAINT CK_AS_OperationCategory CHECK (Category IN (N'Diagnostics', N'Maintenance', N'Repair', N'Safety')),
        CONSTRAINT CK_AS_OperationHours CHECK (StandardHours > 0 AND StandardHours <= 100)
    );

    IF OBJECT_ID(N'aftersales.RepairOrders', N'U') IS NULL
    CREATE TABLE aftersales.RepairOrders
    (
        RepairOrderId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_AS_RepairOrders PRIMARY KEY,
        RepairOrderNumber nvarchar(30) NOT NULL CONSTRAINT UQ_AS_RepairNumber UNIQUE,
        DealerId nvarchar(50) NOT NULL,
        VehicleId int NOT NULL,
        Complaint nvarchar(500) NOT NULL,
        Status nvarchar(30) NOT NULL CONSTRAINT DF_AS_RepairStatus DEFAULT N'Booked',
        Priority nvarchar(10) NOT NULL CONSTRAINT DF_AS_RepairPriority DEFAULT N'Normal',
        OpenedDate date NOT NULL,
        PromisedDate date NOT NULL,
        ClosedDate date NULL,
        MileageKm int NOT NULL,
        CreatedUtc datetime2(0) NOT NULL CONSTRAINT DF_AS_RepairCreated DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_AS_RepairVehicleDealer FOREIGN KEY (VehicleId, DealerId) REFERENCES aftersales.Vehicles(VehicleId, DealerId),
        CONSTRAINT CK_AS_RepairStatus CHECK (Status IN (N'Booked', N'Diagnosing', N'AwaitingParts', N'InRepair', N'Completed', N'Cancelled')),
        CONSTRAINT CK_AS_RepairPriority CHECK (Priority IN (N'Normal', N'Urgent')),
        CONSTRAINT CK_AS_RepairMileage CHECK (MileageKm >= 0),
        CONSTRAINT CK_AS_RepairDates CHECK (PromisedDate >= OpenedDate AND (ClosedDate IS NULL OR ClosedDate >= OpenedDate)),
        CONSTRAINT CK_AS_RepairClosed CHECK ((Status IN (N'Completed', N'Cancelled') AND ClosedDate IS NOT NULL)
            OR (Status NOT IN (N'Completed', N'Cancelled') AND ClosedDate IS NULL))
    );

    IF OBJECT_ID(N'aftersales.RepairLines', N'U') IS NULL
    CREATE TABLE aftersales.RepairLines
    (
        RepairOrderId int NOT NULL,
        LineNumber smallint NOT NULL,
        OperationCode nvarchar(30) NOT NULL,
        LabourHours decimal(6,2) NOT NULL,
        LabourRateEur decimal(10,2) NOT NULL,
        MaterialsAmountEur decimal(12,2) NOT NULL CONSTRAINT DF_AS_LineMaterials DEFAULT (0),
        LineTotalEur AS CONVERT(decimal(18,2), LabourHours * LabourRateEur + MaterialsAmountEur) PERSISTED,
        Status nvarchar(20) NOT NULL CONSTRAINT DF_AS_LineStatus DEFAULT N'Planned',
        CONSTRAINT PK_AS_RepairLines PRIMARY KEY (RepairOrderId, LineNumber),
        CONSTRAINT FK_AS_LineRepair FOREIGN KEY (RepairOrderId) REFERENCES aftersales.RepairOrders(RepairOrderId),
        CONSTRAINT FK_AS_LineOperation FOREIGN KEY (OperationCode) REFERENCES aftersales.ServiceOperations(OperationCode),
        CONSTRAINT CK_AS_LineNumber CHECK (LineNumber > 0),
        CONSTRAINT CK_AS_LineAmounts CHECK (LabourHours >= 0 AND LabourRateEur >= 0 AND MaterialsAmountEur >= 0),
        CONSTRAINT CK_AS_LineStatus CHECK (Status IN (N'Planned', N'Waiting', N'Completed', N'Cancelled'))
    );

    IF OBJECT_ID(N'aftersales.WarrantyCases', N'U') IS NULL
    CREATE TABLE aftersales.WarrantyCases
    (
        WarrantyCaseId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_AS_WarrantyCases PRIMARY KEY,
        CaseNumber nvarchar(30) NOT NULL CONSTRAINT UQ_AS_WarrantyNumber UNIQUE,
        RepairOrderId int NOT NULL,
        SubmittedDate date NOT NULL,
        DecisionDate date NULL,
        Status nvarchar(20) NOT NULL CONSTRAINT DF_AS_WarrantyStatus DEFAULT N'Submitted',
        Reason nvarchar(300) NOT NULL,
        ClaimedAmountEur decimal(12,2) NOT NULL,
        ApprovedAmountEur decimal(12,2) NOT NULL CONSTRAINT DF_AS_WarrantyApproved DEFAULT (0),
        CONSTRAINT FK_AS_WarrantyRepair FOREIGN KEY (RepairOrderId) REFERENCES aftersales.RepairOrders(RepairOrderId),
        CONSTRAINT CK_AS_WarrantyStatus CHECK (Status IN (N'Submitted', N'UnderReview', N'Approved', N'PartiallyApproved', N'Rejected')),
        CONSTRAINT CK_AS_WarrantyAmounts CHECK (ClaimedAmountEur > 0 AND ApprovedAmountEur BETWEEN 0 AND ClaimedAmountEur),
        CONSTRAINT CK_AS_WarrantyDecision CHECK (
            (Status IN (N'Submitted', N'UnderReview') AND DecisionDate IS NULL AND ApprovedAmountEur = 0)
            OR (Status = N'Rejected' AND DecisionDate IS NOT NULL AND ApprovedAmountEur = 0)
            OR (Status = N'Approved' AND DecisionDate IS NOT NULL AND ApprovedAmountEur = ClaimedAmountEur)
            OR (Status = N'PartiallyApproved' AND DecisionDate IS NOT NULL AND ApprovedAmountEur > 0 AND ApprovedAmountEur < ClaimedAmountEur)),
        CONSTRAINT CK_AS_WarrantyDates CHECK (DecisionDate IS NULL OR DecisionDate >= SubmittedDate)
    );

    IF OBJECT_ID(N'aftersales.RepairStatusEvents', N'U') IS NULL
    CREATE TABLE aftersales.RepairStatusEvents
    (
        RepairOrderId int NOT NULL,
        EventSequence smallint NOT NULL,
        OccurredUtc datetime2(0) NOT NULL,
        Status nvarchar(30) NOT NULL,
        PublicNote nvarchar(300) NOT NULL,
        CONSTRAINT PK_AS_RepairEvents PRIMARY KEY (RepairOrderId, EventSequence),
        CONSTRAINT FK_AS_EventRepair FOREIGN KEY (RepairOrderId) REFERENCES aftersales.RepairOrders(RepairOrderId),
        CONSTRAINT CK_AS_EventSequence CHECK (EventSequence > 0),
        CONSTRAINT CK_AS_EventStatus CHECK (Status IN (N'Booked', N'Diagnosing', N'AwaitingParts', N'InRepair', N'Completed', N'Cancelled'))
    );

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'aftersales.Vehicles') AND name=N'IX_AS_VehicleDealer')
        CREATE INDEX IX_AS_VehicleDealer ON aftersales.Vehicles(DealerId, IsActive);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'aftersales.RepairOrders') AND name=N'IX_AS_RepairDealerStatus')
        CREATE INDEX IX_AS_RepairDealerStatus ON aftersales.RepairOrders(DealerId, Status) INCLUDE (PromisedDate, OpenedDate, VehicleId);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'aftersales.RepairOrders') AND name=N'IX_AS_RepairVehicle')
        CREATE INDEX IX_AS_RepairVehicle ON aftersales.RepairOrders(VehicleId, DealerId);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'aftersales.RepairLines') AND name=N'IX_AS_LineOperation')
        CREATE INDEX IX_AS_LineOperation ON aftersales.RepairLines(OperationCode);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'aftersales.WarrantyCases') AND name=N'IX_AS_WarrantyRepairStatus')
        CREATE INDEX IX_AS_WarrantyRepairStatus ON aftersales.WarrantyCases(RepairOrderId, Status) INCLUDE (ClaimedAmountEur, ApprovedAmountEur);

    -- Same business keys as Tenant 1, not copied business transactions. D004 intentionally absent.
    INSERT aftersales.DealerAccounts (DealerId, DealerName, IsActive)
    SELECT s.Id, s.Name, s.Active FROM (VALUES
        (N'D001', N'Detroit Motors', 1), (N'D002', N'Lisbon Garage', 1),
        (N'D003', N'Detroit Motors', 1), (N'D005', N'Chennai Motors', 0),
        (N'T2ONLY-001', N'Demo Independent Service Centre', 1),
        (N'T2ONLY-002', N'Demo Electric Vehicle Workshop', 1),
        (N'T2ONLY-003', N'Demo Closed Service Centre', 0),
        (N'T2ONLY-004', N'Demo Newly Onboarded Workshop', 1)
    ) s(Id, Name, Active)
    WHERE NOT EXISTS (SELECT 1 FROM aftersales.DealerAccounts d WHERE d.DealerId=s.Id);

    INSERT aftersales.Vehicles (VehicleReference, DealerId, ModelName, ModelYear, WarrantyStart, WarrantyEnd, IsActive)
    SELECT s.Ref, s.Dealer, s.Model, s.Year, CONVERT(date,s.StartDate,112), CONVERT(date,s.EndDate,112), s.Active
    FROM (VALUES
        (N'VEH-DEMO-001', N'D001', N'Demo Compact', 2025, '20250101', '20271231', 1),
        (N'VEH-DEMO-002', N'D001', N'Demo SUV', 2024, '20240101', '20261231', 1),
        (N'VEH-DEMO-003', N'D002', N'Demo Electric', 2026, '20260101', '20281231', 1),
        (N'VEH-DEMO-004', N'D002', N'Demo Estate', 2020, '20200101', '20221231', 1),
        (N'VEH-DEMO-005', N'D003', N'Demo Compact', 2025, '20250101', '20271231', 1),
        (N'VEH-DEMO-006', N'D003', N'Demo SUV', 2023, '20230101', '20251231', 1),
        (N'VEH-DEMO-007', N'D005', N'Demo Estate', 2022, '20220101', '20241231', 0),
        (N'VEH-DEMO-008', N'T2ONLY-001', N'Demo Electric', 2026, '20260101', '20281231', 1),
        (N'VEH-DEMO-009', N'T2ONLY-002', N'Demo Electric', 2025, '20250101', '20271231', 1),
        (N'VEH-DEMO-010', N'T2ONLY-003', N'Demo Compact', 2021, '20210101', '20231231', 0)
    ) s(Ref,Dealer,Model,Year,StartDate,EndDate,Active)
    WHERE NOT EXISTS (SELECT 1 FROM aftersales.Vehicles v WHERE v.VehicleReference=s.Ref);

    INSERT aftersales.ServiceOperations (OperationCode, Description, Category, StandardHours, IsActive)
    SELECT s.Code,s.Description,s.Category,s.Hours,s.Active FROM (VALUES
        (N'DIAG-01', N'Electronic fault diagnosis', N'Diagnostics', 1.00, 1),
        (N'BRAKE-01', N'Brake system repair', N'Safety', 2.00, 1),
        (N'BATTERY-01', N'Battery module replacement', N'Repair', 3.00, 1),
        (N'OIL-01', N'Engine oil and filter service', N'Maintenance', 1.00, 1),
        (N'COOL-01', N'Cooling system repair', N'Repair', 2.50, 1),
        (N'LEGACY-01', N'Retired inspection procedure', N'Maintenance', 0.50, 0)
    ) s(Code,Description,Category,Hours,Active)
    WHERE NOT EXISTS (SELECT 1 FROM aftersales.ServiceOperations o WHERE o.OperationCode=s.Code);

    -- Stable dates allow repeatable reporting at @AsOfDate='20260918'.
    INSERT aftersales.RepairOrders (RepairOrderNumber,DealerId,VehicleId,Complaint,Status,Priority,OpenedDate,PromisedDate,ClosedDate,MileageKm)
    SELECT s.Number,v.DealerId,v.VehicleId,s.Complaint,s.Status,s.Priority,
        CONVERT(date,s.Opened,112),CONVERT(date,s.Promised,112),CONVERT(date,s.Closed,112),s.Mileage
    FROM (VALUES
        (N'RO-2026-001',N'VEH-DEMO-001',N'Brake warning and noise',N'AwaitingParts',N'Urgent','20260910','20260916',NULL,18000),
        (N'RO-2026-002',N'VEH-DEMO-002',N'Scheduled oil service',N'Completed',N'Normal','20260901','20260903','20260902',42000),
        (N'RO-2026-003',N'VEH-DEMO-001',N'Intermittent warning lamp',N'Booked',N'Normal','20260918','20260925',NULL,18200),
        (N'RO-2026-004',N'VEH-DEMO-003',N'Battery range loss',N'InRepair',N'Urgent','20260912','20260920',NULL,9000),
        (N'RO-2026-005',N'VEH-DEMO-004',N'Coolant leak outside warranty',N'Completed',N'Normal','20260801','20260805','20260806',120000),
        (N'RO-2026-006',N'VEH-DEMO-005',N'Engine fault requires diagnosis',N'Diagnosing',N'Normal','20260917','20260922',NULL,23000),
        (N'RO-2026-007',N'VEH-DEMO-006',N'Brake service appointment cancelled',N'Cancelled',N'Normal','20260901','20260906','20260902',70000),
        (N'RO-2026-008',N'VEH-DEMO-007',N'Historic inspection at inactive account',N'Completed',N'Normal','20240301','20240302','20240302',30000),
        (N'RO-2026-009',N'VEH-DEMO-008',N'Battery module fault',N'AwaitingParts',N'Urgent','20260908','20260914',NULL,6000),
        (N'RO-2026-010',N'VEH-DEMO-009',N'Cooling system warning',N'InRepair',N'Normal','20260915','20260921',NULL,26000),
        (N'RO-2026-011',N'VEH-DEMO-010',N'Historic brake repair',N'Completed',N'Normal','20240601','20240603','20240603',55000),
        (N'RO-2026-012',N'VEH-DEMO-005',N'Cooling system repair completed',N'Completed',N'Normal','20260810','20260815','20260814',22000),
        (N'RO-2026-013',N'VEH-DEMO-001',N'Cabin climate control fault',N'Diagnosing',N'Normal','20260919','20260924',NULL,18500),
        (N'RO-2026-014',N'VEH-DEMO-002',N'Parking sensor warning',N'Booked',N'Normal','20260920','20260926',NULL,42500),
        (N'RO-2026-015',N'VEH-DEMO-001',N'Brake pedal vibration',N'AwaitingParts',N'Urgent','20260914','20260919',NULL,18800),
        (N'RO-2026-016',N'VEH-DEMO-002',N'Battery charge warning',N'InRepair',N'Urgent','20260916','20260923',NULL,42900),
        (N'RO-2026-017',N'VEH-DEMO-001',N'Infotainment restart issue',N'Booked',N'Normal','20260921','20260927',NULL,19000),
        (N'RO-2026-018',N'VEH-DEMO-002',N'Annual safety inspection',N'Completed',N'Normal','20260905','20260907','20260906',41800),
        (N'RO-2026-019',N'VEH-DEMO-001',N'Headlamp moisture inspection',N'Diagnosing',N'Normal','20260918','20260922',NULL,19200),
        (N'RO-2026-020',N'VEH-DEMO-002',N'Power steering noise',N'AwaitingParts',N'Urgent','20260912','20260918',NULL,43000),
        (N'RO-2026-021',N'VEH-DEMO-001',N'Wiper motor replacement',N'Completed',N'Normal','20260908','20260910','20260909',19400)
    ) s(Number,Vehicle,Complaint,Status,Priority,Opened,Promised,Closed,Mileage)
    JOIN aftersales.Vehicles v ON v.VehicleReference=s.Vehicle
    WHERE NOT EXISTS (SELECT 1 FROM aftersales.RepairOrders r WHERE r.RepairOrderNumber=s.Number);

    INSERT aftersales.RepairLines (RepairOrderId,LineNumber,OperationCode,LabourHours,LabourRateEur,MaterialsAmountEur,Status)
    SELECT r.RepairOrderId,s.Line,s.Operation,s.Hours,s.Rate,s.Materials,s.Status FROM (VALUES
        (N'RO-2026-001',1,N'DIAG-01',1.00,80.00,0.00,N'Completed'),
        (N'RO-2026-001',2,N'BRAKE-01',2.00,80.00,220.00,N'Waiting'),
        (N'RO-2026-002',1,N'OIL-01',1.00,75.00,65.00,N'Completed'),
        (N'RO-2026-003',1,N'DIAG-01',1.00,80.00,0.00,N'Planned'),
        (N'RO-2026-004',1,N'DIAG-01',1.00,90.00,0.00,N'Completed'),
        (N'RO-2026-004',2,N'BATTERY-01',3.00,90.00,1500.00,N'Planned'),
        (N'RO-2026-005',1,N'DIAG-01',1.00,70.00,0.00,N'Completed'),
        (N'RO-2026-005',2,N'COOL-01',2.50,70.00,180.00,N'Completed'),
        (N'RO-2026-006',1,N'DIAG-01',1.00,80.00,0.00,N'Planned'),
        (N'RO-2026-007',1,N'BRAKE-01',2.00,80.00,200.00,N'Cancelled'),
        (N'RO-2026-008',1,N'LEGACY-01',0.50,60.00,0.00,N'Completed'),
        (N'RO-2026-009',1,N'DIAG-01',1.00,85.00,0.00,N'Completed'),
        (N'RO-2026-009',2,N'BATTERY-01',3.00,85.00,1600.00,N'Waiting'),
        (N'RO-2026-010',1,N'DIAG-01',1.00,85.00,0.00,N'Completed'),
        (N'RO-2026-010',2,N'COOL-01',2.50,85.00,200.00,N'Planned'),
        (N'RO-2026-011',1,N'BRAKE-01',2.00,65.00,180.00,N'Completed'),
        (N'RO-2026-012',1,N'DIAG-01',1.00,80.00,0.00,N'Completed'),
        (N'RO-2026-012',2,N'COOL-01',2.50,80.00,200.00,N'Completed'),
        (N'RO-2026-013',1,N'DIAG-01',1.00,80.00,0.00,N'Planned'),
        (N'RO-2026-014',1,N'DIAG-01',1.00,75.00,0.00,N'Planned'),
        (N'RO-2026-015',1,N'BRAKE-01',2.00,80.00,210.00,N'Waiting'),
        (N'RO-2026-016',1,N'BATTERY-01',3.00,75.00,850.00,N'Planned'),
        (N'RO-2026-017',1,N'DIAG-01',1.00,80.00,0.00,N'Planned'),
        (N'RO-2026-018',1,N'BRAKE-01',2.00,75.00,160.00,N'Completed'),
        (N'RO-2026-019',1,N'DIAG-01',1.00,80.00,0.00,N'Planned'),
        (N'RO-2026-020',1,N'COOL-01',2.50,75.00,180.00,N'Waiting'),
        (N'RO-2026-021',1,N'DIAG-01',1.00,80.00,90.00,N'Completed')
    ) s(Number,Line,Operation,Hours,Rate,Materials,Status)
    JOIN aftersales.RepairOrders r ON r.RepairOrderNumber=s.Number
    WHERE NOT EXISTS (SELECT 1 FROM aftersales.RepairLines l WHERE l.RepairOrderId=r.RepairOrderId AND l.LineNumber=s.Line);

    INSERT aftersales.WarrantyCases (CaseNumber,RepairOrderId,SubmittedDate,DecisionDate,Status,Reason,ClaimedAmountEur,ApprovedAmountEur)
    SELECT s.Number,r.RepairOrderId,CONVERT(date,s.Submitted,112),CONVERT(date,s.Decided,112),s.Status,s.Reason,s.Claimed,s.Approved
    FROM (VALUES
        (N'WC-001',N'RO-2026-001','20260911',NULL,N'UnderReview',N'Brake component defect evidence under review',460.00,0.00),
        (N'WC-002',N'RO-2026-004','20260913','20260915',N'Approved',N'Battery module defect confirmed',1860.00,1860.00),
        (N'WC-003',N'RO-2026-005','20260802','20260804',N'Rejected',N'Warranty expired before repair opened',425.00,0.00),
        (N'WC-004',N'RO-2026-006','20260918',NULL,N'Submitted',N'Diagnostic evidence pending',80.00,0.00),
        (N'WC-005',N'RO-2026-012','20260811','20260813',N'PartiallyApproved',N'Cooling component covered; diagnostic cost excluded',480.00,400.00),
        (N'WC-006',N'RO-2026-009','20260909',NULL,N'UnderReview',N'Battery replacement authorization pending',1940.00,0.00),
        (N'WC-007',N'RO-2026-010','20260916','20260917',N'Approved',N'Cooling component manufacturing defect',497.50,497.50),
        (N'WC-008',N'RO-2026-013','20260919',NULL,N'Submitted',N'Climate control diagnostic evidence pending',80.00,0.00),
        (N'WC-009',N'RO-2026-014', '20260920',NULL,N'UnderReview',N'Parking sensor warranty eligibility review',75.00,0.00),
        (N'WC-010',N'RO-2026-015','20260915',NULL,N'UnderReview',N'Brake vibration parts authorization pending',370.00,0.00),
        (N'WC-011',N'RO-2026-016','20260917','20260918',N'PartiallyApproved',N'Battery module covered; labor excluded',1075.00,850.00),
        (N'WC-012',N'RO-2026-017','20260921',NULL,N'Submitted',N'Infotainment software diagnosis submitted',80.00,0.00),
        (N'WC-013',N'RO-2026-018','20260905','20260906',N'Approved',N'Safety inspection repair covered',310.00,310.00),
        (N'WC-014',N'RO-2026-019','20260918',NULL,N'UnderReview',N'Headlamp sealing inspection under review',80.00,0.00),
        (N'WC-015',N'RO-2026-020','20260913',NULL,N'Submitted',N'Power steering component assessment pending',367.50,0.00),
        (N'WC-016',N'RO-2026-021','20260908','20260909',N'Approved',N'Wiper motor manufacturing fault confirmed',170.00,170.00)
    ) s(Number,Repair,Submitted,Decided,Status,Reason,Claimed,Approved)
    JOIN aftersales.RepairOrders r ON r.RepairOrderNumber=s.Repair
    WHERE NOT EXISTS (SELECT 1 FROM aftersales.WarrantyCases w WHERE w.CaseNumber=s.Number);

    -- One opening event and one current event per repair. Both are seeded only if missing.
    INSERT aftersales.RepairStatusEvents (RepairOrderId,EventSequence,OccurredUtc,Status,PublicNote)
    SELECT r.RepairOrderId,1,CONVERT(datetime2(0),r.OpenedDate),N'Booked',N'Repair request registered.'
    FROM aftersales.RepairOrders r
    WHERE r.RepairOrderNumber IN (N'RO-2026-001',N'RO-2026-002',N'RO-2026-003',N'RO-2026-004',N'RO-2026-005',N'RO-2026-006',N'RO-2026-007',N'RO-2026-008',N'RO-2026-009',N'RO-2026-010',N'RO-2026-011',N'RO-2026-012')
        AND NOT EXISTS (SELECT 1 FROM aftersales.RepairStatusEvents e WHERE e.RepairOrderId=r.RepairOrderId AND e.EventSequence=1);
    INSERT aftersales.RepairStatusEvents (RepairOrderId,EventSequence,OccurredUtc,Status,PublicNote)
    SELECT r.RepairOrderId,2,DATEADD(hour,12,CONVERT(datetime2(0),COALESCE(r.ClosedDate,r.OpenedDate))),r.Status,
        CASE r.Status WHEN N'AwaitingParts' THEN N'Repair waiting for material availability; no parts-order linkage has been asserted.'
            WHEN N'Completed' THEN N'Repair work completed.' WHEN N'Cancelled' THEN N'Appointment cancelled.'
            WHEN N'InRepair' THEN N'Workshop repair work in progress.' ELSE N'Diagnostic work in progress.' END
    FROM aftersales.RepairOrders r
    WHERE r.RepairOrderNumber IN (N'RO-2026-001',N'RO-2026-002',N'RO-2026-004',N'RO-2026-005',N'RO-2026-006',N'RO-2026-007',N'RO-2026-008',N'RO-2026-009',N'RO-2026-010',N'RO-2026-011',N'RO-2026-012')
        AND NOT EXISTS (SELECT 1 FROM aftersales.RepairStatusEvents e WHERE e.RepairOrderId=r.RepairOrderId AND e.EventSequence=2);

    -- Approved read projections. No contact information, credentials or internal configuration.
    EXEC(N'CREATE OR ALTER VIEW aftersales.vw_RepairOverview AS
        SELECT d.DealerId,d.DealerName,d.IsActive AS DealerIsActive,r.RepairOrderNumber,
            v.VehicleReference,v.ModelName,r.Complaint,r.Status,r.Priority,r.OpenedDate,r.PromisedDate,r.ClosedDate,
            COALESCE(t.EstimatedAmountEur,CONVERT(decimal(38,2),0)) AS EstimatedAmountEur
        FROM aftersales.RepairOrders r
        JOIN aftersales.DealerAccounts d ON d.DealerId=r.DealerId
        JOIN aftersales.Vehicles v ON v.VehicleId=r.VehicleId AND v.DealerId=r.DealerId
        OUTER APPLY (SELECT SUM(l.LineTotalEur) AS EstimatedAmountEur FROM aftersales.RepairLines l
            WHERE l.RepairOrderId=r.RepairOrderId AND l.Status<>N''Cancelled'') t;');
    EXEC(N'CREATE OR ALTER VIEW aftersales.vw_WarrantyOverview AS
        SELECT r.DealerId,r.RepairOrderNumber,w.CaseNumber,w.Status,w.Reason,w.SubmittedDate,w.DecisionDate,
            w.ClaimedAmountEur,w.ApprovedAmountEur
        FROM aftersales.WarrantyCases w JOIN aftersales.RepairOrders r ON r.RepairOrderId=w.RepairOrderId;');
    EXEC(N'CREATE OR ALTER VIEW aftersales.vw_DealerWorkload AS
        SELECT d.DealerId,d.DealerName,d.IsActive,COUNT(r.RepairOrderId) AS TotalRepairs,
            SUM(CASE WHEN r.Status IN (N''Booked'',N''Diagnosing'',N''AwaitingParts'',N''InRepair'') THEN 1 ELSE 0 END) AS OpenRepairs,
            SUM(CASE WHEN r.Status=N''AwaitingParts'' THEN 1 ELSE 0 END) AS AwaitingParts,
            SUM(CASE WHEN r.Status=N''Completed'' THEN 1 ELSE 0 END) AS CompletedRepairs
        FROM aftersales.DealerAccounts d LEFT JOIN aftersales.RepairOrders r ON r.DealerId=d.DealerId
        GROUP BY d.DealerId,d.DealerName,d.IsActive;');

    -- Parameters are deliberately wider than the key to reject oversize input rather than truncate it.
    -- Unknown dealer => empty result sets. Existing dealer with no work => account row + empty work lists.
    EXEC(N'CREATE OR ALTER PROCEDURE aftersales.usp_GetDealerServiceOverview
        @DealerId nvarchar(4000)
    AS
    BEGIN
        SET NOCOUNT ON;
        IF @DealerId IS NULL OR DATALENGTH(@DealerId) NOT BETWEEN 2 AND 100
            OR @DealerId COLLATE Latin1_General_100_BIN2 LIKE N''%[^A-Z0-9-]%''
            THROW 51110, ''Invalid DealerId. Use 1-50 uppercase letters, digits or hyphens.'', 1;
        SELECT DealerId,DealerName,IsActive FROM aftersales.DealerAccounts WHERE DealerId=@DealerId;
        SELECT DealerId,RepairOrderNumber,VehicleReference,ModelName,Complaint,Status,Priority,
            OpenedDate,PromisedDate,ClosedDate,EstimatedAmountEur
        FROM aftersales.vw_RepairOverview WHERE DealerId=@DealerId ORDER BY OpenedDate DESC,RepairOrderNumber;
        SELECT r.DealerId,r.RepairOrderNumber,l.LineNumber,l.OperationCode,o.Description,
            l.LabourHours,l.LabourRateEur,l.MaterialsAmountEur,l.LineTotalEur,l.Status
        FROM aftersales.RepairLines l JOIN aftersales.RepairOrders r ON r.RepairOrderId=l.RepairOrderId
        JOIN aftersales.ServiceOperations o ON o.OperationCode=l.OperationCode
        WHERE r.DealerId=@DealerId ORDER BY r.RepairOrderNumber,l.LineNumber;
    END;');
    EXEC(N'CREATE OR ALTER PROCEDURE aftersales.usp_GetDealerRepairStatus
        @DealerId nvarchar(4000), @AsOfDate date=NULL
    AS
    BEGIN
        SET NOCOUNT ON;
        IF @DealerId IS NULL OR DATALENGTH(@DealerId) NOT BETWEEN 2 AND 100
            OR @DealerId COLLATE Latin1_General_100_BIN2 LIKE N''%[^A-Z0-9-]%''
            THROW 51110, ''Invalid DealerId. Use 1-50 uppercase letters, digits or hyphens.'', 1;
        SET @AsOfDate=COALESCE(@AsOfDate,CONVERT(date,SYSUTCDATETIME()));
        SELECT DealerId,DealerName,IsActive FROM aftersales.DealerAccounts WHERE DealerId=@DealerId;
        SELECT DealerId,RepairOrderNumber,Status,Priority,PromisedDate,@AsOfDate AS EvaluationDate,
            CONVERT(bit,CASE WHEN Status NOT IN (N''Completed'',N''Cancelled'') AND PromisedDate<@AsOfDate THEN 1 ELSE 0 END) AS IsOverdue
        FROM aftersales.vw_RepairOverview WHERE DealerId=@DealerId ORDER BY PromisedDate,RepairOrderNumber;
        SELECT r.DealerId,r.RepairOrderNumber,e.EventSequence,e.OccurredUtc,e.Status,e.PublicNote
        FROM aftersales.RepairStatusEvents e JOIN aftersales.RepairOrders r ON r.RepairOrderId=e.RepairOrderId
        WHERE r.DealerId=@DealerId ORDER BY r.RepairOrderNumber,e.EventSequence;
    END;');
    EXEC(N'CREATE OR ALTER PROCEDURE aftersales.usp_GetDealerWarrantySummary
        @DealerId nvarchar(4000)
    AS
    BEGIN
        SET NOCOUNT ON;
        IF @DealerId IS NULL OR DATALENGTH(@DealerId) NOT BETWEEN 2 AND 100
            OR @DealerId COLLATE Latin1_General_100_BIN2 LIKE N''%[^A-Z0-9-]%''
            THROW 51110, ''Invalid DealerId. Use 1-50 uppercase letters, digits or hyphens.'', 1;
        SELECT d.DealerId,d.DealerName,d.IsActive,COUNT(w.CaseNumber) AS TotalCases,
            SUM(CASE WHEN w.Status IN (N''Submitted'',N''UnderReview'') THEN 1 ELSE 0 END) AS PendingCases,
            COALESCE(SUM(w.ClaimedAmountEur),0) AS ClaimedAmountEur,
            COALESCE(SUM(w.ApprovedAmountEur),0) AS ApprovedAmountEur
        FROM aftersales.DealerAccounts d LEFT JOIN aftersales.vw_WarrantyOverview w ON w.DealerId=d.DealerId
        WHERE d.DealerId=@DealerId GROUP BY d.DealerId,d.DealerName,d.IsActive;
        SELECT DealerId,RepairOrderNumber,CaseNumber,Status,Reason,SubmittedDate,DecisionDate,ClaimedAmountEur,ApprovedAmountEur
        FROM aftersales.vw_WarrantyOverview WHERE DealerId=@DealerId ORDER BY SubmittedDate DESC,CaseNumber;
    END;');

    IF EXISTS (SELECT 1 FROM sys.sql_expression_dependencies
        WHERE referencing_id IN (SELECT object_id FROM sys.objects WHERE schema_id=SCHEMA_ID(N'aftersales'))
            AND (referenced_database_name IS NOT NULL OR referenced_server_name IS NOT NULL))
        THROW 51111, 'Cross-database dependencies are prohibited.', 1;
    IF EXISTS (SELECT 1 FROM sys.synonyms WHERE schema_id=SCHEMA_ID(N'aftersales'))
        THROW 51112, 'Synonyms are not permitted in this demo schema.', 1;
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE schema_id=SCHEMA_ID(N'aftersales') AND (is_disabled=1 OR is_not_trusted=1))
        THROW 51113, 'All foreign keys must be enabled and trusted.', 1;
    IF EXISTS (SELECT 1 FROM aftersales.WarrantyCases w JOIN aftersales.RepairOrders r ON r.RepairOrderId=w.RepairOrderId WHERE w.SubmittedDate<r.OpenedDate)
        THROW 51114, 'Warranty submission cannot precede repair opening.', 1;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

-- Fresh-install counts: 8,10,6,12,18,7,23. Additional existing records are preserved.
SELECT N'DealerAccounts' AS TableName,COUNT_BIG(*) AS RecordCount FROM aftersales.DealerAccounts
UNION ALL SELECT N'Vehicles',COUNT_BIG(*) FROM aftersales.Vehicles
UNION ALL SELECT N'ServiceOperations',COUNT_BIG(*) FROM aftersales.ServiceOperations
UNION ALL SELECT N'RepairOrders',COUNT_BIG(*) FROM aftersales.RepairOrders
UNION ALL SELECT N'RepairLines',COUNT_BIG(*) FROM aftersales.RepairLines
UNION ALL SELECT N'WarrantyCases',COUNT_BIG(*) FROM aftersales.WarrantyCases
UNION ALL SELECT N'RepairStatusEvents',COUNT_BIG(*) FROM aftersales.RepairStatusEvents;
SELECT DealerId,DealerName,IsActive,TotalRepairs,OpenRepairs,AwaitingParts,CompletedRepairs
FROM aftersales.vw_DealerWorkload ORDER BY DealerId;
EXEC aftersales.usp_GetDealerServiceOverview @DealerId=N'D001';
EXEC aftersales.usp_GetDealerRepairStatus @DealerId=N'D001',@AsOfDate='20260918';
EXEC aftersales.usp_GetDealerWarrantySummary @DealerId=N'D002';
EXEC aftersales.usp_GetDealerWarrantySummary @DealerId=N'D003';
EXEC aftersales.usp_GetDealerServiceOverview @DealerId=N'D005';
EXEC aftersales.usp_GetDealerServiceOverview @DealerId=N'T2ONLY-001';
EXEC aftersales.usp_GetDealerServiceOverview @DealerId=N'T2ONLY-004';
-- Missing Tenant 2 match: all result sets empty on a fresh install; preserve Tenant 1 in the wrapper.
EXEC aftersales.usp_GetDealerServiceOverview @DealerId=N'D004';
BEGIN TRY
    EXEC aftersales.usp_GetDealerServiceOverview @DealerId=N'bad/key';
    THROW 51115, 'Input validation failed.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>51110 THROW;
    PRINT N'PASS: invalid integration key rejected.';
END CATCH;
PRINT N'After-sales Tenant 2 setup completed. Tenant 1 was not accessed or changed. HTTP APIs and AI Core are separate application deliverables.';
