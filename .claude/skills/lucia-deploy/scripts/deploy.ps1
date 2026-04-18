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

$WixProject = Join-Path $RepoRoot 'src\Lucia.WixInstaller\Lucia.WixInstaller.wixproj'
$MsiPath    = Join-Path $RepoRoot 'src\Lucia.WixInstaller\bin\x64\Release\ja-JP\Lucia.msi'

Write-Log ''
Write-Log '=== Lucia deploy ==='
Write-Log "  リポジトリ: $RepoRoot"

# Lucia.WixInstaller を Release ビルド
# （wixproj 内の PublishServer Target が Lucia.Server の publish を自動実行する）
Write-Log ''
Write-Log '>>> MSI インストーラーをビルドしています...'
dotnet build $WixProject --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-Log 'MSI のビルドに失敗しました。'
    exit 1
}
Write-Log "  MSI: $MsiPath"

# MSI でサイレントインストール実行
# MajorUpgrade により旧バージョンは自動アンインストールされる
Write-Log ''
Write-Log '>>> MSI インストールを実行しています...'
$msiLog = Join-Path $RepoRoot 'logs\lucia-msi.log'
msiexec /i $MsiPath /quiet /norestart /l*v $msiLog

if ($LASTEXITCODE -ne 0) {
    Write-Log "インストールに失敗しました。詳細ログ: $msiLog"
    exit 1
}

Write-Log ''
Write-Log '=== deploy 完了 ==='
