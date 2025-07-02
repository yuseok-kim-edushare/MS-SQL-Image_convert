-- SQL Server CLR Assembly Deployment Script - Single Database Version
-- MS-SQL-Image_convert
-- Run this script once per target database by changing the @target_db variable

-- =============================================
-- CONFIGURATION - CHANGE THESE VALUES
-- =============================================
DECLARE @target_db NVARCHAR(128) = N'db1';  -- <<<< CHANGE THIS FOR EACH DATABASE
DECLARE @dll_path NVARCHAR(260) = N'C:\CLR\MS-SQL-Image_convert.dll';  -- <<<< SET YOUR PATH HERE
DECLARE @system_drawing_path NVARCHAR(260) = N'C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Drawing\v4.0_4.0.0.0__b03f5f7f11d50a3a\System.Drawing.dll';

-- =============================================
-- Enable CLR (run once per instance)
-- =============================================
EXEC sp_configure 'show advanced options', 1;
RECONFIGURE;
EXEC sp_configure 'clr enabled', 1;
RECONFIGURE;

-- =============================================
-- Trust assemblies (run once per instance)
-- =============================================
USE [master];

-- Trust MS-SQL-Image_convert assembly
DECLARE @hash VARBINARY(64);
SELECT @hash = HASHBYTES('SHA2_512', BulkColumn)
FROM OPENROWSET(BULK 'C:\CLR\MS-SQL-Image_convert.dll', SINGLE_BLOB) AS x;

IF NOT EXISTS (SELECT * FROM sys.trusted_assemblies WHERE [hash] = @hash)
BEGIN
    EXEC sys.sp_add_trusted_assembly @hash = @hash, @description = N'MS-SQL-Image_convert Assembly';
    PRINT 'MS-SQL-Image_convert assembly hash added to trusted assemblies.';
END
ELSE
BEGIN
    PRINT 'MS-SQL-Image_convert assembly hash already exists in trusted assemblies.';
END

-- Trust System.Drawing assembly
SELECT @hash = HASHBYTES('SHA2_512', BulkColumn)
FROM OPENROWSET(BULK 'C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Drawing\v4.0_4.0.0.0__b03f5f7f11d50a3a\System.Drawing.dll', SINGLE_BLOB) AS x;

IF NOT EXISTS (SELECT * FROM sys.trusted_assemblies WHERE [hash] = @hash)
BEGIN
    EXEC sys.sp_add_trusted_assembly @hash = @hash, @description = N'System.Drawing Assembly';
    PRINT 'System.Drawing assembly hash added to trusted assemblies.';
END
ELSE
BEGIN
    PRINT 'System.Drawing assembly hash already exists in trusted assemblies.';
END

-- =============================================
-- Switch to target database
-- =============================================
DECLARE @sql NVARCHAR(MAX) = N'USE ' + QUOTENAME(@target_db);
EXEC(@sql);

PRINT 'Deploying to database: ' + @target_db;

-- =============================================
-- Clean up existing objects
-- =============================================

-- Drop existing functions
IF OBJECT_ID('dbo.ConvertToJpg', 'FN') IS NOT NULL DROP FUNCTION dbo.ConvertToJpg;
IF OBJECT_ID('dbo.ConvertToPng', 'FN') IS NOT NULL DROP FUNCTION dbo.ConvertToPng;
IF OBJECT_ID('dbo.ResizeImage', 'FN') IS NOT NULL DROP FUNCTION dbo.ResizeImage;
IF OBJECT_ID('dbo.ReduceImageSize', 'FN') IS NOT NULL DROP FUNCTION dbo.ReduceImageSize;
IF OBJECT_ID('dbo.EncryptImage', 'FN') IS NOT NULL DROP FUNCTION dbo.EncryptImage;
IF OBJECT_ID('dbo.DecryptImage', 'FN') IS NOT NULL DROP FUNCTION dbo.DecryptImage;
IF OBJECT_ID('dbo.GetImageInfo', 'FN') IS NOT NULL DROP FUNCTION dbo.GetImageInfo;

PRINT 'Dropped existing functions';

-- Drop existing assemblies
IF EXISTS (SELECT * FROM sys.assemblies WHERE name = 'MS_SQL_Image_convert') 
    DROP ASSEMBLY [MS_SQL_Image_convert];
IF EXISTS (SELECT * FROM sys.assemblies WHERE name = 'MS-SQL-Image_convert') 
    DROP ASSEMBLY [MS-SQL-Image_convert];
