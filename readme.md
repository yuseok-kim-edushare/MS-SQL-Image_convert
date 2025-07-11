# MS-SQL-Image_convert
This Repository is for convert image inplace in MS-SQL Server.
convert format or en/decrypt and so on.

## Features

- Convert Image byte stream to specific format byte stream (like jpg, png)

## Requirements

- Windows 10 20H2 or Later (server 2022 or later)
  - .NET Framework 4.8.1
- ~~Not Ensured, but it should work with .NET 4.8 and windows 7 or later~~
  - MS introduce no comapatibility change from .NET 4.8 to .NET 4.8.1 (just enhance support of ARM windows and some accessibility feature add)
    - ~~then, it should work with .NET 4.8 and windows 7 or later~~
  - And Our Server 2019 with .NET Framework 4.8 and SQL Server 2022 well works 

## Building the Library

first, you need to install .NET Framework 4.8.1 SDK.
also if you want to use dotnet cli, you need to install .NET 8+ SDK.

1. Open the solution in Visual Studio 2022+
2. Build the solution in Release mode
3. if you want to build with dotnet cli(cause of not having visual studio)
   ```powershell
   dotnet build MS-SQL-Image_convert.csproj --configuration Release
   ```
4. **For Production Use** I recommand to use your own key file. for avoid malicious copy install.
    - but, hopefully, github release artifact hash can easily see on github release page.
        - So, you can download the dll and check the hash.
    - And, you can create key file with `sn -k MS-SQL-Image_convert.snk`
        - `sn` is a tool for creating and managing strong names. provided by .NET Framework SDK.
            - you can find it in `C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8.1 Tools\`

## Functions Available

### 1. ConvertToJpg
Converts any image format to JPEG with customizable quality.
```sql
dbo.ConvertToJpg(@imageData VARBINARY(MAX), @quality INT = 85) RETURNS VARBINARY(MAX)
```
- `@imageData`: The source image binary data
- `@quality`: JPEG quality (1-100, default: 85)

### 2. ConvertToPng
Converts any image format to PNG.
```sql
dbo.ConvertToPng(@imageData VARBINARY(MAX)) RETURNS VARBINARY(MAX)
```
- `@imageData`: The source image binary data

### 3. ResizeImage
Resizes an image to specified dimensions with optional aspect ratio preservation.
```sql
dbo.ResizeImage(@imageData VARBINARY(MAX), @width INT, @height INT, @maintainAspectRatio BIT = 1) RETURNS VARBINARY(MAX)
```
- `@imageData`: The source image binary data
- `@width`: Target width in pixels
- `@height`: Target height in pixels
- `@maintainAspectRatio`: 1 to maintain aspect ratio, 0 to stretch (default: 1)

### 4. ReduceImageSize
Reduces image file size by applying compression and optionally resizing.
**This Function convert image to jpeg format to reduce size by compression.**
```sql
dbo.ReduceImageSize(@imageData VARBINARY(MAX), @maxSizeKB INT = 100, @jpegQuality INT = 85) RETURNS VARBINARY(MAX)
```
- `@imageData`: The source image binary data
- `@maxSizeKB`: Maximum output size in KB (default: 100)
- `@jpegQuality`: JPEG compression quality (1-100, default: 85)

### 5. EncryptImage
Encrypts image data using AES-256 GCM encryption.
```sql
dbo.EncryptImage(@imageData VARBINARY(MAX), @password NVARCHAR(MAX)) RETURNS VARBINARY(MAX)
```
- `@imageData`: The image to encrypt
- `@password`: Encryption password

### 6. DecryptImage
Decrypts previously encrypted image data.
```sql
dbo.DecryptImage(@encryptedData VARBINARY(MAX), @password NVARCHAR(MAX)) RETURNS VARBINARY(MAX)
```
- `@encryptedData`: The encrypted image data
- `@password`: Decryption password (must match encryption password)

### 7. GetImageInfo
Returns detailed information about an image.
```sql
dbo.GetImageInfo(@imageData VARBINARY(MAX)) RETURNS NVARCHAR(MAX)
```
- `@imageData`: The image to analyze
- Returns: Format, dimensions, size, resolution, and pixel format

## Installation

1. Build the project to generate the DLL
2. Copy the DLL to your SQL Server
3. **Deploy to databases**: See [Deployment Guide](README_deployment.md) for multiple deployment options
4. Ensure CLR is enabled

### Quick Start Deployment

For the easiest deployment experience, use the **Single Database Script**:

1. Use the `deploy_single_db.sql` script
2. Edit the `@target_db` variable for each database you want to deploy to
3. Run the script once per database

📖 **Full deployment documentation**: [README_deployment.md](README_deployment.md)

## Usage Examples

### Convert PNG to JPG
```sql
UPDATE MyImages
SET ImageData = dbo.ConvertToJpg(ImageData, 90)
WHERE ImageFormat = 'PNG';
```

### Create thumbnails
```sql
SELECT 
    ImageId,
    dbo.ResizeImage(FullImage, 150, 150, 1) AS Thumbnail
FROM ProductImages;
```

### Encrypt sensitive images
```sql
UPDATE SensitiveDocuments
SET ImageData = dbo.EncryptImage(ImageData, 'StrongPassword123!');
```

### Reduce storage size
```sql
UPDATE LargeImages
SET ImageData = dbo.ReduceImageSize(ImageData, 500, 80)
WHERE DATALENGTH(ImageData) > 1024 * 1024; -- Images larger than 1MB
```

## Security Considerations

- The assembly requires UNSAFE permission due to System.Drawing usage
- Store encryption passwords securely, not in plain text
- Consider using SQL Server's built-in encryption for password storage
- Test thoroughly in a non-production environment first

## Performance Tips

- Process images in batches during off-peak hours
- Consider creating a separate filegroup for image data
- Monitor tempdb usage during large batch operations
- Use appropriate indexes on tables containing images

## Troubleshooting

- If you get "Assembly not found" errors, ensure the DLL path is correct
- For "Permission denied" errors, check TRUSTWORTHY setting and CLR permissions
- For out of memory errors, process images in smaller batches
- Check SQL Server error log for detailed CLR error messages


