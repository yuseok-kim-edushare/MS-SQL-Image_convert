# MS-SQL-Image_convert Deployment Guide

This guide provides multiple options for deploying the MS-SQL-Image_convert CLR assembly to multiple SQL Server databases.

## Prerequisites

1. SQL Server with CLR integration enabled
2. MS-SQL-Image_convert.dll compiled and available
3. Administrative privileges on SQL Server
4. .NET Framework System.Drawing assembly available

## Deployment Options

### Option 1: Single Database Script (Recommended)
**File**: `deploy_single_db.sql`

This is the simplest and most reliable approach. Run the script once per database by changing the `@target_db` variable.

**How to use**:
1. Open `deploy_single_db.sql`
2. Edit the configuration section at the top:
   ```sql
   DECLARE @target_db NVARCHAR(128) = N'vilacdb';  -- Change this
   DECLARE @dll_path NVARCHAR(260) = N'C:\CLR\MS-SQL-Image_convert.dll';  -- Set your path
   ```
3. Run the script in SQL Server Management Studio
4. Repeat for each target database by changing `@target_db`

**Advantages**:
- Simple and reliable
- Easy to debug issues
- Works with any SQL client
- No external dependencies

### Option 2: PowerShell Automation
**File**: `deploy_multiple.ps1`

Automates deployment to multiple databases using PowerShell.

**Prerequisites**:
- PowerShell 5.0 or later
- SqlServer PowerShell module: `Install-Module -Name SqlServer`

**How to use**:
```powershell
# Basic usage with default settings
.\deploy_multiple.ps1

# Custom server and databases
.\deploy_multiple.ps1 -ServerInstance "MyServer\Instance" -TargetDatabases @("db1", "db2", "db3")

# Custom DLL path
.\deploy_multiple.ps1 -DllPath "D:\MyPath\MS-SQL-Image_convert.dll"
```

**Advantages**:
- Fully automated
- Parallel execution possible
- Detailed error reporting
- Professional logging

## Configuration

### DLL Path
Ensure your `MS-SQL-Image_convert.dll` is accessible to SQL Server:
- Default location: `C:\CLR\MS-SQL-Image_convert.dll`
- Must be accessible by SQL Server service account
- Consider using a shared network path for multiple servers

### Target Databases
Edit the database lists in each script:
- **Single script**: Change `@target_db` variable
- **PowerShell**: Modify `$TargetDatabases` array
- **Batch**: Edit `DATABASE1`, `DATABASE2`, etc. variables

### Server Configuration
The scripts will automatically:
- Enable CLR integration
- Add assemblies to trusted assemblies list
- Create required assemblies and functions

## Functions Created

Each deployment creates these 7 functions:

1. `dbo.ConvertToJpg` - Convert images to JPEG format
2. `dbo.ConvertToPng` - Convert images to PNG format  
3. `dbo.ResizeImage` - Resize images to specific dimensions
4. `dbo.ReduceImageSize` - Reduce image file size
5. `dbo.EncryptImage` - Encrypt image data
6. `dbo.DecryptImage` - Decrypt image data
7. `dbo.GetImageInfo` - Get image metadata

## Troubleshooting

### Common Issues

1. **Assembly dependency errors**:
   - Ensure all dependent functions are dropped before dropping assemblies
   - The scripts handle this automatically

2. **Permission errors**:
   - Run with SQL Server administrator privileges
   - Ensure service account can access DLL file

3. **CLR not enabled**:
   - Scripts automatically enable CLR integration
   - May require server restart in some cases

4. **Trust assembly errors**:
   - Scripts automatically add assemblies to trusted list
   - Verify DLL is not corrupted

### Verification

After deployment, verify success:
```sql
-- Check assemblies
SELECT name, permission_set_desc 
FROM sys.assemblies 
WHERE name IN ('MS_SQL_Image_convert', 'System.Drawing');

-- Check functions
SELECT name, type_desc 
FROM sys.objects 
WHERE type = 'FN' 
AND name LIKE '%Image%' OR name LIKE '%Convert%';
```

## Choosing the Right Option

- **New users / Simple deployment**: Use Option 1 (Single Database Script)
- **Multiple databases / Automation**: Use Option 2 (PowerShell)
- **Basic automation / No PowerShell**: Use Option 3 (Batch Script)
- **Complex environments**: Customize Option 2 for your needs

## Support

If you encounter issues:
1. Check SQL Server error logs
2. Verify DLL accessibility and integrity
3. Ensure proper permissions
4. Test with a single database first 