IF EXISTS (SELECT * FROM sys.assemblies WHERE name = 'System.Drawing') 
    DROP ASSEMBLY [System.Drawing];

PRINT 'Dropped existing assemblies';

-- =============================================
-- Create assemblies
-- =============================================

-- Create System.Drawing assembly
SET @sql = N'CREATE ASSEMBLY [System.Drawing] 
FROM ''' + @system_drawing_path + ''' 
WITH PERMISSION_SET = UNSAFE';
EXEC(@sql);
PRINT 'Created System.Drawing assembly';

-- Create MS_SQL_Image_convert assembly
SET @sql = N'CREATE ASSEMBLY [MS_SQL_Image_convert] 
FROM ''' + @dll_path + ''' 
WITH PERMISSION_SET = UNSAFE';
EXEC(@sql);
PRINT 'Created MS_SQL_Image_convert assembly';

-- =============================================
-- Create functions
-- =============================================

PRINT 'Creating functions...';
GO

-- ConvertToJpg
CREATE FUNCTION dbo.ConvertToJpg 
(
    @imageData VARBINARY(MAX), 
    @quality INT = 85
) 
RETURNS VARBINARY(MAX) 
AS EXTERNAL NAME MS_SQL_Image_convert.[MS_SQL_Image_convert.ImageFunctions].ConvertToJpg;
GO

PRINT 'ConvertToJpg created';

-- ConvertToPng
CREATE FUNCTION dbo.ConvertToPng 
(
    @imageData VARBINARY(MAX)
) 
RETURNS VARBINARY(MAX) 
AS EXTERNAL NAME MS_SQL_Image_convert.[MS_SQL_Image_convert.ImageFunctions].ConvertToPng;
GO

PRINT 'ConvertToPng created';

-- ResizeImage
CREATE FUNCTION dbo.ResizeImage 
(
    @imageData VARBINARY(MAX), 
    @width INT, 
    @height INT, 
    @maintainAspectRatio BIT = 1
) 
RETURNS VARBINARY(MAX) 
AS EXTERNAL NAME MS_SQL_Image_convert.[MS_SQL_Image_convert.ImageFunctions].ResizeImage;
GO

PRINT 'ResizeImage created';

-- ReduceImageSize
CREATE FUNCTION dbo.ReduceImageSize 
(
    @imageData VARBINARY(MAX), 
    @maxSizeKB INT = 100, 
    @jpegQuality INT = 85
) 
RETURNS VARBINARY(MAX) 
AS EXTERNAL NAME MS_SQL_Image_convert.[MS_SQL_Image_convert.ImageFunctions].ReduceImageSize;
GO

PRINT 'ReduceImageSize created';

-- EncryptImage
CREATE FUNCTION dbo.EncryptImage 
(
    @imageData VARBINARY(MAX), 
    @password NVARCHAR(MAX)
) 
RETURNS VARBINARY(MAX) 
AS EXTERNAL NAME MS_SQL_Image_convert.[MS_SQL_Image_convert.ImageFunctions].EncryptImage;
GO

PRINT 'EncryptImage created';

-- DecryptImage
CREATE FUNCTION dbo.DecryptImage 
(
    @encryptedData VARBINARY(MAX), 
    @password NVARCHAR(MAX)
) 
RETURNS VARBINARY(MAX) 
AS EXTERNAL NAME MS_SQL_Image_convert.[MS_SQL_Image_convert.ImageFunctions].DecryptImage;
GO

PRINT 'DecryptImage created';

-- GetImageInfo
CREATE FUNCTION dbo.GetImageInfo 
(
    @imageData VARBINARY(MAX)
) 
RETURNS NVARCHAR(MAX) 
AS EXTERNAL NAME MS_SQL_Image_convert.[MS_SQL_Image_convert.ImageFunctions].GetImageInfo;
GO

PRINT 'GetImageInfo created';

-- =============================================
-- Verify deployment
-- =============================================
DECLARE @func_count INT;
SELECT @func_count = COUNT(*) 
FROM sys.objects 
WHERE type = 'FN' 
AND name IN ('ConvertToJpg', 'ConvertToPng', 'ResizeImage', 'ReduceImageSize', 
             'EncryptImage', 'DecryptImage', 'GetImageInfo');

IF @func_count = 7
    PRINT 'SUCCESS: All 7 functions deployed successfully to database: ' + DB_NAME();
ELSE
    PRINT 'ERROR: Only ' + CAST(@func_count AS NVARCHAR(10)) + ' of 7 functions were created in database: ' + DB_NAME();

PRINT 'Deployment completed for database: ' + DB_NAME(); 