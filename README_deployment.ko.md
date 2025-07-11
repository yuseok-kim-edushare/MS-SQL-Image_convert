# MS-SQL-Image_convert 배포 가이드

[English](README_deployment.md)
#### 이 문서는 AI번역본입니다. 오역이 있을 수 있습니다.

이 가이드는 MS-SQL-Image_convert CLR 어셈블리를 여러 SQL Server 데이터베이스에 배포하기 위한 여러 옵션을 제공합니다.

## 사전 요구사항

1. CLR 통합이 활성화된 SQL Server
2. 컴파일되고 사용 가능한 MS-SQL-Image_convert.dll
3. SQL Server에 대한 관리자 권한
4. 사용 가능한 .NET Framework System.Drawing 어셈블리

## 배포 옵션

### 옵션 1: 단일 데이터베이스 스크립트 (권장)
**파일**: `deploy_single_db.sql`

가장 간단하고 안정적인 접근 방식입니다. `@target_db` 변수를 변경하여 데이터베이스당 한 번씩 스크립트를 실행합니다.

**사용 방법**:
1. `deploy_single_db.sql`을 엽니다
2. 상단의 구성 섹션을 편집합니다:
   ```sql
   DECLARE @target_db NVARCHAR(128) = N'vilacdb';  -- 이것을 변경하세요
   DECLARE @dll_path NVARCHAR(260) = N'C:\CLR\MS-SQL-Image_convert.dll';  -- 경로를 설정하세요
   ```
3. SQL Server Management Studio에서 스크립트를 실행합니다
4. `@target_db`를 변경하여 각 대상 데이터베이스에 대해 반복합니다

**장점**:
- 간단하고 안정적
- 문제 디버깅이 쉬움
- 모든 SQL 클라이언트에서 작동
- 외부 종속성 없음

### 옵션 2: PowerShell 자동화
**파일**: `deploy_multiple.ps1`

PowerShell을 사용하여 여러 데이터베이스에 자동 배포합니다.

**사전 요구사항**:
- PowerShell 5.0 이상
- SqlServer PowerShell 모듈: `Install-Module -Name SqlServer`

**사용 방법**:
```powershell
# 기본 설정으로 기본 사용법
.\deploy_multiple.ps1

# 사용자 정의 서버 및 데이터베이스
.\deploy_multiple.ps1 -ServerInstance "MyServer\Instance" -TargetDatabases @("db1", "db2", "db3")

# 사용자 정의 DLL 경로
.\deploy_multiple.ps1 -DllPath "D:\MyPath\MS-SQL-Image_convert.dll"
```

**장점**:
- 완전 자동화
- 병렬 실행 가능
- 상세한 오류 보고
- 전문적인 로깅

## 구성

### DLL 경로
SQL Server가 `MS-SQL-Image_convert.dll`에 접근할 수 있는지 확인하세요:
- 기본 위치: `C:\CLR\MS-SQL-Image_convert.dll`
- SQL Server 서비스 계정이 접근할 수 있어야 함
- 여러 서버의 경우 공유 네트워크 경로 사용 고려

### 대상 데이터베이스
각 스크립트의 데이터베이스 목록을 편집하세요:
- **단일 스크립트**: `@target_db` 변수 변경
- **PowerShell**: `$TargetDatabases` 배열 수정
- **배치**: `DATABASE1`, `DATABASE2` 등 변수 편집

### 서버 구성
스크립트는 자동으로 다음을 수행합니다:
- CLR 통합 활성화
- 신뢰할 수 있는 어셈블리 목록에 어셈블리 추가
- 필요한 어셈블리 및 함수 생성

## 생성되는 함수

각 배포는 다음 7개 함수를 생성합니다:

1. `dbo.ConvertToJpg` - 이미지를 JPEG 형식으로 변환
2. `dbo.ConvertToPng` - 이미지를 PNG 형식으로 변환
3. `dbo.ResizeImage` - 이미지를 특정 크기로 리사이즈
4. `dbo.ReduceImageSize` - 이미지 파일 크기 줄이기
5. `dbo.EncryptImage` - 이미지 데이터 암호화
6. `dbo.DecryptImage` - 이미지 데이터 복호화
7. `dbo.GetImageInfo` - 이미지 메타데이터 가져오기

## 문제 해결

### 일반적인 문제

1. **어셈블리 종속성 오류**:
   - 어셈블리를 삭제하기 전에 모든 종속 함수가 삭제되었는지 확인
   - 스크립트가 이를 자동으로 처리합니다

2. **권한 오류**:
   - SQL Server 관리자 권한으로 실행
   - 서비스 계정이 DLL 파일에 접근할 수 있는지 확인

3. **CLR이 활성화되지 않음**:
   - 스크립트가 자동으로 CLR 통합을 활성화합니다
   - 경우에 따라 서버 재시작이 필요할 수 있습니다

4. **신뢰 어셈블리 오류**:
   - 스크립트가 자동으로 어셈블리를 신뢰할 수 있는 목록에 추가합니다
   - DLL이 손상되지 않았는지 확인

### 검증

배포 후 성공을 확인하세요:
```sql
-- 어셈블리 확인
SELECT name, permission_set_desc 
FROM sys.assemblies 
WHERE name IN ('MS_SQL_Image_convert', 'System.Drawing');

-- 함수 확인
SELECT name, type_desc 
FROM sys.objects 
WHERE type = 'FN' 
AND name LIKE '%Image%' OR name LIKE '%Convert%';
```

## 적절한 옵션 선택

- **신규 사용자 / 간단한 배포**: 옵션 1 (단일 데이터베이스 스크립트) 사용
- **여러 데이터베이스 / 자동화**: 옵션 2 (PowerShell) 사용
- **기본 자동화 / PowerShell 없음**: 옵션 3 (배치 스크립트) 사용
- **복잡한 환경**: 필요에 맞게 옵션 2 사용자 정의

## 지원

문제가 발생하면:
1. SQL Server 오류 로그 확인
2. DLL 접근성 및 무결성 확인
3. 적절한 권한 확인
4. 먼저 단일 데이터베이스로 테스트 