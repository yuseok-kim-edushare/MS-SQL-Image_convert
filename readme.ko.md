# MS-SQL-Image_convert
[English](readme.md)
#### 이 문서는 AI번역본입니다. 오역이 있을 수 있습니다.
이 저장소는 MS-SQL Server에서 이미지를 내장 변환하기 위한 것입니다.
형식 변환이나 암호화/복호화 등을 수행합니다.

## 기능

- 이미지 바이트 스트림을 특정 형식 바이트 스트림으로 변환 및 암호화 등 (예: jpg, png)

## 요구사항

- Windows 10 20H2 이상 (Server 2022 이상)
  - .NET Framework 4.8.1
- ~~확인되지 않았지만 .NET 4.8과 Windows 7 이상에서도 작동할 것으로 예상됩니다~~
  - MS는 .NET 4.8에서 .NET 4.8.1로의 호환성 변경사항이 없다고 발표했습니다 (ARM Windows 지원 향상과 일부 접근성 기능 추가만)
    - ~~따라서 .NET 4.8과 Windows 7 이상에서도 작동할 것으로 예상됩니다~~
  - 그리고 저희 Server 2019와 .NET Framework 4.8, SQL Server 2022에서도 잘 작동합니다

## 라이브러리 빌드

먼저 .NET Framework 4.8.1 SDK를 설치해야 합니다.
또한 dotnet cli를 사용하고 싶다면 .NET 8+ SDK도 설치해야 합니다.

1. Visual Studio 2022+에서 솔루션을 엽니다
2. Release 모드로 솔루션을 빌드합니다
3. dotnet cli로 빌드하고 싶다면 (Visual Studio가 없는 경우)
   ```powershell
   dotnet build MS-SQL-Image_convert.csproj --configuration Release
   ```
