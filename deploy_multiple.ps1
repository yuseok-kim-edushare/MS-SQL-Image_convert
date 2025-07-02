# PowerShell script to deploy MS-SQL-Image_convert to multiple databases
# This script runs the single database deployment script against multiple target databases

param(
    [string]$ServerInstance = "localhost",
    [string]$DllPath = "C:\CLR\MS-SQL-Image_convert.dll",
    [string[]]$TargetDatabases = @("master", "db1")
)

# Verify SQL Module is available
if (!(Get-Module -ListAvailable -Name SqlServer)) {
    Write-Error "SqlServer PowerShell module is not installed. Install it with: Install-Module -Name SqlServer"
    exit 1
}

Import-Module SqlServer

# SQL Script template
$sqlScript = @"
-- SQL Server CLR Assembly Deployment Script - Single Database Version
-- MS-SQL-Image_convert

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
FROM OPENROWSET(BULK '$DllPath', SINGLE_BLOB) AS x;

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
-- Switch to target database: {0}
-- =============================================
USE [{0}];

PRINT 'Deploying to database: {0}';

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
CREATE ASSEMBLY [System.Drawing] 
FROM 'C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Drawing\v4.0_4.0.0.0__b03f5f7f11d50a3a\System.Drawing.dll'
WITH PERMISSION_SET = UNSAFE;

PRINT 'Created System.Drawing assembly';

-- Create MS_SQL_Image_convert assembly
CREATE ASSEMBLY [MS_SQL_Image_convert] 
FROM '$DllPath'
WITH PERMISSION_SET = UNSAFE;

PRINT 'Created MS_SQL_Image_convert assembly';

-- =============================================
-- Create functions
-- =============================================

PRINT 'Creating functions...';

-- ConvertToJpg
CREATE FUNCTION dbo.ConvertToJpg 
(
    @imageData VARBINARY(MAX), 
    @quality INT = 85
) 
RETURNS VARBINARY(MAX) 
AS EXTERNAL NAME MS_SQL_Image_convert.[MS_SQL_Image_convert.ImageFunctions].ConvertToJpg;

PRINT 'ConvertToJpg created';

-- ConvertToPng
CREATE FUNCTION dbo.ConvertToPng 
(
    @imageData VARBINARY(MAX)
) 
RETURNS VARBINARY(MAX) 
AS EXTERNAL NAME MS_SQL_Image_convert.[MS_SQL_Image_convert.ImageFunctions].ConvertToPng;

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

PRINT 'ReduceImageSize created';

-- EncryptImage
CREATE FUNCTION dbo.EncryptImage 
(
    @imageData VARBINARY(MAX), 
    @password NVARCHAR(MAX)
) 
RETURNS VARBINARY(MAX) 
AS EXTERNAL NAME MS_SQL_Image_convert.[MS_SQL_Image_convert.ImageFunctions].EncryptImage;

PRINT 'EncryptImage created';

-- DecryptImage
CREATE FUNCTION dbo.DecryptImage 
(
    @encryptedData VARBINARY(MAX), 
    @password NVARCHAR(MAX)
) 
RETURNS VARBINARY(MAX) 
AS EXTERNAL NAME MS_SQL_Image_convert.[MS_SQL_Image_convert.ImageFunctions].DecryptImage;

PRINT 'DecryptImage created';

-- GetImageInfo
CREATE FUNCTION dbo.GetImageInfo 
(
    @imageData VARBINARY(MAX)
) 
RETURNS NVARCHAR(MAX) 
AS EXTERNAL NAME MS_SQL_Image_convert.[MS_SQL_Image_convert.ImageFunctions].GetImageInfo;

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
    PRINT 'SUCCESS: All 7 functions deployed successfully to database: {0}';
ELSE
    PRINT 'ERROR: Only ' + CAST(@func_count AS NVARCHAR(10)) + ' of 7 functions were created in database: {0}';

PRINT 'Deployment completed for database: {0}';
"@

# Verify DLL exists
if (!(Test-Path $DllPath)) {
    Write-Error "DLL file not found at: $DllPath"
    exit 1
}

Write-Host "Starting deployment to multiple databases..." -ForegroundColor Green
Write-Host "Server: $ServerInstance" -ForegroundColor Yellow
Write-Host "DLL Path: $DllPath" -ForegroundColor Yellow
Write-Host "Target Databases: $($TargetDatabases -join ', ')" -ForegroundColor Yellow
Write-Host ""

$successCount = 0
$errorCount = 0
$results = @()

foreach ($database in $TargetDatabases) {
    Write-Host "Deploying to database: $database" -ForegroundColor Cyan
    
    try {
        # Format the SQL script with the current database name
        $currentSql = $sqlScript -f $database
        
        # Execute the SQL script
        $result = Invoke-Sqlcmd -ServerInstance $ServerInstance -Database $database -Query $currentSql -Verbose -ErrorAction Stop
        
        Write-Host "✓ Successfully deployed to $database" -ForegroundColor Green
        $successCount++
        
        $results += [PSCustomObject]@{
            Database = $database
            Status = "Success"
            Message = "Deployment completed successfully"
        }
    }
    catch {
        Write-Host "✗ Failed to deploy to $database" -ForegroundColor Red
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
        $errorCount++
        
        $results += [PSCustomObject]@{
            Database = $database
            Status = "Failed"
            Message = $_.Exception.Message
        }
    }
    
    Write-Host ""
}

# Summary
Write-Host "=== DEPLOYMENT SUMMARY ===" -ForegroundColor Magenta
Write-Host "Total databases: $($TargetDatabases.Count)" -ForegroundColor White
Write-Host "Successful: $successCount" -ForegroundColor Green
Write-Host "Failed: $errorCount" -ForegroundColor Red
Write-Host ""

# Display detailed results
$results | Format-Table -AutoSize

if ($errorCount -eq 0) {
    Write-Host "All deployments completed successfully!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "Some deployments failed. Please check the errors above." -ForegroundColor Red
    exit 1
} 