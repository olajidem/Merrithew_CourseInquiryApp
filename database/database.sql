-- database.sql
-- Creates the CourseInquiryDb database and its full schema in one script.
-- Matches the EF Core migrations up to 20261108182152_AddCrmSyncAttempts.
-- Safe to run more than once: every step checks before creating anything.
--
-- Alternative to EF Core: `dotnet ef database update` creates the same database
-- and schema. Use either this script OR migrations. This script records the
-- migrations in EF's history table, so running `dotnet ef database update`
-- afterwards is a no-op.
--
-- Adding a migration later: add its schema change below, and add its ID to the
-- migration history list at the end of the file.

-------------------------------------------------------------------------------
-- Database
-------------------------------------------------------------------------------
IF DB_ID(N'CourseInquiryDb') IS NULL
BEGIN
    CREATE DATABASE [CourseInquiryDb];
    PRINT 'Database CourseInquiryDb created.';
END
ELSE
BEGIN
    PRINT 'Database CourseInquiryDb already exists - skipped.';
END
GO

USE [CourseInquiryDb];
GO

-------------------------------------------------------------------------------
-- Inquiries
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.Inquiries', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Inquiries
    (
        Id                INT            IDENTITY(1, 1) NOT NULL,
        FirstName         NVARCHAR(100)  NOT NULL,
        LastName          NVARCHAR(100)  NOT NULL,
        Email             NVARCHAR(255)  NOT NULL,
        Phone             NVARCHAR(20)   NULL,
        CourseName        NVARCHAR(150)  NOT NULL,
        PreferredLocation NVARCHAR(150)  NULL,
        Message           NVARCHAR(2000) NULL,
        Status            NVARCHAR(50)   NOT NULL,   -- New | Contacted | Pending | Registered | Closed
        CreatedDate       DATETIME2      NOT NULL,   -- UTC
        UpdatedDate       DATETIME2      NULL,       -- UTC

        CONSTRAINT PK_Inquiries PRIMARY KEY (Id)
    );

    PRINT 'Table dbo.Inquiries created.';
END
ELSE
BEGIN
    PRINT 'Table dbo.Inquiries already exists - skipped.';
END
GO

-- Soft delete (migration AddInquiryAchivedDate). Kept as a separate step so it
-- also upgrades databases created before archiving existed.
IF COL_LENGTH('dbo.Inquiries', 'ArchivedDate') IS NULL
BEGIN
    ALTER TABLE dbo.Inquiries ADD ArchivedDate DATETIME2 NULL;   -- UTC; NULL = active
    PRINT 'Column dbo.Inquiries.ArchivedDate added.';
END
GO

-------------------------------------------------------------------------------
-- CrmSyncAttempts (audit trail of CRM sync attempts)
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.CrmSyncAttempts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CrmSyncAttempts
    (
        Id                 INT           IDENTITY(1, 1) NOT NULL,
        InquiryId          INT           NOT NULL,
        AttemptNumber      INT           NOT NULL,
        Succeeded          BIT           NOT NULL,
        IsTransientFailure BIT           NOT NULL,
        CrmReference       NVARCHAR(50)  NULL,
        ErrorMessage       NVARCHAR(500) NULL,       -- sanitised, never payload data
        DurationMs         INT           NOT NULL,
        AttemptedAt        DATETIME2     NOT NULL,   -- UTC

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
-- Marks every migration above as applied, so `dotnet ef database update`
-- doesn't try to create these tables or columns a second time.
-------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.__EFMigrationsHistory
    (
        MigrationId    NVARCHAR(150) NOT NULL,
        ProductVersion NVARCHAR(32)  NOT NULL,
        CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY (MigrationId)
    );
END
GO

INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
SELECT m.MigrationId, N'10.0.14'
FROM (VALUES
        (N'20261107214043_InitialCreate'),
        (N'20261108171811_AddInquiryAchivedDate'),
        (N'20261108182152_AddCrmSyncAttempts')
     ) AS m (MigrationId)
WHERE NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory h WHERE h.MigrationId = m.MigrationId);
GO

PRINT 'CourseInquiryDb schema is up to date.';
GO