4. **프로덕션 사용을 위해** 악의적인 복사 설치를 방지하기 위해 자체 키 파일 사용을 권장합니다.
    - 하지만 다행히도 github 릴리스 아티팩트 해시는 github 릴리스 페이지에서 쉽게 확인할 수 있습니다.
        - 따라서 dll을 다운로드하고 해시를 확인할 수 있습니다.
    - 그리고 `sn -k MS-SQL-Image_convert.snk`로 키 파일을 생성할 수 있습니다.
        - `sn`은 강력한 이름을 생성하고 관리하는 도구입니다. .NET Framework SDK에서 제공됩니다.
            - `C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8.1 Tools\`에서 찾을 수 있습니다.

## 사용 가능한 함수

### 1. ConvertToJpg
모든 이미지 형식을 사용자 정의 품질의 JPEG로 변환합니다.
```sql
dbo.ConvertToJpg(@imageData VARBINARY(MAX), @quality INT = 85) RETURNS VARBINARY(MAX)
```
- `@imageData`: 소스 이미지 바이너리 데이터
- `@quality`: JPEG 품질 (1-100, 기본값: 85)

### 2. ConvertToPng
모든 이미지 형식을 PNG로 변환합니다.
```sql
dbo.ConvertToPng(@imageData VARBINARY(MAX)) RETURNS VARBINARY(MAX)
```
- `@imageData`: 소스 이미지 바이너리 데이터

### 3. ResizeImage
지정된 크기로 이미지를 리사이즈하며 선택적으로 종횡비를 유지합니다.
```sql
dbo.ResizeImage(@imageData VARBINARY(MAX), @width INT, @height INT, @maintainAspectRatio BIT = 1) RETURNS VARBINARY(MAX)
```
- `@imageData`: 소스 이미지 바이너리 데이터
- `@width`: 픽셀 단위 목표 너비
- `@height`: 픽셀 단위 목표 높이
- `@maintainAspectRatio`: 종횡비 유지하려면 1, 늘리려면 0 (기본값: 1)

### 4. ReduceImageSize
압축을 적용하고 선택적으로 리사이징하여 이미지 파일 크기를 줄입니다.
**이 함수는 압축을 통해 크기를 줄이기 위해 이미지를 jpeg 형식으로 변환합니다.**
```sql
dbo.ReduceImageSize(@imageData VARBINARY(MAX), @maxSizeKB INT = 100, @jpegQuality INT = 85) RETURNS VARBINARY(MAX)
```
- `@imageData`: 소스 이미지 바이너리 데이터
- `@maxSizeKB`: KB 단위 최대 출력 크기 (기본값: 100)
- `@jpegQuality`: JPEG 압축 품질 (1-100, 기본값: 85)

### 5. EncryptImage
AES-256 GCM 암호화를 사용하여 이미지 데이터를 암호화합니다.
```sql
dbo.EncryptImage(@imageData VARBINARY(MAX), @password NVARCHAR(MAX)) RETURNS VARBINARY(MAX)
```
- `@imageData`: 암호화할 이미지
- `@password`: 암호화 비밀번호

### 6. DecryptImage
이전에 암호화된 이미지 데이터를 복호화합니다.
```sql
dbo.DecryptImage(@encryptedData VARBINARY(MAX), @password NVARCHAR(MAX)) RETURNS VARBINARY(MAX)
```
- `@encryptedData`: 암호화된 이미지 데이터
- `@password`: 복호화 비밀번호 (암호화 비밀번호와 일치해야 함)

### 7. GetImageInfo
이미지에 대한 자세한 정보를 반환합니다.
```sql
dbo.GetImageInfo(@imageData VARBINARY(MAX)) RETURNS NVARCHAR(MAX)
```
- `@imageData`: 분석할 이미지
- 반환값: 형식, 크기, 파일 크기, 해상도, 픽셀 형식

## 설치

1. DLL을 생성하기 위해 프로젝트를 빌드합니다
2. DLL을 SQL Server에 복사합니다
3. **데이터베이스에 배포**: 여러 배포 옵션은 [배포 가이드](README_deployment.md)를 참조하세요
4. CLR이 활성화되어 있는지 확인합니다

### 빠른 시작 배포

가장 쉬운 배포 경험을 위해 **단일 데이터베이스 스크립트**를 사용하세요:

1. `deploy_single_db.sql` 스크립트를 사용합니다
2. 배포하려는 각 데이터베이스에 대해 `@target_db` 변수를 편집합니다
3. 데이터베이스당 한 번씩 스크립트를 실행합니다

📖 **전체 배포 문서**: [README_deployment.ko.md](README_deployment.ko.md)

## 사용 예제

### PNG를 JPG로 변환
```sql
UPDATE MyImages
SET ImageData = dbo.ConvertToJpg(ImageData, 90)
WHERE ImageFormat = 'PNG';
```

### 썸네일 생성
```sql
SELECT 
    ImageId,
    dbo.ResizeImage(FullImage, 150, 150, 1) AS Thumbnail
FROM ProductImages;
```

### 민감한 이미지 암호화
```sql
UPDATE SensitiveDocuments
SET ImageData = dbo.EncryptImage(ImageData, 'StrongPassword123!');
```

### 저장 공간 크기 줄이기
```sql
UPDATE LargeImages
SET ImageData = dbo.ReduceImageSize(ImageData, 500, 80)
WHERE DATALENGTH(ImageData) > 1024 * 1024; -- 1MB보다 큰 이미지
```

## 보안 고려사항

- 어셈블리는 System.Drawing 사용으로 인해 UNSAFE 권한이 필요합니다
- 암호화 비밀번호를 평문으로 저장하지 말고 안전하게 저장하세요
- 비밀번호 저장을 위해 SQL Server의 내장 암호화 사용을 고려하세요
- 프로덕션 환경에서 사용하기 전에 비프로덕션 환경에서 철저히 테스트하세요

## 성능 팁

- 피크 시간 외에 이미지를 배치로 처리하세요
- 이미지 데이터를 위한 별도의 파일그룹 생성을 고려하세요
- 대용량 배치 작업 중 tempdb 사용량을 모니터링하세요
- 이미지를 포함하는 테이블에 적절한 인덱스를 사용하세요

## 문제 해결

- "Assembly not found" 오류가 발생하면 DLL 경로가 올바른지 확인하세요
- "Permission denied" 오류의 경우 TRUSTWORTHY 설정과 CLR 권한을 확인하세요
- 메모리 부족 오류의 경우 더 작은 배치로 이미지를 처리하세요
- 자세한 CLR 오류 메시지는 SQL Server 오류 로그를 확인하세요 