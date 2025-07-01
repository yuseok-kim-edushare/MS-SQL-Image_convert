# MS-SQL-Image_convert
This Repository is for convert image inplace in MS-SQL Server.
convert format or en/decrypt and so on.

## Features

- Convert Image byte stream to specific format byte stream (like jpg, png)

## Requirements

- Windows 10 20H2 or Later (server 2022 or later)
  - .NET Framework 4.8.1
- Not Ensured, but it should work with .NET 4.8 and windows 7 or later
  - MS introduce no comapatibility change from .NET 4.8 to .NET 4.8.1
    - then, it should work with .NET 4.8 and windows 7 or later

## Building the Library

first, you need to install .NET Framework 4.8.1 SDK.
also if you want to use dotnet cli, you need to install .NET 8+ SDK.

1. Open the solution in Visual Studio 2022+
2. Build the solution in Release mode
3. if you want to build with dotnet cli(cause of not having visual studio)
   ```powershell
   dotnet build MS-SQL-Image_convert.csproj --configuration Release
   ```


