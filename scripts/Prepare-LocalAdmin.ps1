param(
    [Parameter(Mandatory = $true)][string]$Email,
    [string]$FullName = 'Quản trị CLB'
)

$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $PSScriptRoot '..\src\StudentClubManagement.Web\StudentClubManagement.Web.csproj'
[xml]$projectXml = Get-Content -LiteralPath $projectPath
$secretId = [string]$projectXml.Project.PropertyGroup.UserSecretsId
$secretPath = Join-Path $env:APPDATA "Microsoft\UserSecrets\$secretId\secrets.json"
if (Test-Path -LiteralPath $secretPath) {
    $existing = Get-Content -LiteralPath $secretPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($existing.PSObject.Properties['BootstrapAdmin:Password'] -or
        ($existing.BootstrapAdmin -and $existing.BootstrapAdmin.PSObject.Properties['Password'])) {
        throw 'Đã có cấu hình quản trị cục bộ. Dùng Show-LocalAdminLogin.ps1 để xem; không tự ghi đè mật khẩu.'
    }
}

if ([string]::IsNullOrWhiteSpace($Email) -or [string]::IsNullOrWhiteSpace($FullName)) {
    throw 'Email và tên hiển thị không được để trống.'
}
$randomBytes = New-Object byte[] 32
$randomGenerator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try { $randomGenerator.GetBytes($randomBytes) } finally { $randomGenerator.Dispose() }
$password = 'Aa7!' + [Convert]::ToBase64String($randomBytes)
$settings = @{
    'BootstrapAdmin:Email' = $Email.Trim()
    'BootstrapAdmin:FullName' = $FullName.Trim()
    'BootstrapAdmin:Password' = $password
}

# Truyền qua stdin để mật khẩu không xuất hiện trong đối số dòng lệnh.
$settings | ConvertTo-Json | dotnet user-secrets set --project $projectPath
if ($LASTEXITCODE -ne 0) { throw 'Không thể lưu User Secrets.' }
Write-Host 'Đã chuẩn bị thông tin cục bộ. Chạy ứng dụng với --bootstrap-admin để tạo tài khoản.'
