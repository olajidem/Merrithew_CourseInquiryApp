-- 002_CreateTables.sql
-- Creates the schema. It matches the EF Core migration 20261107214043_InitialCreate.
-- Safe to run more than once.

USE [CourseInquiryDb];
GO

-------------------------------------------------------------------------------
-- Inquiries
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.Inquiries', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Inquiries
    (
        Id INT IDENTITY(1, 1) NOT NULL,
        FirstName NVARCHAR(100) NOT NULL,
        LastName NVARCHAR(100) NOT NULL,
        Email NVARCHAR(255) NOT NULL,
        Phone NVARCHAR(20) NULL,
        CourseName NVARCHAR(150) NOT NULL,
        PreferredLocation NVARCHAR(150) NULL,
        Message NVARCHAR(2000) NULL,
        Status NVARCHAR(50) NOT NULL,
        -- New | Contacted | Pending | Registered | Closed
        CreatedDate DATETIME2 NOT NULL,
        -- UTC
        UpdatedDate DATETIME2 NULL,
        -- UTC

        CONSTRAINT PK_Inquiries PRIMARY KEY (Id)
    );

    PRINT 'Table dbo.Inquiries created.';
END
ELSE
BEGIN
    PRINT 'Table dbo.Inquiries already exists - skipped.';
END
GO
IF COL_LENGTH('dbo.Inquiries', 'ArchivedDate') IS NULL
    ALTER TABLE dbo.Inquiries ADD ArchivedDate DATETIME2 NULL;   -- soft delete (UTC)
GO

-------------------------------------------------------------------------------
-- CrmSyncAttempts (audit trail of CRM sync attempts)
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.CrmSyncAttempts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CrmSyncAttempts
    (
        Id INT IDENTITY(1, 1) NOT NULL,
        InquiryId INT NOT NULL,
        AttemptNumber INT NOT NULL,
        Succeeded BIT NOT NULL,
        IsTransientFailure BIT NOT NULL,
        CrmReference NVARCHAR(50) NULL,
        ErrorMessage NVARCHAR(500) NULL,
        -- sanitised, never payload data
        DurationMs INT NOT NULL,
        AttemptedAt DATETIME2 NOT NULL,
        -- UTC

        CONSTRAINT PK_CrmSyncAttempts PRIMARY KEY (Id),
        CONSTRAINT FK_CrmSyncAttempts_Inquiries_InquiryId
            FOREIGN KEY (InquiryId) REFERENCES dbo.Inquiries (Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_CrmSyncAttempts_InquiryId ON dbo.CrmSyncAttempts (InquiryId);

    PRINT 'Table dbo.CrmSyncAttempts created.';
END
ELSE
BEGIN
    PRINT 'Table dbo.CrmSyncAttempts already exists - skipped.';
END
GO



-------------------------------------------------------------------------------
-- EF Core migration history
-- Marks InitialCreate as applied, so `dotnet ef database update` won't try to
-- create the Inquiries table a second time.
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.__EFMigrationsHistory
    (
        MigrationId NVARCHAR(150) NOT NULL,
        ProductVersion NVARCHAR(32) NOT NULL,
        CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY (MigrationId)
    );
END
GO

-- Record every migration this script covers, so `dotnet ef database update` skips them
INSERT INTO dbo.__EFMigrationsHistory
    (MigrationId, ProductVersion)
SELECT m.MigrationId, N'10.0.14'
FROM (VALUES
        (N'20261107214043_InitialCreate'),
        (N'20261108171811_AddInquiryAchivedDate'),
        (N'20261108182152_AddCrmSyncAttempts')
     ) AS m (MigrationId)
WHERE NOT EXISTS (SELECT 1
FROM dbo.__EFMigrationsHistory h
WHERE h.MigrationId = m.MigrationId);
GO

