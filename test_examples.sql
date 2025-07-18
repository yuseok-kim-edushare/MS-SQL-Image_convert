-- MS-SQL-Image_convert Test Examples
-- This script demonstrates how to use the image conversion functions

-- Create a test table for demonstration
IF OBJECT_ID('dbo.ImageTest', 'U') IS NOT NULL
    DROP TABLE dbo.ImageTest;
GO

CREATE TABLE dbo.ImageTest
(
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ImageName NVARCHAR(100),
    OriginalImage VARBINARY(MAX),
    ConvertedImage VARBINARY(MAX),
    EncryptedImage VARBINARY(MAX),
    ImageInfo NVARCHAR(MAX)
);
GO

-- Example 1: Load an image from file and convert to JPG
-- Note: You'll need to enable xp_cmdshell or use OPENROWSET to load files
-- This is just an example of how to use the functions

-- Insert a test image (you would normally load this from a file)
DECLARE @TestImage VARBINARY(MAX);
-- In production, you would load the image like this:
-- SELECT @TestImage = BulkColumn FROM OPENROWSET(BULK 'C:\Path\To\Image.png', SINGLE_BLOB) AS Image;

-- For testing, let's assume we have an image in @TestImage variable

-- Example 2: Convert image to JPG with 90% quality
DECLARE @JpgImage VARBINARY(MAX);
SET @JpgImage = dbo.ConvertToJpg(@TestImage, 90);

-- Example 3: Convert image to PNG
DECLARE @PngImage VARBINARY(MAX);
SET @PngImage = dbo.ConvertToPng(@TestImage);

-- Example 4: Resize image to 800x600 maintaining aspect ratio
DECLARE @ResizedImage VARBINARY(MAX);
SET @ResizedImage = dbo.ResizeImage(@TestImage, 800, 600, 1);

-- Example 5: Reduce image size to max 50KB
DECLARE @ReducedImage VARBINARY(MAX);
SET @ReducedImage = dbo.ReduceImageSize(@TestImage, 50, 75);

-- Example 6: Encrypt and decrypt image
DECLARE @Password NVARCHAR(100) = 'MySecretPassword123!';
DECLARE @EncryptedImg VARBINARY(MAX);
DECLARE @DecryptedImg VARBINARY(MAX);

SET @EncryptedImg = dbo.EncryptImage(@TestImage, @Password);
SET @DecryptedImg = dbo.DecryptImage(@EncryptedImg, @Password);

-- Example 7: Get image information
DECLARE @ImageInfo NVARCHAR(MAX);
SET @ImageInfo = dbo.GetImageInfo(@TestImage);
PRINT @ImageInfo;

-- Example 8: Batch processing multiple images in a table
-- Assuming you have a table with images
/*
UPDATE YourImageTable
SET ConvertedImage = dbo.ConvertToJpg(OriginalImage, 85)
WHERE ImageFormat = 'PNG';
*/

-- Example 9: Convert and resize in one operation
/*
UPDATE YourImageTable
SET ProcessedImage = dbo.ConvertToJpg(
    dbo.ResizeImage(OriginalImage, 1024, 768, 1), 
    90
)
WHERE NeedsProcessing = 1;
*/

-- Example 10: Encrypt sensitive images (OPTIMIZED with cached keys for better performance)
/*
-- Step 1: Derive key once for the batch
DECLARE @EncryptionKey NVARCHAR(MAX) = dbo.DeriveKeyFromPassword('YourStrongPassword');

-- Step 2: Use cached key for batch encryption (much faster)
UPDATE SensitiveImages
SET EncryptedData = dbo.EncryptImageWithCachedKey(ImageData, @EncryptionKey),
    ImageData = NULL -- Clear original
WHERE RequiresEncryption = 1;
*/

-- Example 10b: Traditional single-image encryption (for comparison)
/*
UPDATE SensitiveImages
SET EncryptedData = dbo.EncryptImage(ImageData, 'YourStrongPassword'),
    ImageData = NULL -- Clear original
WHERE RequiresEncryption = 1;
*/

-- Example 11: Working with image columns in SELECT statements
/*
SELECT 
    Id,
    ImageName,
    dbo.GetImageInfo(OriginalImage) AS ImageDetails,
    DATALENGTH(OriginalImage) AS OriginalSize,
    DATALENGTH(dbo.ReduceImageSize(OriginalImage, 100, 80)) AS ReducedSize
FROM YourImageTable;
*/

-- Example 12: Create a view with processed images
/*
CREATE VIEW ProcessedImagesView AS
SELECT 
    Id,
    ImageName,
    dbo.ConvertToJpg(OriginalImage, 85) AS JpgImage,
    dbo.ResizeImage(OriginalImage, 150, 150, 1) AS ThumbnailImage
FROM YourImageTable;
*/

-- Example 13: Error handling
BEGIN TRY
    DECLARE @InvalidImage VARBINARY(MAX) = 0x0123456789; -- Invalid image data
    DECLARE @Result VARBINARY(MAX);
    SET @Result = dbo.ConvertToJpg(@InvalidImage, 85);
