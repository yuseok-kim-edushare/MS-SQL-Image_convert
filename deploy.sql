-- SQL Server CLR Assembly Deployment Script
-- MS-SQL-Image_convert

-- Enable CLR integration if not already enabled
EXEC sp_configure 'clr enabled', 1;
RECONFIGURE;
GO

-- Set database to TRUSTWORTHY (required for EXTERNAL_ACCESS)
-- Replace [YourDatabase] with your actual database name
ALTER DATABASE [YourDatabase] SET TRUSTWORTHY ON;
GO

-- Drop existing assembly and functions if they exist
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ConvertToJpg]') AND type = N'FS')
    DROP FUNCTION [dbo].[ConvertToJpg];
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ConvertToPng]') AND type = N'FS')
    DROP FUNCTION [dbo].[ConvertToPng];
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ResizeImage]') AND type = N'FS')
    DROP FUNCTION [dbo].[ResizeImage];
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ReduceImageSize]') AND type = N'FS')
    DROP FUNCTION [dbo].[ReduceImageSize];
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EncryptImage]') AND type = N'FS')
    DROP FUNCTION [dbo].[EncryptImage];
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DecryptImage]') AND type = N'FS')
    DROP FUNCTION [dbo].[DecryptImage];
GO

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GetImageInfo]') AND type = N'FS')
    DROP FUNCTION [dbo].[GetImageInfo];
GO

IF EXISTS (SELECT * FROM sys.assemblies WHERE name = N'MS_SQL_Image_convert')
    DROP ASSEMBLY [MS_SQL_Image_convert];
GO

-- Create the assembly
-- Replace the path with the actual path to your DLL
CREATE ASSEMBLY [MS_SQL_Image_convert]
FROM 'C:\Path\To\Your\MS-SQL-Image_convert.dll'
WITH PERMISSION_SET = UNSAFE; -- Required for System.Drawing
GO

-- Create wrapper functions

-- Convert to JPG with quality parameter
CREATE FUNCTION [dbo].[ConvertToJpg]
(
    @imageData VARBINARY(MAX),
    @quality INT = 85
)
RETURNS VARBINARY(MAX)
AS EXTERNAL NAME [MS_SQL_Image_convert].[MS_SQL_Image_convert.ImageFunctions].[ConvertToJpg];
GO

-- Convert to PNG
CREATE FUNCTION [dbo].[ConvertToPng]
(
    @imageData VARBINARY(MAX)
)
RETURNS VARBINARY(MAX)
AS EXTERNAL NAME [MS_SQL_Image_convert].[MS_SQL_Image_convert.ImageFunctions].[ConvertToPng];
GO

-- Resize image
CREATE FUNCTION [dbo].[ResizeImage]
(
    @imageData VARBINARY(MAX),
    @width INT,
    @height INT,
    @maintainAspectRatio BIT = 1
)
RETURNS VARBINARY(MAX)
AS EXTERNAL NAME [MS_SQL_Image_convert].[MS_SQL_Image_convert.ImageFunctions].[ResizeImage];
GO

-- Reduce image size
CREATE FUNCTION [dbo].[ReduceImageSize]
(
    @imageData VARBINARY(MAX),
    @maxSizeKB INT = 100,
    @jpegQuality INT = 85
)
RETURNS VARBINARY(MAX)
AS EXTERNAL NAME [MS_SQL_Image_convert].[MS_SQL_Image_convert.ImageFunctions].[ReduceImageSize];
GO

-- Encrypt image
CREATE FUNCTION [dbo].[EncryptImage]
(
    @imageData VARBINARY(MAX),
    @password NVARCHAR(MAX)
)
RETURNS VARBINARY(MAX)
AS EXTERNAL NAME [MS_SQL_Image_convert].[MS_SQL_Image_convert.ImageFunctions].[EncryptImage];
GO

-- Decrypt image
CREATE FUNCTION [dbo].[DecryptImage]
(
    @encryptedData VARBINARY(MAX),
    @password NVARCHAR(MAX)
)
RETURNS VARBINARY(MAX)
AS EXTERNAL NAME [MS_SQL_Image_convert].[MS_SQL_Image_convert.ImageFunctions].[DecryptImage];
GO

-- Get image info
CREATE FUNCTION [dbo].[GetImageInfo]
(
    @imageData VARBINARY(MAX)
)
RETURNS NVARCHAR(MAX)
AS EXTERNAL NAME [MS_SQL_Image_convert].[MS_SQL_Image_convert.ImageFunctions].[GetImageInfo];
GO

PRINT 'MS-SQL-Image_convert assembly and functions deployed successfully!'; 