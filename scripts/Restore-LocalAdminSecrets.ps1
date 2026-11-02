param(
    [switch]$CheckOnly,
    [string]$RecoveryPath = (Join-Path $PSScriptRoot '..\.local\admin-login-recovery.clixml')
)

$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $PSScriptRoot '..\src\StudentClubManagement.Web\StudentClubManagement.Web.csproj'
[xml]$projectXml = Get-Content -LiteralPath $projectPath -Encoding UTF8
$secretId = [string]$projectXml.Project.PropertyGroup.UserSecretsId
$secretPath = Join-Path $env:APPDATA "Microsoft\UserSecrets\$secretId\secrets.json"
if (-not (Test-Path -LiteralPath $RecoveryPath -PathType Leaf)) {
    throw 'Khong tim thay ban khoi phuc da ma hoa. Khong tao lai Admin.'
}
# Export-Clixml protects PSCredential passwords with Windows DPAPI for this Windows user.
$credential = Import-Clixml -LiteralPath $RecoveryPath
if ($credential -isnot [System.Management.Automation.PSCredential]) {
    throw 'Ban khoi phuc khong dung dinh dang PSCredential.'
}
$recoveredPassword = $credential.GetNetworkCredential().Password
if ([string]::IsNullOrWhiteSpace($credential.UserName) -or [string]::IsNullOrWhiteSpace($recoveredPassword)) {
    throw 'Khong doc duoc thong tin khoi phuc.'
}
if (-not (Test-Path -LiteralPath $secretPath -PathType Leaf)) {
    throw 'Khong tim thay tep cau hinh hien tai. Khong tu tao hoac thay connection string.'
}
$settings = Microsoft.PowerShell.Utility\ConvertFrom-Json -InputObject ([System.IO.File]::ReadAllText($secretPath))
$hasExistingAdminSettings = $settings.PSObject.Properties['BootstrapAdmin:Email'] -or
    $settings.PSObject.Properties['BootstrapAdmin:Password'] -or
    ($settings.BootstrapAdmin -and ($settings.BootstrapAdmin.PSObject.Properties['Email'] -or $settings.BootstrapAdmin.PSObject.Properties['Password']))
if ($CheckOnly) {
    [pscustomobject]@{
        RecoveryReadable = $true
        SecretsPath = $secretPath
        ExistingAdminSettings = [bool]$hasExistingAdminSettings
        WillRestoreMissingSettings = -not [bool]$hasExistingAdminSettings
    }
    return
}
if ($hasExistingAdminSettings) {
    throw 'Da co truong cau hinh Admin; khong ghi de. Dung Show-LocalAdminLogin.ps1 -CheckOnly de kiem tra.'
}

$settings | Add-Member -NotePropertyName 'BootstrapAdmin:Email' -NotePropertyValue $credential.UserName
$settings | Add-Member -NotePropertyName 'BootstrapAdmin:Password' -NotePropertyValue $recoveredPassword
$json = Microsoft.PowerShell.Utility\ConvertTo-Json -InputObject $settings -Depth 100
$temporaryPath = $secretPath + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
try {
    [System.IO.File]::WriteAllText($temporaryPath, $json, (New-Object System.Text.UTF8Encoding($false)))
    # Replace only the configuration file read above, preserving its other JSON fields.
    [System.IO.File]::Replace($temporaryPath, $secretPath, [NullString]::Value)
}
finally {
    if (Test-Path -LiteralPath $temporaryPath) { Remove-Item -LiteralPath $temporaryPath }
    $recoveredPassword = $null
    $json = $null
}
Write-Host 'Da khoi phuc cau hinh khoi tao. Khong thay tai khoan/mat khau trong database.'
& (Join-Path $PSScriptRoot 'Show-LocalAdminLogin.ps1')
