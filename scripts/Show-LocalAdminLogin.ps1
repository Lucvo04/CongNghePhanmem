param([switch]$CheckOnly)

$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $PSScriptRoot '..\src\StudentClubManagement.Web\StudentClubManagement.Web.csproj'
[xml]$projectXml = Get-Content -LiteralPath $projectPath -Encoding UTF8
$secretId = [string]$projectXml.Project.PropertyGroup.UserSecretsId
$secretPath = Join-Path $env:APPDATA "Microsoft\UserSecrets\$secretId\secrets.json"
if (-not (Test-Path -LiteralPath $secretPath)) { throw "Khong tim thay User Secrets: $secretPath" }
$secretText = [System.IO.File]::ReadAllText($secretPath, [System.Text.Encoding]::UTF8)
$settings = Microsoft.PowerShell.Utility\ConvertFrom-Json -InputObject $secretText
$email = $settings.PSObject.Properties['BootstrapAdmin:Email'].Value
$password = $settings.PSObject.Properties['BootstrapAdmin:Password'].Value
if ([string]::IsNullOrWhiteSpace($email)) { $email = $settings.BootstrapAdmin.Email }
if ([string]::IsNullOrWhiteSpace($password)) { $password = $settings.BootstrapAdmin.Password }
if ($CheckOnly) {
    [pscustomobject]@{
        ScriptPath = $PSCommandPath
        SecretsPath = $secretPath
        PowerShellVersion = $PSVersionTable.PSVersion.ToString()
        FileBytes = (Get-Item -LiteralPath $secretPath).Length
        FileLastWriteUtc = (Get-Item -LiteralPath $secretPath).LastWriteTimeUtc.ToString('o')
        ConfigurationKeys = @($settings.PSObject.Properties.Name) -join ', '
        HasAdminEmail = -not [string]::IsNullOrWhiteSpace($email)
        HasInitialPassword = -not [string]::IsNullOrWhiteSpace($password)
    }
    return
}
if ([string]::IsNullOrWhiteSpace($email) -or [string]::IsNullOrWhiteSpace($password)) {
    throw 'Thieu email hoac mat khau khoi tao. Chay lai script voi -CheckOnly de kiem tra dung tep cau hinh; khong tao lai Admin.'
}
Write-Host "Email: $email"
Write-Host "Mật khẩu khởi tạo: $password"
Write-Host 'Nếu đã đổi mật khẩu trên website, hãy dùng mật khẩu mới bạn đã đặt; script này không đọc được mật khẩu hiện tại.'
Write-Host 'Thông tin riêng trên máy này; không đưa vào Git hoặc ảnh chụp báo cáo.'
