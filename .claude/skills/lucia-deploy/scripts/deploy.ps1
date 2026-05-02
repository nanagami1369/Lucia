# ==============================================================================
# 【開発者専用】開発中バージョンの再インストールスクリプト
#
# このスクリプトは開発マシン上で現在のブランチをビルドして再インストールするためのもの。
# エンドユーザーへの配布・通常インストール用ではない。
# ==============================================================================
#
# このスクリプトは <repo>/.claude/skills/lucia-deploy/scripts/ に配置されている
# $PSScriptRoot から 4 階層上がリポジトリルート
$RepoRoot      = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..' ))
$LogFile       = Join-Path $RepoRoot 'logs\lucia-deploy.log'
$InstallerPath = Join-Path $RepoRoot 'src\Lucia.Installer\bin\Release\net10.0-windows\win-x64\publish\installer.exe'
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

function Write-Log([string]$message, [string]$color = 'White') {
    Write-Host $message -ForegroundColor $color
    Add-Content $LogFile -Value $message -Encoding UTF8
}

function Invoke-Installer([string[]]$Arguments) {
    $argString = ($Arguments | ForEach-Object {
        if ($_ -match '[\s"]') { '"' + $_.TrimEnd('\').Replace('"', '\"') + '"' }
        else { $_ }
    }) -join ' '

    Write-Log "  実行: installer.exe $argString" 'Gray'

    $stdoutFile = [System.IO.Path]::GetTempFileName()
    $stderrFile = [System.IO.Path]::GetTempFileName()
    try {
        $process = Start-Process -FilePath $InstallerPath `
                                 -ArgumentList $argString `
                                 -Wait -PassThru -NoNewWindow `
                                 -RedirectStandardOutput $stdoutFile `
                                 -RedirectStandardError  $stderrFile
        $stdout = Get-Content $stdoutFile -Encoding UTF8 -ErrorAction SilentlyContinue
        $stderr = Get-Content $stderrFile -Encoding UTF8 -ErrorAction SilentlyContinue
        if ($stdout) { $stdout | ForEach-Object { Write-Log "  $_" 'Gray' } }
        if ($stderr) { $stderr | ForEach-Object { Write-Log "  [STDERR] $_" 'Yellow' } }
        return $process.ExitCode
    } finally {
        Remove-Item $stdoutFile, $stderrFile -ErrorAction SilentlyContinue
    }
}

Write-Log ''
Write-Log '=== Lucia redeploy ===' 'Cyan'
Write-Log "  リポジトリ: $RepoRoot"

# ── Step 1: CLI インストーラーを Release ビルド ────────────────────────────
# dotnet publish が内部で Lucia.Server の publish → app-bundle.zip 生成まで自動実行する
Write-Log ''
Write-Log '>>> CLI インストーラーをビルドしています...' 'Cyan'
$csproj = Join-Path $RepoRoot 'src\Lucia.Installer\Lucia.Installer.csproj'
dotnet publish $csproj --configuration Release
if ($LASTEXITCODE -ne 0) {
    Write-Log 'ビルドに失敗しました。' 'Red'
    exit 1
}
Write-Log "  installer.exe: $InstallerPath" 'Gray'

# ── Step 2: 既存インストールをアンインストール ────────────────────────────
Write-Log ''
Write-Log '>>> 既存バージョンをアンインストールしています...' 'Cyan'
$uninstallCode = Invoke-Installer @('uninstall', '--yes')
if ($uninstallCode -eq 0) {
    Write-Log '  アンインストール完了' 'Gray'
} elseif ($uninstallCode -eq 1) {
    Write-Log '  未インストール状態のため、スキップします' 'Gray'
} else {
    Write-Log "  アンインストールに失敗しました (ExitCode: $uninstallCode)" 'Red'
    exit 1
}

# ── Step 3: 新バージョンをインストール ───────────────────────────────────
Write-Log ''
Write-Log '>>> 新バージョンをインストールしています...' 'Cyan'
$installCode = Invoke-Installer @('install', '--yes')
if ($installCode -ne 0) {
    Write-Log "インストールに失敗しました (ExitCode: $installCode)" 'Red'
    exit 1
}

Write-Log ''
Write-Log '=== redeploy 完了 ===' 'Green'
