#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Lucia.Installer.exe のインテグレーションテストスクリプト。

.DESCRIPTION
    以下のテストパターンを網羅する:

    No  シナリオ                              事前状態          操作
    00  埋め込みリソース確認                   ビルド済み DLL    Assembly.GetManifestResourceNames()
    01  新規インストール（デフォルト）         サービスなし      install --silent
    02  再インストール（稼働中に上書き）        サービス稼働中    install --silent (再度)
    03  カスタムポート                         サービスなし      install --port 7100 --silent
    04  カスタムインストールパス               サービスなし      install --install-path C:\Lucia-Test --silent
    05  カスタムサブネット                     サービスなし      install --allowed-subnet 10.0.0.0/8 --silent
    06  正常アンインストール                   サービス稼働中    uninstall --silent
    07  サービス不在でのアンインストール        サービスなし      uninstall --silent
    08  存在しないパスでのアンインストール      サービスなし      uninstall --install-path C:\NonExistent-Lucia --silent
    09  install → uninstall → install（連続）  サービスなし      3連続操作

.NOTES
    - 管理者権限で実行すること (#Requires -RunAsAdministrator が強制する)
    - 実行前に publish/Lucia.Installer/Lucia.Installer.exe が存在すること
    - テスト中に LuciaServer サービスが停止・削除されるため、本番環境では実行しないこと
    - ログファイルは C:\Users\Public\lucia-test.log に出力される
#>

param(
    [string]$InstallerExe = (Join-Path $PSScriptRoot '..\publish\Lucia.Installer\Lucia.Installer.exe'),
    [string]$DllPath      = (Join-Path $PSScriptRoot '..\src\Lucia.Installer\bin\Release\net10.0-windows\win-x64\Lucia.Installer.dll'),
    [string]$LogFile      = 'C:\Users\Public\lucia-test.log'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ─── ログ設定 ────────────────────────────────────────────────
Remove-Item $LogFile -ErrorAction SilentlyContinue

function Write-Log([string]$message, [string]$color = 'White') {
    Write-Host $message -ForegroundColor $color
    Add-Content -Path $LogFile -Value $message -Encoding UTF8
}

# ─── 定数 ───────────────────────────────────────────────────
$ServiceName        = 'LuciaServer'
$FirewallRuleName   = 'LuciaServer'
$DefaultInstallPath = 'C:\Program Files\Lucia'
$TestInstallPath    = 'C:\Lucia-Test'
$DefaultPort        = 6100

# ─── テスト結果 ──────────────────────────────────────────────
$script:Results = [System.Collections.Generic.List[PSCustomObject]]::new()

# ─── ユーティリティ関数 ──────────────────────────────────────

function Write-Section([string]$message) {
    Write-Log "`n$('─' * 60)" 'DarkGray'
    Write-Log $message 'Cyan'
    Write-Log $('─' * 60) 'DarkGray'
}

function Write-Pass([string]$message) { Write-Log "  [PASS] $message" 'Green' }
function Write-Fail([string]$message) { Write-Log "  [FAIL] $message" 'Red' }
function Write-Info([string]$message) { Write-Log "  [INFO] $message" 'Gray' }

function Invoke-Installer([string[]]$arguments) {
    Write-Info "実行: Lucia.Installer.exe $($arguments -join ' ')"
    # スペースを含む引数はダブルクォートで囲む（Start-Process は自動クォートしないため）
    $argString = ($arguments | ForEach-Object { if ($_ -match ' ') { "`"$_`"" } else { $_ } }) -join ' '
    $psi = [System.Diagnostics.ProcessStartInfo]::new($InstallerExe)
    $psi.Arguments              = $argString
    $psi.UseShellExecute        = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError  = $true
    $psi.CreateNoWindow         = $true
    $process = [System.Diagnostics.Process]::Start($psi)
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    if ($stdout) { foreach ($line in ($stdout -split "`n")) { if ($line.Trim()) { Write-Info "  stdout: $($line.TrimEnd())" } } }
    if ($stderr) { foreach ($line in ($stderr -split "`n")) { if ($line.Trim()) { Write-Info "  stderr: $($line.TrimEnd())" } } }
    return $process.ExitCode
}

# ─── アサーション関数 ────────────────────────────────────────

function Assert-True([bool]$condition, [string]$description) {
    if ($condition) {
        Write-Pass $description
        return $true
    } else {
        Write-Fail $description
        return $false
    }
}

function Assert-ServiceRunning([string]$description = 'サービスが Running 状態である') {
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    return Assert-True ($null -ne $service -and $service.Status -eq 'Running') $description
}

function Assert-ServiceNotExists([string]$description = 'サービスが存在しない') {
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    return Assert-True ($null -eq $service) $description
}

function Assert-FirewallRuleExists([int]$port, [string]$description = 'FW 規則が存在する') {
    $rule = Get-NetFirewallRule -DisplayName $FirewallRuleName -ErrorAction SilentlyContinue
    if ($null -eq $rule) { return Assert-True $false $description }
    $portFilter = $rule | Get-NetFirewallPortFilter
    return Assert-True ($portFilter.LocalPort -eq $port) $description
}

function ConvertTo-DottedMask([int]$prefixLength) {
    $bytes = @(0, 0, 0, 0)
    $remaining = $prefixLength
    for ($i = 0; $i -lt 4; $i++) {
        if ($remaining -ge 8) {
            $bytes[$i] = 255
            $remaining -= 8
        } elseif ($remaining -gt 0) {
            $bytes[$i] = 256 - [int][Math]::Pow(2, 8 - $remaining)
            $remaining = 0
        }
    }
    return "$($bytes[0]).$($bytes[1]).$($bytes[2]).$($bytes[3])"
}

function Assert-FirewallRuleSubnet([string]$expectedSubnet, [string]$description = 'FW 規則のリモートIPが正しい') {
    $rule = Get-NetFirewallRule -DisplayName $FirewallRuleName -ErrorAction SilentlyContinue
    if ($null -eq $rule) { return Assert-True $false $description }
    $addressFilter = $rule | Get-NetFirewallAddressFilter
    $actual = ($addressFilter.RemoteAddress | ForEach-Object { $_.ToString() }) -join ','

    # Windows Firewall は CIDR (/8) をドット表記 (/255.0.0.0) で保存するため両形式を試みる
    $matchesDirect = $actual -like "*$expectedSubnet*"
    $matchesDotted = $false
    if ($expectedSubnet -match '^(.+)/(\d+)$') {
        $networkAddr = $Matches[1]
        $dottedMask  = ConvertTo-DottedMask ([int]$Matches[2])
        $matchesDotted = $actual -like "*$networkAddr/$dottedMask*"
    }
    return Assert-True ($matchesDirect -or $matchesDotted) "$description (actual: $actual)"
}

function Assert-FirewallRuleNotExists([string]$description = 'FW 規則が存在しない') {
    $rule = Get-NetFirewallRule -DisplayName $FirewallRuleName -ErrorAction SilentlyContinue
    return Assert-True ($null -eq $rule) $description
}

function Assert-FilesExist([string]$installPath, [string]$description = 'インストール先にファイルが存在する') {
    $exeExists = Test-Path (Join-Path $installPath 'Lucia.Server.exe')
    return Assert-True $exeExists $description
}

function Assert-FilesNotExist([string]$installPath, [string]$description = 'インストール先のファイルが存在しない') {
    $dirExists = Test-Path $installPath
    return Assert-True (-not $dirExists) $description
}

function Assert-EventLogSourceExists([string]$description = 'イベントログソースが存在する') {
    $exists = [System.Diagnostics.EventLog]::SourceExists($ServiceName)
    return Assert-True $exists $description
}

function Assert-EventLogSourceNotExists([string]$description = 'イベントログソースが存在しない') {
    $exists = [System.Diagnostics.EventLog]::SourceExists($ServiceName)
    return Assert-True (-not $exists) $description
}

function Assert-ExitCode([int]$actual, [int]$expected = 0, [string]$description = "終了コードが $expected である") {
    return Assert-True ($actual -eq $expected) "$description (actual: $actual)"
}

function Assert-EmbeddedResourcesExist([string]$description = 'インストーラー DLL にリソースが埋め込まれている') {
    if (-not (Test-Path $DllPath)) {
        return Assert-True $false "$description (DLL が見つかりません: $DllPath)"
    }
    $asm   = [System.Reflection.Assembly]::LoadFile((Resolve-Path $DllPath).Path)
    $names = $asm.GetManifestResourceNames()
    foreach ($name in $names) { Write-Info "埋め込みリソース: $name" }
    return Assert-True ($names.Count -gt 0) $description
}

# ─── セットアップ／クリーンアップヘルパー ───────────────────

function Ensure-Clean([string]$installPath = $DefaultInstallPath) {
    Write-Info 'クリーン状態を確保しています...'
    Invoke-Installer @('uninstall', '--install-path', $installPath, '--silent') | Out-Null
    if (Test-Path $installPath) { Remove-Item $installPath -Recurse -Force }
}

function Ensure-Installed([string]$installPath = $DefaultInstallPath, [int]$port = $DefaultPort) {
    Write-Info 'インストール済み状態を確保しています...'
    Invoke-Installer @('install', '--install-path', $installPath, '--port', "$port", '--silent') | Out-Null
}

# ─── テストランナー ──────────────────────────────────────────

function Run-Test([string]$number, [string]$name, [scriptblock]$body) {
    Write-Section "Test $number : $name"
    $passed = $true
    try {
        $assertions = & $body
        foreach ($result in $assertions) {
            if ($result -eq $false) { $passed = $false }
        }
    } catch {
        Write-Fail "例外: $_"
        $passed = $false
    }
    $script:Results.Add([PSCustomObject]@{
        No     = $number
        Name   = $name
        Passed = $passed
    })
}

# ════════════════════════════════════════════════════════════
#  前提確認
# ════════════════════════════════════════════════════════════
Write-Section '前提確認'

if (-not (Test-Path $InstallerExe)) {
    Write-Log "インストーラーが見つかりません: $InstallerExe" 'Red'
    Write-Log "先に Release ビルドを実行してください:" 'Yellow'
    Write-Log "  dotnet publish src/Lucia.Installer/Lucia.Installer.csproj --configuration Release --output publish/Lucia.Installer"
    exit 1
}
Write-Info "インストーラー: $InstallerExe"
Write-Info "デフォルト インストールパス: $DefaultInstallPath"
Write-Info "テスト用インストールパス   : $TestInstallPath"
Write-Info "ログファイル               : $LogFile"

# ════════════════════════════════════════════════════════════
#  テスト実行
# ════════════════════════════════════════════════════════════

Run-Test '00' 'インストーラー DLL への埋め込みリソース確認' {
    Assert-EmbeddedResourcesExist
}

Run-Test '01' '新規インストール（デフォルト設定）' {
    Ensure-Clean

    $exitCode = Invoke-Installer @('install', '--silent')

    Assert-ExitCode $exitCode 0 '終了コードが 0'
    Assert-ServiceRunning
    Assert-FirewallRuleExists $DefaultPort "FW 規則がポート $DefaultPort で存在する"
    Assert-FilesExist $DefaultInstallPath
    Assert-EventLogSourceExists

    Ensure-Clean
}

Run-Test '02' '再インストール（サービス稼働中に上書き）' {
    Ensure-Clean
    Ensure-Installed

    $exitCode = Invoke-Installer @('install', '--silent')

    Assert-ExitCode $exitCode 0 '終了コードが 0'
    Assert-ServiceRunning '上書き後もサービスが Running 状態である'
    Assert-FirewallRuleExists $DefaultPort

    Ensure-Clean
}

Run-Test '03' 'カスタムポート（7100）' {
    Ensure-Clean

    $exitCode = Invoke-Installer @('install', '--port', '7100', '--silent')

    Assert-ExitCode $exitCode 0 '終了コードが 0'
    Assert-ServiceRunning
    Assert-FirewallRuleExists 7100 'FW 規則がポート 7100 で存在する'

    Invoke-Installer @('uninstall', '--silent') | Out-Null
}

Run-Test '04' 'カスタムインストールパス（C:\Lucia-Test）' {
    Ensure-Clean $TestInstallPath
    Ensure-Clean $DefaultInstallPath

    $exitCode = Invoke-Installer @('install', '--install-path', $TestInstallPath, '--silent')

    Assert-ExitCode $exitCode 0 '終了コードが 0'
    Assert-ServiceRunning
    Assert-FilesExist $TestInstallPath "C:\Lucia-Test にファイルが存在する"

    Invoke-Installer @('uninstall', '--install-path', $TestInstallPath, '--silent') | Out-Null
}

Run-Test '05' 'カスタムサブネット（10.0.0.0/8）' {
    Ensure-Clean

    $exitCode = Invoke-Installer @('install', '--allowed-subnet', '10.0.0.0/8', '--silent')

    Assert-ExitCode $exitCode 0 '終了コードが 0'
    Assert-ServiceRunning
    Assert-FirewallRuleSubnet '10.0.0.0/8' 'FW 規則のリモートIPが 10.0.0.0/8 である'

    Ensure-Clean
}

Run-Test '06' '正常アンインストール' {
    Ensure-Clean
    Ensure-Installed

    $exitCode = Invoke-Installer @('uninstall', '--silent')

    Assert-ExitCode $exitCode 0 '終了コードが 0'
    Assert-ServiceNotExists
    Assert-FirewallRuleNotExists
    Assert-FilesNotExist $DefaultInstallPath
    Assert-EventLogSourceNotExists
}

Run-Test '07' 'サービス不在でのアンインストール（警告のみ）' {
    Ensure-Clean

    $exitCode = Invoke-Installer @('uninstall', '--silent')

    Assert-ExitCode $exitCode 0 'サービス不在でも終了コードが 0'
}

Run-Test '08' '存在しないパスでのアンインストール（警告のみ）' {
    Ensure-Clean
    $nonExistentPath = 'C:\NonExistent-Lucia'

    $exitCode = Invoke-Installer @('uninstall', '--install-path', $nonExistentPath, '--silent')

    Assert-ExitCode $exitCode 0 'パス不在でも終了コードが 0'
}

Run-Test '09' 'install → uninstall → install（連続操作）' {
    Ensure-Clean

    Invoke-Installer @('install', '--silent') | Out-Null
    Invoke-Installer @('uninstall', '--silent') | Out-Null
    $exitCode = Invoke-Installer @('install', '--silent')

    Assert-ExitCode $exitCode 0 '3回目の install の終了コードが 0'
    Assert-ServiceRunning '3回目の install 後にサービスが Running 状態である'

    Ensure-Clean
}

# ════════════════════════════════════════════════════════════
#  結果サマリー
# ════════════════════════════════════════════════════════════
Write-Section 'テスト結果サマリー'

$passedTests = $script:Results | Where-Object { $_.Passed }
$failedTests = $script:Results | Where-Object { -not $_.Passed }

foreach ($result in $script:Results) {
    $mark  = if ($result.Passed) { '[PASS]' } else { '[FAIL]' }
    $color = if ($result.Passed) { 'Green' }  else { 'Red' }
    Write-Log ("  {0} Test {1} : {2}" -f $mark, $result.No, $result.Name) $color
}

Write-Log ''
Write-Log ("合計: {0} / {1} 通過" -f $passedTests.Count, $script:Results.Count) (
    if ($failedTests.Count -eq 0) { 'Green' } else { 'Yellow' }
)

if ($failedTests.Count -gt 0) {
    Write-Log "失敗: $($failedTests.Count) 件" 'Red'
    exit 1
} else {
    Write-Log '全テスト通過' 'Green'
    exit 0
}
