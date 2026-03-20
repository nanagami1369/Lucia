# デプロイ設定（変更する場合はこのファイルを直接編集すること）
$Port          = 6100
$InstallPath   = 'C:\Program Files\Lucia'
$AllowedSubnet = '192.168.0.0/16'

# このスクリプトは <repo>/.claude/skills/lucia-deploy/scripts/ に配置されている
# $PSScriptRoot から 4 階層上がリポジトリルート
$RepoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..' ))
$LogFile  = Join-Path $RepoRoot 'logs\lucia-deploy.log'
New-Item -ItemType Directory -Force -Path (Split-Path $LogFile) | Out-Null

# 管理者権限チェック: 非昇格の場合はログファイル経由で出力を受け取りながら自己を昇格再実行する
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Set-Content $LogFile -Value '' -Encoding UTF8
    Write-Host '管理者権限が必要です。UAC ダイアログで昇格して再実行します...' -ForegroundColor Yellow
    $argList = "-ExecutionPolicy Bypass -File `"$PSCommandPath`""
    Start-Process pwsh -ArgumentList $argList -Verb RunAs -Wait
    Write-Host ''
    Write-Host '--- 昇格プロセスの出力 ---' -ForegroundColor DarkGray
    if (Test-Path $LogFile) {
        Get-Content $LogFile -Encoding UTF8 | ForEach-Object { Write-Host $_ }
    }
    exit
}

function Write-Log([string]$message) {
    Write-Host $message
    Add-Content $LogFile -Value $message -Encoding UTF8
}

$InstallerProject = Join-Path $RepoRoot 'src\Lucia.Installer\Lucia.Installer.csproj'
$PublishDir       = Join-Path $RepoRoot 'publish\Lucia.Installer'
$InstallerExe     = Join-Path $PublishDir 'Lucia.Installer.exe'

Write-Log ''
Write-Log '=== Lucia deploy ==='
Write-Log "  リポジトリ    : $RepoRoot"
Write-Log "  ポート        : $Port"
Write-Log "  インストール先: $InstallPath"
Write-Log "  許可サブネット: $AllowedSubnet"

# Lucia.Installer を Release publish
# （csproj 内の BuildServerBundle Target が Lucia.Server のビルド・zip 化・埋め込みを自動実行する）
Write-Log ''
Write-Log '>>> Lucia.Installer をビルド・発行しています...'
dotnet publish $InstallerProject `
    --configuration Release `
    --output $PublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Log 'Lucia.Installer のビルドに失敗しました。'
    exit 1
}
Write-Log "  発行先: $PublishDir"

# Lucia.Installer.exe でインストール実行
# （既に昇格済みのため requireAdministrator は問題なく動作する）
Write-Log ''
Write-Log '>>> インストールを実行しています...'
& $InstallerExe install `
    --port $Port `
    --install-path $InstallPath `
    --allowed-subnet $AllowedSubnet `
    --silent

if ($LASTEXITCODE -ne 0) {
    Write-Log 'インストールに失敗しました。'
    exit 1
}

Write-Log ''
Write-Log '=== deploy 完了 ==='