END TRY
BEGIN CATCH
    PRINT 'Error: ' + ERROR_MESSAGE();
END CATCH;

-- Cleanup
-- DROP TABLE dbo.ImageTest;

-- ============================================================================
-- NEW: Performance Optimized Functions (Added in latest version)
-- ============================================================================

-- Example 14: Using cached keys for batch operations (PERFORMANCE OPTIMIZED)
DECLARE @BatchPassword NVARCHAR(100) = 'BatchOptimizedPassword2024!';
DECLARE @SampleData VARBINARY(MAX) = CAST(REPLICATE('SampleData', 100) AS VARBINARY(MAX));

-- Step 1: Derive key once for the entire batch (new optimized approach)
DECLARE @CachedKey NVARCHAR(MAX) = dbo.DeriveKeyFromPassword(@BatchPassword);

-- Step 2: Use cached key for multiple operations (much faster than traditional method)
DECLARE @OptimizedEncrypted VARBINARY(MAX) = dbo.EncryptImageWithCachedKey(@SampleData, @CachedKey);
DECLARE @OptimizedDecrypted VARBINARY(MAX) = dbo.DecryptImageWithCachedKey(@OptimizedEncrypted, @CachedKey);

-- Verify the optimized method works correctly
IF @SampleData = @OptimizedDecrypted
    PRINT 'SUCCESS: Optimized cached key encryption/decryption works correctly';
ELSE
    PRINT 'ERROR: Optimized method failed';

-- Example 15: Performance comparison demonstration
DECLARE @TestData VARBINARY(MAX) = CAST(REPLICATE('PerformanceTest', 50) AS VARBINARY(MAX));
DECLARE @TestPassword NVARCHAR(100) = 'PerformanceTestPassword123!';

-- Traditional method timing
DECLARE @StartTime DATETIME2 = SYSDATETIME();
DECLARE @TraditionalResult VARBINARY(MAX);

-- Simulate multiple operations with traditional method
DECLARE @Counter INT = 1;
WHILE @Counter <= 5
BEGIN
    SET @TraditionalResult = dbo.EncryptImage(@TestData, @TestPassword);
    SET @TraditionalResult = dbo.DecryptImage(@TraditionalResult, @TestPassword);
    SET @Counter = @Counter + 1;
END

DECLARE @TraditionalTime BIGINT = DATEDIFF(MICROSECOND, @StartTime, SYSDATETIME());

-- Optimized method timing
SET @StartTime = SYSDATETIME();
DECLARE @OptimizedTestKey NVARCHAR(MAX) = dbo.DeriveKeyFromPassword(@TestPassword);
DECLARE @OptimizedResult VARBINARY(MAX);

-- Simulate multiple operations with optimized method
SET @Counter = 1;
WHILE @Counter <= 5
BEGIN
    SET @OptimizedResult = dbo.EncryptImageWithCachedKey(@TestData, @OptimizedTestKey);
    SET @OptimizedResult = dbo.DecryptImageWithCachedKey(@OptimizedResult, @OptimizedTestKey);
    SET @Counter = @Counter + 1;
END

DECLARE @OptimizedTime BIGINT = DATEDIFF(MICROSECOND, @StartTime, SYSDATETIME());

-- Show performance comparison
PRINT 'Performance Comparison (5 encrypt/decrypt cycles):';
PRINT 'Traditional method: ' + CAST(@TraditionalTime AS NVARCHAR(20)) + ' microseconds';
PRINT 'Optimized method: ' + CAST(@OptimizedTime AS NVARCHAR(20)) + ' microseconds';

IF @TraditionalTime > 0
BEGIN
    DECLARE @ImprovementPct DECIMAL(10,2) = 
        (CAST(@TraditionalTime - @OptimizedTime AS DECIMAL(10,2)) / @TraditionalTime) * 100;
    PRINT 'Performance improvement: ' + CAST(@ImprovementPct AS NVARCHAR(20)) + '%';
END

-- Example 16: Best practices for batch processing
/*
-- RECOMMENDED: For batch operations with same password
DECLARE @BatchKey NVARCHAR(MAX) = dbo.DeriveKeyFromPassword('YourPassword');

UPDATE YourImageTable
SET EncryptedColumn = dbo.EncryptImageWithCachedKey(ImageColumn, @BatchKey)
WHERE BatchId = @SomeBatch;

-- NOT RECOMMENDED: For batch operations (slower)
UPDATE YourImageTable  
SET EncryptedColumn = dbo.EncryptImage(ImageColumn, 'YourPassword')
WHERE BatchId = @SomeBatch;

-- RECOMMENDED: For single operations (either method is fine)
DECLARE @SingleEncrypted VARBINARY(MAX) = dbo.EncryptImage(@SingleImage, 'Password');
-- OR
DECLARE @SingleKey NVARCHAR(MAX) = dbo.DeriveKeyFromPassword('Password');
DECLARE @SingleEncrypted2 VARBINARY(MAX) = dbo.EncryptImageWithCachedKey(@SingleImage, @SingleKey);
*/ 