-- SUPERSEDED DOMAIN: use 02_CreateTenant2AfterSalesDb.sql for the requested after-sales demo.
-- This older training installer is retained to preserve prior work; it is not a prerequisite.
-- Tenant 2: Dealer Training and Certification. SQL Server 2019 or later.
-- Execute the ENTIRE file in SSMS on the intended instance.
-- Requires CREATE DATABASE permission (first run) and DDL permission in Tenant2DemoDb.
-- No SQLCMD mode, manual substitutions, external files, or Tenant 1 access are required.
-- DealerId is an API correlation key only; there are no cross-database references.
-- Existing records are never deleted or overwritten. Re-running adds missing demo seeds.
USE [master];
GO
SET NOCOUNT ON;
IF DB_ID(N'Tenant2DemoDb') IS NULL
BEGIN
    IF ISNULL(HAS_PERMS_BY_NAME(NULL, NULL, 'CREATE ANY DATABASE'), 0) <> 1
        THROW 51000, 'Database creation permission is required. Ask an administrator to execute this file or use your permitted LocalDB instance.', 1;
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
    THROW 51001, 'Wrong database selected. Stop: no Tenant 1 objects may be changed.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    IF SCHEMA_ID(N'training') IS NULL
        EXEC(N'CREATE SCHEMA training AUTHORIZATION dbo;');

    IF OBJECT_ID(N'training.DealerAccounts', N'U') IS NULL
    CREATE TABLE training.DealerAccounts
    (
        DealerId nvarchar(50) NOT NULL CONSTRAINT PK_TrainingDealerAccounts PRIMARY KEY,
        DisplayName nvarchar(200) NOT NULL,
        CountryCode char(2) NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_TrainingDealerAccounts_IsActive DEFAULT (1),
        CreatedUtc datetime2(0) NOT NULL CONSTRAINT DF_TrainingDealerAccounts_Created DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_TrainingDealerAccounts_DealerId CHECK (LEN(LTRIM(RTRIM(DealerId))) BETWEEN 1 AND 50 AND DealerId NOT LIKE '%[^A-Z0-9-]%'),
        CONSTRAINT CK_TrainingDealerAccounts_Name CHECK (LEN(LTRIM(RTRIM(DisplayName))) > 0)
    );

    IF OBJECT_ID(N'training.Providers', N'U') IS NULL
    CREATE TABLE training.Providers
    (
        ProviderId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_TrainingProviders PRIMARY KEY,
        ProviderCode nvarchar(30) NOT NULL CONSTRAINT UQ_TrainingProviders_Code UNIQUE,
        ProviderName nvarchar(200) NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_TrainingProviders_IsActive DEFAULT (1)
    );

    IF OBJECT_ID(N'training.Courses', N'U') IS NULL
    CREATE TABLE training.Courses
    (
        CourseId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_TrainingCourses PRIMARY KEY,
        CourseCode nvarchar(30) NOT NULL CONSTRAINT UQ_TrainingCourses_Code UNIQUE,
        ProviderId int NOT NULL,
        Title nvarchar(200) NOT NULL,
        Topic nvarchar(30) NOT NULL,
        DurationHours decimal(5,1) NOT NULL,
        PassMark tinyint NOT NULL,
        ValidityMonths smallint NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_TrainingCourses_IsActive DEFAULT (1),
        CONSTRAINT FK_TrainingCourses_Provider FOREIGN KEY (ProviderId) REFERENCES training.Providers(ProviderId),
        CONSTRAINT CK_TrainingCourses_Topic CHECK (Topic IN (N'Delivery', N'Claims', N'Safety', N'Inventory')),
        CONSTRAINT CK_TrainingCourses_Duration CHECK (DurationHours > 0 AND DurationHours <= 500),
        CONSTRAINT CK_TrainingCourses_PassMark CHECK (PassMark BETWEEN 1 AND 100),
        CONSTRAINT CK_TrainingCourses_Validity CHECK (ValidityMonths BETWEEN 1 AND 120)
    );

    IF OBJECT_ID(N'training.Enrollments', N'U') IS NULL
    CREATE TABLE training.Enrollments
    (
        EnrollmentId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_TrainingEnrollments PRIMARY KEY,
        EnrollmentCode nvarchar(30) NOT NULL CONSTRAINT UQ_TrainingEnrollments_Code UNIQUE,
        DealerId nvarchar(50) NOT NULL,
        CourseId int NOT NULL,
        ParticipantReference nvarchar(50) NOT NULL,
        Status nvarchar(20) NOT NULL CONSTRAINT DF_TrainingEnrollments_Status DEFAULT N'Booked',
        EnrolledDate date NOT NULL,
        ScheduledDate date NOT NULL,
        CompletedDate date NULL,
        CreatedUtc datetime2(0) NOT NULL CONSTRAINT DF_TrainingEnrollments_Created DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_TrainingEnrollments_Dealer FOREIGN KEY (DealerId) REFERENCES training.DealerAccounts(DealerId),
        CONSTRAINT FK_TrainingEnrollments_Course FOREIGN KEY (CourseId) REFERENCES training.Courses(CourseId),
        CONSTRAINT UQ_TrainingEnrollments_Participant UNIQUE (DealerId, CourseId, ParticipantReference, ScheduledDate),
        CONSTRAINT CK_TrainingEnrollments_Status CHECK (Status IN (N'Booked', N'InProgress', N'Completed', N'Cancelled', N'Failed')),
        CONSTRAINT CK_TrainingEnrollments_Dates CHECK (ScheduledDate >= EnrolledDate AND (CompletedDate IS NULL OR CompletedDate >= ScheduledDate)),
        CONSTRAINT CK_TrainingEnrollments_Completion CHECK ((Status IN (N'Completed', N'Failed') AND CompletedDate IS NOT NULL) OR (Status NOT IN (N'Completed', N'Failed') AND CompletedDate IS NULL))
    );

    IF OBJECT_ID(N'training.Assessments', N'U') IS NULL
    CREATE TABLE training.Assessments
    (
        AssessmentId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_TrainingAssessments PRIMARY KEY,
        EnrollmentId int NOT NULL,
        AttemptNumber smallint NOT NULL,
        AssessedDate date NOT NULL,
        Score tinyint NOT NULL,
        Passed bit NOT NULL,
        CONSTRAINT FK_TrainingAssessments_Enrollment FOREIGN KEY (EnrollmentId) REFERENCES training.Enrollments(EnrollmentId),
        CONSTRAINT UQ_TrainingAssessments_Attempt UNIQUE (EnrollmentId, AttemptNumber),
        CONSTRAINT CK_TrainingAssessments_Attempt CHECK (AttemptNumber BETWEEN 1 AND 10),
        CONSTRAINT CK_TrainingAssessments_Score CHECK (Score BETWEEN 0 AND 100)
    );

    IF OBJECT_ID(N'training.Certifications', N'U') IS NULL
    CREATE TABLE training.Certifications
    (
        CertificationId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_TrainingCertifications PRIMARY KEY,
        CertificateNumber nvarchar(40) NOT NULL CONSTRAINT UQ_TrainingCertifications_Number UNIQUE,
        AssessmentId int NOT NULL CONSTRAINT UQ_TrainingCertifications_Assessment UNIQUE,
        IssuedDate date NOT NULL,
        ExpiresDate date NOT NULL,
        IsRevoked bit NOT NULL CONSTRAINT DF_TrainingCertifications_Revoked DEFAULT (0),
        CONSTRAINT FK_TrainingCertifications_Assessment FOREIGN KEY (AssessmentId) REFERENCES training.Assessments(AssessmentId),
        CONSTRAINT CK_TrainingCertifications_Dates CHECK (ExpiresDate > IssuedDate)
    );

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'training.Courses') AND name = N'IX_TrainingCourses_Provider')
        CREATE INDEX IX_TrainingCourses_Provider ON training.Courses(ProviderId);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'training.Enrollments') AND name = N'IX_TrainingEnrollments_DealerStatus')
        CREATE INDEX IX_TrainingEnrollments_DealerStatus ON training.Enrollments(DealerId, Status) INCLUDE (CourseId, ScheduledDate, CompletedDate);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'training.Enrollments') AND name = N'IX_TrainingEnrollments_Course')
        CREATE INDEX IX_TrainingEnrollments_Course ON training.Enrollments(CourseId);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'training.Certifications') AND name = N'IX_TrainingCertifications_Expiry')
        CREATE INDEX IX_TrainingCertifications_Expiry ON training.Certifications(ExpiresDate, IsRevoked);

    INSERT training.DealerAccounts (DealerId, DisplayName, CountryCode, IsActive)
    SELECT s.DealerId, s.DisplayName, s.CountryCode, s.IsActive
    FROM (VALUES
        (N'D001', N'Demo Academy Account D001', 'CZ', 1),
        (N'D002', N'Demo Academy Account D002', 'DE', 1),
        (N'D003', N'Demo Academy Account D003', 'GB', 1),
        (N'D005', N'Demo Academy Account D005', 'FR', 0),
        (N'T2ONLY-001', N'Tenant Two Independent Academy', 'CZ', 1),
        (N'T2ONLY-002', N'Tenant Two Pilot Academy', 'DE', 1),
        (N'T2ONLY-003', N'Tenant Two Closed Academy', 'FR', 0),
        (N'T2ONLY-004', N'Tenant Two New Academy', 'GB', 1)
    ) s(DealerId, DisplayName, CountryCode, IsActive)
    WHERE NOT EXISTS (SELECT 1 FROM training.DealerAccounts d WHERE d.DealerId = s.DealerId);

    INSERT training.Providers (ProviderCode, ProviderName, IsActive)
    SELECT s.Code, s.Name, s.Active
    FROM (VALUES (N'ACADEMY-EU', N'Demo European Training Academy', 1),
        (N'SAFETY-LAB', N'Demo Workshop Safety Lab', 1),
        (N'LEGACY-ACADEMY', N'Demo Retired Training Provider', 0)) s(Code, Name, Active)
    WHERE NOT EXISTS (SELECT 1 FROM training.Providers p WHERE p.ProviderCode = s.Code);

    INSERT training.Courses (CourseCode, ProviderId, Title, Topic, DurationHours, PassMark, ValidityMonths, IsActive)
    SELECT s.Code, p.ProviderId, s.Title, s.Topic, s.Hours, s.PassMark, s.Months, s.Active
    FROM (VALUES
        (N'DELIVERY-101', N'ACADEMY-EU', N'Delivery exception handling', N'Delivery', 4.0, 70, 24, 1),
        (N'CLAIMS-101', N'ACADEMY-EU', N'Warranty claim evidence', N'Claims', 6.0, 75, 24, 1),
        (N'SAFETY-101', N'SAFETY-LAB', N'Workshop safety certification', N'Safety', 8.0, 80, 12, 1),
        (N'STOCK-101', N'ACADEMY-EU', N'Inventory accuracy and cycle counts', N'Inventory', 3.0, 70, 24, 1),
        (N'CLAIMS-201', N'ACADEMY-EU', N'Advanced claims investigation', N'Claims', 8.0, 85, 24, 1),
        (N'DELIVERY-OLD', N'LEGACY-ACADEMY', N'Retired delivery procedure', N'Delivery', 2.0, 60, 12, 0)
    ) s(Code, Provider, Title, Topic, Hours, PassMark, Months, Active)
    JOIN training.Providers p ON p.ProviderCode = s.Provider
    WHERE NOT EXISTS (SELECT 1 FROM training.Courses c WHERE c.CourseCode = s.Code);

    -- Fixed dates make repeated executions deterministic. Use @AsOfDate='2026-09-18'
    -- for repeatable demo reporting; omit it for status as of today's UTC date.
    INSERT training.Enrollments (EnrollmentCode, DealerId, CourseId, ParticipantReference, Status, EnrolledDate, ScheduledDate, CompletedDate)
    SELECT s.Code, s.DealerId, c.CourseId, s.Participant, s.Status,
        CONVERT(date, s.Enrolled, 112), CONVERT(date, s.Scheduled, 112), CONVERT(date, s.Completed, 112)
    FROM (VALUES
        (N'ENR-001', N'D001', N'DELIVERY-101', N'DEMO-P01', N'Completed', '20260101', '20260201', '20260201'),
        (N'ENR-002', N'D001', N'CLAIMS-101', N'DEMO-P01', N'Completed', '20260201', '20260301', '20260301'),
        (N'ENR-003', N'D001', N'SAFETY-101', N'DEMO-P02', N'InProgress', '20260901', '20260918', NULL),
        (N'ENR-004', N'D001', N'CLAIMS-201', N'DEMO-P01', N'Booked', '20260910', '20261001', NULL),
        (N'ENR-005', N'D002', N'DELIVERY-101', N'DEMO-P03', N'Failed', '20260101', '20260210', '20260210'),
        (N'ENR-006', N'D002', N'SAFETY-101', N'DEMO-P03', N'Completed', '20240101', '20240201', '20240201'),
        (N'ENR-007', N'D002', N'CLAIMS-101', N'DEMO-P04', N'Cancelled', '20260801', '20260901', NULL),
        (N'ENR-008', N'D003', N'DELIVERY-101', N'DEMO-P05', N'Completed', '20260501', '20260601', '20260602'),
        (N'ENR-009', N'D003', N'STOCK-101', N'DEMO-P05', N'Completed', '20260601', '20260701', '20260701'),
        (N'ENR-010', N'D003', N'CLAIMS-101', N'DEMO-P06', N'Booked', '20260901', '20261010', NULL),
        (N'ENR-011', N'D005', N'DELIVERY-OLD', N'DEMO-P07', N'Completed', '20240101', '20240301', '20240301'),
        (N'ENR-012', N'T2ONLY-001', N'CLAIMS-101', N'DEMO-P08', N'Completed', '20260401', '20260501', '20260501'),
        (N'ENR-013', N'T2ONLY-001', N'SAFETY-101', N'DEMO-P08', N'Failed', '20260601', '20260701', '20260701'),
        (N'ENR-014', N'T2ONLY-002', N'STOCK-101', N'DEMO-P09', N'InProgress', '20260901', '20260918', NULL),
        (N'ENR-015', N'T2ONLY-002', N'DELIVERY-101', N'DEMO-P10', N'Booked', '20260901', '20261101', NULL),
        (N'ENR-016', N'T2ONLY-003', N'SAFETY-101', N'DEMO-P11', N'Cancelled', '20260801', '20260901', NULL)
    ) s(Code, DealerId, Course, Participant, Status, Enrolled, Scheduled, Completed)
    JOIN training.Courses c ON c.CourseCode = s.Course
    WHERE NOT EXISTS (SELECT 1 FROM training.Enrollments e WHERE e.EnrollmentCode = s.Code);

    INSERT training.Assessments (EnrollmentId, AttemptNumber, AssessedDate, Score, Passed)
    SELECT e.EnrollmentId, s.Attempt, CONVERT(date, s.Assessed, 112), s.Score, s.Passed
    FROM (VALUES
        (N'ENR-001', 1, '20260201', 92, 1), (N'ENR-002', 1, '20260301', 88, 1),
        (N'ENR-005', 1, '20260210', 52, 0), (N'ENR-006', 1, '20240201', 95, 1),
        (N'ENR-008', 1, '20260601', 55, 0), (N'ENR-008', 2, '20260602', 82, 1),
        (N'ENR-009', 1, '20260701', 91, 1), (N'ENR-011', 1, '20240301', 76, 1),
        (N'ENR-012', 1, '20260501', 84, 1), (N'ENR-013', 1, '20260701', 60, 0)
    ) s(Code, Attempt, Assessed, Score, Passed)
    JOIN training.Enrollments e ON e.EnrollmentCode = s.Code
    WHERE NOT EXISTS (SELECT 1 FROM training.Assessments a WHERE a.EnrollmentId = e.EnrollmentId AND a.AttemptNumber = s.Attempt);

    INSERT training.Certifications (CertificateNumber, AssessmentId, IssuedDate, ExpiresDate, IsRevoked)
    SELECT s.Number, a.AssessmentId, a.AssessedDate, DATEADD(month, c.ValidityMonths, a.AssessedDate), s.Revoked
    FROM (VALUES
        (N'CERT-001', N'ENR-001', 1, 0), (N'CERT-002', N'ENR-002', 1, 0),
        (N'CERT-003', N'ENR-006', 1, 0), (N'CERT-004', N'ENR-008', 2, 0),
        (N'CERT-005', N'ENR-009', 1, 1), (N'CERT-006', N'ENR-011', 1, 0),
        (N'CERT-007', N'ENR-012', 1, 0)
    ) s(Number, Code, Attempt, Revoked)
    JOIN training.Enrollments e ON e.EnrollmentCode = s.Code
    JOIN training.Courses c ON c.CourseId = e.CourseId
    JOIN training.Assessments a ON a.EnrollmentId = e.EnrollmentId AND a.AttemptNumber = s.Attempt
    WHERE NOT EXISTS (SELECT 1 FROM training.Certifications cert WHERE cert.CertificateNumber = s.Number);

    EXEC(N'CREATE OR ALTER VIEW training.vw_DealerTrainingDetails AS
        SELECT d.DealerId, d.DisplayName, d.IsActive AS DealerIsActive,
            e.EnrollmentCode, c.CourseCode, c.Title AS CourseTitle, c.Topic,
            e.Status AS EnrollmentStatus, e.ScheduledDate, e.CompletedDate,
            cert.CertificateNumber, cert.IssuedDate, cert.ExpiresDate, cert.IsRevoked
        FROM training.DealerAccounts d
        LEFT JOIN training.Enrollments e ON e.DealerId = d.DealerId
        LEFT JOIN training.Courses c ON c.CourseId = e.CourseId
        LEFT JOIN training.Assessments a ON a.EnrollmentId = e.EnrollmentId AND a.Passed = 1
        LEFT JOIN training.Certifications cert ON cert.AssessmentId = a.AssessmentId;');

    EXEC(N'CREATE OR ALTER VIEW training.vw_CourseReporting AS
        SELECT c.CourseCode, c.Title, c.Topic, c.IsActive,
            COUNT(e.EnrollmentId) AS EnrollmentCount,
            SUM(CASE WHEN e.Status = N''Completed'' THEN 1 ELSE 0 END) AS CompletedCount,
            SUM(CASE WHEN e.Status = N''Failed'' THEN 1 ELSE 0 END) AS FailedCount,
            SUM(CASE WHEN e.Status = N''Booked'' THEN 1 ELSE 0 END) AS BookedCount
        FROM training.Courses c
        LEFT JOIN training.Enrollments e ON e.CourseId = c.CourseId
        GROUP BY c.CourseCode, c.Title, c.Topic, c.IsActive;');

    EXEC(N'CREATE OR ALTER PROCEDURE training.usp_GetDealerTrainingDetails
        @DealerId nvarchar(4000)
    AS
    BEGIN
        SET NOCOUNT ON;
        IF @DealerId IS NULL OR LEN(LTRIM(RTRIM(@DealerId))) = 0 OR DATALENGTH(@DealerId) > 100
            OR @DealerId <> LTRIM(RTRIM(@DealerId)) OR @DealerId LIKE N''%[^A-Z0-9-]%''
            THROW 51010, ''Invalid DealerId. Use a dealer business key of up to 50 letters, digits or hyphens.'', 1;
        SELECT DealerId, DisplayName, DealerIsActive, EnrollmentCode, CourseCode,
            CourseTitle, Topic, EnrollmentStatus, ScheduledDate, CompletedDate,
            CertificateNumber, IssuedDate, ExpiresDate, IsRevoked
        FROM training.vw_DealerTrainingDetails WHERE DealerId = @DealerId
        ORDER BY ScheduledDate DESC, EnrollmentCode, CertificateNumber;
    END;');

    EXEC(N'CREATE OR ALTER PROCEDURE training.usp_GetDealerTrainingSummary
        @DealerId nvarchar(4000), @AsOfDate date = NULL
    AS
    BEGIN
        SET NOCOUNT ON;
        IF @DealerId IS NULL OR LEN(LTRIM(RTRIM(@DealerId))) = 0 OR DATALENGTH(@DealerId) > 100
            OR @DealerId <> LTRIM(RTRIM(@DealerId)) OR @DealerId LIKE N''%[^A-Z0-9-]%''
            THROW 51010, ''Invalid DealerId. Use a dealer business key of up to 50 letters, digits or hyphens.'', 1;
        SET @AsOfDate = COALESCE(@AsOfDate, CONVERT(date, SYSUTCDATETIME()));
        SELECT d.DealerId, d.DisplayName, d.IsActive, @AsOfDate AS AsOfDate,
            (SELECT COUNT(*) FROM training.Enrollments e WHERE e.DealerId = d.DealerId) AS EnrollmentCount,
            (SELECT COUNT(*) FROM training.Enrollments e WHERE e.DealerId = d.DealerId AND e.Status = N''Completed'') AS CompletedCount,
            (SELECT COUNT(*) FROM training.Enrollments e WHERE e.DealerId = d.DealerId AND e.Status = N''Failed'') AS FailedCount,
            (SELECT COUNT(*) FROM training.Enrollments e WHERE e.DealerId = d.DealerId AND e.Status IN (N''Booked'', N''InProgress'')) AS PendingCount,
            (SELECT COUNT(*) FROM training.Certifications cert
                JOIN training.Assessments a ON a.AssessmentId = cert.AssessmentId
                JOIN training.Enrollments e ON e.EnrollmentId = a.EnrollmentId
                WHERE e.DealerId = d.DealerId AND cert.IsRevoked = 0
                    AND cert.IssuedDate <= @AsOfDate AND cert.ExpiresDate >= @AsOfDate) AS ValidCertificateCount
        FROM training.DealerAccounts d WHERE d.DealerId = @DealerId;
    END;');

    EXEC(N'CREATE OR ALTER PROCEDURE training.usp_GetDealerTrainingStatus
        @DealerId nvarchar(4000), @AsOfDate date = NULL
    AS
    BEGIN
        SET NOCOUNT ON;
        IF @DealerId IS NULL OR LEN(LTRIM(RTRIM(@DealerId))) = 0 OR DATALENGTH(@DealerId) > 100
            OR @DealerId <> LTRIM(RTRIM(@DealerId)) OR @DealerId LIKE N''%[^A-Z0-9-]%''
            THROW 51010, ''Invalid DealerId. Use a dealer business key of up to 50 letters, digits or hyphens.'', 1;
        SET @AsOfDate = COALESCE(@AsOfDate, CONVERT(date, SYSUTCDATETIME()));
        SELECT d.DealerId, d.IsActive, c.CourseCode, c.Topic, cert.CertificateNumber,
            cert.ExpiresDate, @AsOfDate AS AsOfDate,
            CASE WHEN d.IsActive = 0 THEN N''InactiveDealer''
                WHEN cert.IsRevoked = 1 THEN N''Revoked''
                WHEN cert.IssuedDate > @AsOfDate THEN N''NotYetValid''
                WHEN cert.ExpiresDate < @AsOfDate THEN N''Expired''
                WHEN cert.CertificateNumber IS NOT NULL THEN N''Valid''
                ELSE N''NoCertificate'' END AS CertificationStatus
        FROM training.DealerAccounts d
        LEFT JOIN training.Enrollments e ON e.DealerId = d.DealerId
        LEFT JOIN training.Courses c ON c.CourseId = e.CourseId
        LEFT JOIN training.Assessments a ON a.EnrollmentId = e.EnrollmentId AND a.Passed = 1
        LEFT JOIN training.Certifications cert ON cert.AssessmentId = a.AssessmentId
        WHERE d.DealerId = @DealerId
        ORDER BY c.CourseCode, cert.CertificateNumber;
    END;');

    -- Reject inconsistent certification data rather than silently correcting user data.
    IF EXISTS (SELECT 1 FROM training.Certifications cert
        JOIN training.Assessments a ON a.AssessmentId = cert.AssessmentId
        JOIN training.Enrollments e ON e.EnrollmentId = a.EnrollmentId
        JOIN training.Courses c ON c.CourseId = e.CourseId
        WHERE a.Passed = 0 OR a.Score < c.PassMark OR e.Status <> N'Completed'
            OR cert.IssuedDate < a.AssessedDate)
        THROW 51011, 'Certification validation failed. No changes from this transaction were committed.', 1;
    IF EXISTS (SELECT 1 FROM sys.sql_expression_dependencies
        WHERE referencing_id IN (SELECT object_id FROM sys.objects WHERE schema_id = SCHEMA_ID(N'training'))
            AND (referenced_database_name IS NOT NULL OR referenced_server_name IS NOT NULL))
        THROW 51012, 'Cross-database dependencies are not permitted in the training schema.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

-- Validation output: fresh installation = 8 accounts, 3 providers, 6 courses,
-- 16 enrollments, 10 assessments, 7 certificates. Re-runs retain additional user records.
SELECT N'DealerAccounts' AS TableName, COUNT_BIG(*) AS RecordCount FROM training.DealerAccounts
UNION ALL SELECT N'Providers', COUNT_BIG(*) FROM training.Providers
UNION ALL SELECT N'Courses', COUNT_BIG(*) FROM training.Courses
UNION ALL SELECT N'Enrollments', COUNT_BIG(*) FROM training.Enrollments
UNION ALL SELECT N'Assessments', COUNT_BIG(*) FROM training.Assessments
UNION ALL SELECT N'Certifications', COUNT_BIG(*) FROM training.Certifications;

EXEC training.usp_GetDealerTrainingDetails @DealerId = N'D001';
EXEC training.usp_GetDealerTrainingSummary @DealerId = N'D001', @AsOfDate = '20260918';
EXEC training.usp_GetDealerTrainingStatus @DealerId = N'D002', @AsOfDate = '20260918';
EXEC training.usp_GetDealerTrainingStatus @DealerId = N'D003', @AsOfDate = '20260918';
EXEC training.usp_GetDealerTrainingStatus @DealerId = N'D005', @AsOfDate = '20260918';
EXEC training.usp_GetDealerTrainingDetails @DealerId = N'T2ONLY-001';
EXEC training.usp_GetDealerTrainingSummary @DealerId = N'T2ONLY-004', @AsOfDate = '20260918';
-- Empty result set on a fresh install: wrapper API should retain Tenant 1 data and report no match.
EXEC training.usp_GetDealerTrainingDetails @DealerId = N'D004';
SELECT * FROM training.vw_CourseReporting ORDER BY CourseCode;
PRINT N'Tenant2DemoDb setup completed. Tenant 1 was not accessed or changed.';
