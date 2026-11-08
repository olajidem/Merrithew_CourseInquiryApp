-- 001_CreateDatabase.sql
-- Creates the CourseInquiryDb database. Safe to run more than once.
--
-- Alternative to EF Core: `dotnet ef database update` creates the same database
-- and schema. Use either these scripts OR migrations to create the database.
-- (002 records the migration in EF's history table, so running
-- `dotnet ef database update` afterwards is a no-op.)

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
