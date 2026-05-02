<#
.SYNOPSIS
    Lucia CLI インストーラーのインテグレーションテストスクリプト。

.DESCRIPTION
    以下のテストパターンを網羅する:

    No  シナリオ                              事前状態          操作
    00  installer.exe ファイル確認            ビルド済み        ファイル存在確認
    01  新規インストール（デフォルト）         サービスなし      install --yes
    02  再インストール（稼働中に上書き）        サービス稼働中    install --yes
    03  カスタムポート（7100）                 サービスなし      install --port 7100 --yes
    04  カスタムインストールパス               サービスなし      install --install-dir C:\Lucia-Test --yes
    05  カスタムサブネット（10.0.0.0/8）       サービスなし      install --subnet 10.0.0.0/8 --yes
    06  正常アンインストール                   サービス稼働中    uninstall --yes
    07  未インストール状態でのアンインストール  サービスなし      uninstall --yes (終了コード 1 を期待)
    08  install → uninstall → install（連続）  サービスなし      3連続操作
    09  不正ポート（0）は終了コード 1          サービスなし      install --port 0 --yes
    10  不正ポート（65536）は終了コード 1      サービスなし      install --port 65536 --yes
    11  不正ポート（非数値）は終了コード 1     サービスなし      install --port abc --yes
    12  不正 CIDR（スラッシュなし）は終了コード 1  サービスなし  install --subnet 192.168.0.0 --yes
    13  不正 CIDR（プレフィックス超過）は終了コード 1  サービスなし  install --subnet 192.168.0.0/33 --yes
    14  境界値ポート 1 で正常インストール      サービスなし      install --port 1 --yes
    15  境界値ポート 65535 で正常インストール  サービスなし      install --port 65535 --yes
    16  境界値サブネット 0.0.0.0/0            サービスなし      install --subnet 0.0.0.0/0 --yes
    17  status コマンド（未インストール）      サービスなし      status
    18  status コマンド（インストール済み）    サービス稼働中    status
    19  modify コマンド（ポート変更）          サービス稼働中    modify --port 7200 --yes
    20  modify コマンド（サブネット変更）      サービス稼働中    modify --subnet 10.0.0.0/8 --yes
    21  modify（未インストール時）は終了コード 1  サービスなし   modify --port 7300 --yes
    22  --help は終了コード 0                  -                 --help
    23  不明なコマンドは終了コード 1           -                 unknown-command

.PARAMETER InstallerPath
    テスト対象の installer.exe のパス。省略時は Release publish の出力先を使用。

.PARAMETER LogFile
    テスト結果を書き出すログファイルのパス。UAC 昇格後の結果を非昇格シェルから参照するために使用。

.PARAMETER Force
    既存の Lucia インストールが検出された場合の確認プロンプトをスキップする。

.NOTES
    - 管理者権限が必要。非昇格で実行するとログファイル経由で自己昇格する。
    - 実行前に Release ビルドが完了していること:
        dotnet publish src/Lucia.Installer/Lucia.Installer.csproj --configuration Release
    - テスト中に LuciaServer サービスが停止・削除されるため、本番環境では実行しないこと
    - ログファイルは C:\Users\Public\lucia-test.log に出力される
#>

param(
    [string]$InstallerPath = (Join-Path $PSScriptRoot '..\src\Lucia.Installer\bin\Release\net10.0-windows\win-x64\publish\installer.exe'),
    [string]$LogFile       = 'C:\Users\Public\lucia-test.log',
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$InstallerPath = [System.IO.Path]::GetFullPath($InstallerPath)
$LogFile       = [System.IO.Path]::GetFullPath($LogFile)

# ─── 管理者権限チェック: 非昇格の場合はログファイル経由で UAC 自己昇格する ───
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Set-Content $LogFile -Value '' -Encoding UTF8
    Write-Host '管理者権限が必要です。UAC ダイアログで昇格して再実行します...' -ForegroundColor Yellow
    $forceArg = if ($Force) { ' -Force' } else { '' }
    $argList  = "-ExecutionPolicy Bypass -File `"$PSCommandPath`"$forceArg"
    Start-Process pwsh -ArgumentList $argList -Verb RunAs -Wait
    Write-Host ''
    Write-Host '--- 昇格プロセスの出力 ---' -ForegroundColor DarkGray
    if (Test-Path $LogFile) { Get-Content $LogFile -Encoding UTF8 | ForEach-Object { Write-Host $_ } }
    exit
}

# ─── ログ設定 ────────────────────────────────────────────────────────
Remove-Item $LogFile -ErrorAction SilentlyContinue

function Write-Log([string]$message, [string]$color = 'White') {
    Write-Host $message -ForegroundColor $color
    Add-Content -Path $LogFile -Value $message -Encoding UTF8
}

# ─── 定数 ──────────────────────────────────────────────────────────────
$ServiceName        = 'LuciaServer'
$FirewallRuleName   = 'LuciaServer'
$DefaultInstallPath = 'C:\Program Files\Lucia'
$TestInstallPath    = 'C:\Lucia-Test'
$DefaultPort        = 6100
$DefaultSubnet      = '192.168.0.0/16'

# ─── テスト結果 ────────────────────────────────────────────────────────
$script:Results = [System.Collections.Generic.List[PSCustomObject]]::new()

# ─── ユーティリティ関数 ──────────────────────────────────────────────

function Write-Section([string]$message) {
    Write-Log "`n$('─' * 60)" 'DarkGray'
    Write-Log $message 'Cyan'
    Write-Log $('─' * 60) 'DarkGray'
}

function Write-Pass([string]$message) { Write-Log "  [PASS] $message" 'Green' }
function Write-Fail([string]$message) { Write-Log "  [FAIL] $message" 'Red' }
function Write-Warn([string]$message) { Write-Log "  [WARN] $message" 'Yellow' }
function Write-Info([string]$message) { Write-Log "  [INFO] $message" 'Gray' }

function Invoke-Installer {
    param([string[]]$Arguments)

    # スペースを含む引数は二重引用符で囲む（末尾バックスラッシュはエスケープ回避のため除去）
    $argString = ($Arguments | ForEach-Object {
        if ($_ -match '[\s"]') { '"' + $_.TrimEnd('\').Replace('"', '\"') + '"' }
        else { $_ }
    }) -join ' '

    Write-Info "実行: installer.exe $argString"

    $stdoutFile = [System.IO.Path]::GetTempFileName()
    $stderrFile = [System.IO.Path]::GetTempFileName()
    try {
        $process = Start-Process -FilePath $InstallerPath `
                                 -ArgumentList $argString `
                                 -Wait -PassThru -NoNewWindow `
                                 -RedirectStandardOutput $stdoutFile `
                                 -RedirectStandardError $stderrFile
        $stdout = Get-Content $stdoutFile -Encoding UTF8 -ErrorAction SilentlyContinue
        $stderr = Get-Content $stderrFile -Encoding UTF8 -ErrorAction SilentlyContinue
        if ($stdout) { $stdout | ForEach-Object { Write-Info $_ } }
        if ($stderr) { $stderr | ForEach-Object { Write-Warn "STDERR: $_" } }
        Write-Info "終了コード: $($process.ExitCode)"
        return $process.ExitCode
    } finally {
        Remove-Item $stdoutFile, $stderrFile -ErrorAction SilentlyContinue
    }
}

# ─── アサーション関数 ─────────────────────────────────────────────────

function Assert-True([bool]$condition, [string]$description) {
    if ($condition) {
        Write-Pass $description
        return $true
    } else {
        Write-Fail $description
        return $false
    }
}

function Assert-ExitCode([int]$actual, [int[]]$expected = @(0), [string]$description = '') {
    $ok   = $expected -contains $actual
    $desc = if ($description) { $description } else { "終了コードが $($expected -join ' or ') である (actual: $actual)" }
    return Assert-True $ok $desc
}

function Assert-ServiceRunning([string]$description = 'サービスが Running 状態である') {
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    return Assert-True ($null -ne $service -and $service.Status -eq 'Running') $description
}

function Assert-ServiceNotExists([string]$description = 'サービスが存在しない') {
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    return Assert-True ($null -eq $service) $description
}

function Assert-FirewallRuleExists([int]$port, [string]$description = '') {
    $desc = if ($description) { $description } else { "FW 規則がポート $port で存在する" }
    $rule = Get-NetFirewallRule -DisplayName $FirewallRuleName -ErrorAction SilentlyContinue
    if ($null -eq $rule) { return Assert-True $false $desc }
    $portFilter = $rule | Get-NetFirewallPortFilter
    return Assert-True ("$($portFilter.LocalPort)" -eq "$port") $desc
}

function ConvertTo-DottedMask([int]$prefixLength) {
    $bytes     = @(0, 0, 0, 0)
    $remaining = $prefixLength
    for ($i = 0; $i -lt 4; $i++) {
        if ($remaining -ge 8) {
            $bytes[$i] = 255; $remaining -= 8
        } elseif ($remaining -gt 0) {
            $bytes[$i] = 256 - [int][Math]::Pow(2, 8 - $remaining); $remaining = 0
        }
    }
    return "$($bytes[0]).$($bytes[1]).$($bytes[2]).$($bytes[3])"
}

function Assert-FirewallRuleSubnet([string]$expectedSubnet, [string]$description = '') {
    $desc = if ($description) { $description } else { "FW 規則のリモートIPが $expectedSubnet である" }
    $rule = Get-NetFirewallRule -DisplayName $FirewallRuleName -ErrorAction SilentlyContinue
    if ($null -eq $rule) { return Assert-True $false $desc }
    $addressFilter = $rule | Get-NetFirewallAddressFilter
    $actual        = ($addressFilter.RemoteAddress | ForEach-Object { $_.ToString() }) -join ','
    Write-Info "FW RemoteAddress (actual): $actual"

    # Windows Firewall は CIDR (/16) をドット表記 (/255.255.0.0) で保存するため両形式を検証
    $matchesDirect = $actual -like "*$expectedSubnet*"
    $matchesDotted = $false
    if ($expectedSubnet -match '^(.+)/(\d+)$') {
        $networkAddr   = $Matches[1]
        $dottedMask    = ConvertTo-DottedMask ([int]$Matches[2])
        $matchesDotted = $actual -like "*$networkAddr/$dottedMask*"
    }
    $matchesAny = ($expectedSubnet -eq '0.0.0.0/0' -and $actual -like '*Any*')
    return Assert-True ($matchesDirect -or $matchesDotted -or $matchesAny) $desc
}

function Assert-FirewallRuleNotExists([string]$description = 'FW 規則が存在しない') {
    $rule = Get-NetFirewallRule -DisplayName $FirewallRuleName -ErrorAction SilentlyContinue
    return Assert-True ($null -eq $rule) $description
}

function Assert-FilesExist([string]$installPath, [string]$description = '') {
    $desc      = if ($description) { $description } else { "$installPath に Lucia.Server.exe が存在する" }
    $exeExists = Test-Path (Join-Path $installPath 'Lucia.Server.exe')
    return Assert-True $exeExists $desc
}

function Assert-FilesNotExist([string]$installPath, [string]$description = '') {
    $desc      = if ($description) { $description } else { "$installPath が存在しない" }
    $dirExists = Test-Path $installPath
    return Assert-True (-not $dirExists) $desc
}

function Assert-EventLogSourceExists([string]$description = 'イベントログソースが存在する') {
    $exists = [System.Diagnostics.EventLog]::SourceExists($ServiceName)
    return Assert-True $exists $description
}

function Assert-EventLogSourceNotExists([string]$description = 'イベントログソースが存在しない') {
    $exists = [System.Diagnostics.EventLog]::SourceExists($ServiceName)
    return Assert-True (-not $exists) $description
}

function Assert-PortListening([int]$port, [string]$description = '') {
    $desc      = if ($description) { $description } else { "ポート $port でリッスンしている" }
    $deadline  = (Get-Date).AddSeconds(10)
    $listening = $false
    while ((Get-Date) -lt $deadline) {
        $result = Test-NetConnection -ComputerName 'localhost' -Port $port `
                                     -InformationLevel Quiet -WarningAction SilentlyContinue
        if ($result) { $listening = $true; break }
        Start-Sleep -Seconds 1
    }
    return Assert-True $listening $desc
}

# ─── セットアップ／クリーンアップヘルパー ────────────────────────────

function Ensure-Clean() {
    Write-Info 'クリーン状態を確保しています...'
    # 未インストール時は終了コード 1 が返るが無視する
    Invoke-Installer @('uninstall', '--yes') | Out-Null
    if (Test-Path $DefaultInstallPath) {
        Write-Info "$DefaultInstallPath を強制削除しています（テスト環境クリーンアップ）..."
        Remove-Item $DefaultInstallPath -Recurse -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path $TestInstallPath) {
        Write-Info "$TestInstallPath を強制削除しています（テスト環境クリーンアップ）..."
        Remove-Item $TestInstallPath -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Ensure-Installed {
    param([string[]]$ExtraArgs = @())
    Write-Info 'インストール済み状態を確保しています...'
    Invoke-Installer (@('install', '--yes') + $ExtraArgs) | Out-Null
}

# ─── テストランナー ──────────────────────────────────────────────────

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

if (-not (Test-Path $InstallerPath)) {
    Write-Log "installer.exe が見つかりません: $InstallerPath" 'Red'
    Write-Log "先に Release ビルドを実行してください:" 'Yellow'
    Write-Log "  dotnet publish src/Lucia.Installer/Lucia.Installer.csproj --configuration Release"
    exit 1
}

Write-Info "インストーラーパス         : $InstallerPath"
Write-Info "デフォルト インストールパス: $DefaultInstallPath"
Write-Info "テスト用インストールパス   : $TestInstallPath"
Write-Info "ログファイル               : $LogFile"

# ─── 既存インストール確認 ─────────────────────────────────────────────
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -ne $existingService) {
    Write-Log '' 'White'
    Write-Log "警告: LuciaServer サービスが既にインストールされています（状態: $($existingService.Status)）。" 'Yellow'
    Write-Log "このテストは既存の Lucia をアンインストールし、複数回の再インストールを行います。" 'Yellow'
    Write-Log "本番環境では実行しないでください。" 'Yellow'

    if (-not $Force) {
        Write-Log '' 'White'
        $answer = Read-Host "続行しますか？ [Y/N]"
        if ($answer -notmatch '^[Yy]') {
            Write-Log "テストをキャンセルしました。" 'Cyan'
            exit 0
        }
    } else {
        Write-Info "-Force が指定されているため確認をスキップします。"
    }
}

# ════════════════════════════════════════════════════════════
#  テスト実行
# ════════════════════════════════════════════════════════════

Run-Test '00' 'installer.exe ファイル確認' {
    Assert-True (Test-Path $InstallerPath) "installer.exe が存在する"
}

Run-Test '01' '新規インストール（デフォルト設定）' {
    Ensure-Clean
    $exitCode = Invoke-Installer @('install', '--yes')
    Assert-ExitCode $exitCode
    Assert-ServiceRunning
    Assert-FirewallRuleExists $DefaultPort
    Assert-FirewallRuleSubnet $DefaultSubnet "FW 規則のリモートIPがデフォルトサブネット ($DefaultSubnet) である"
    Assert-FilesExist $DefaultInstallPath
    Assert-EventLogSourceExists
    Ensure-Clean
}

Run-Test '02' '再インストール（サービス稼働中に上書き）' {
    Ensure-Clean
    Ensure-Installed
    $exitCode = Invoke-Installer @('install', '--yes')
    Assert-ExitCode $exitCode
    Assert-ServiceRunning '上書き後もサービスが Running 状態である'
    Assert-FirewallRuleExists $DefaultPort
    Assert-FilesExist $DefaultInstallPath '上書き後もファイルが存在する'
    Ensure-Clean
}

Run-Test '03' 'カスタムポート（7100）' {
    Ensure-Clean
    $exitCode = Invoke-Installer @('install', '--port', '7100', '--yes')
    Assert-ExitCode $exitCode
    Assert-ServiceRunning
    Assert-FirewallRuleExists 7100 'FW 規則がポート 7100 で存在する'
    Assert-PortListening 7100 'サービスがポート 7100 でリッスンしている'
    Ensure-Clean
}

Run-Test '04' 'カスタムインストールパス（C:\Lucia-Test）' {
    Ensure-Clean
    $exitCode = Invoke-Installer @('install', '--install-dir', $TestInstallPath, '--yes')
    Assert-ExitCode $exitCode
    Assert-ServiceRunning
    Assert-FilesExist $TestInstallPath 'C:\Lucia-Test に Lucia.Server.exe が存在する'
    Assert-FilesNotExist $DefaultInstallPath "デフォルトパス ($DefaultInstallPath) にはインストールされていない"
    Ensure-Clean
}

Run-Test '05' 'カスタムサブネット（10.0.0.0/8）' {
    Ensure-Clean
    $exitCode = Invoke-Installer @('install', '--subnet', '10.0.0.0/8', '--yes')
    Assert-ExitCode $exitCode
    Assert-ServiceRunning
    Assert-FirewallRuleSubnet '10.0.0.0/8' 'FW 規則のリモートIPが 10.0.0.0/8 である'
    Ensure-Clean
}

Run-Test '06' '正常アンインストール' {
    Ensure-Clean
    Ensure-Installed
    $exitCode = Invoke-Installer @('uninstall', '--yes')
    Assert-ExitCode $exitCode
    Assert-ServiceNotExists
    Assert-FirewallRuleNotExists
    Assert-FilesNotExist $DefaultInstallPath
    Assert-EventLogSourceNotExists
}

Run-Test '07' '未インストール状態でのアンインストール（終了コード 1）' {
    Ensure-Clean
    $exitCode = Invoke-Installer @('uninstall', '--yes')
    Assert-ExitCode $exitCode @(1) '未インストール時の終了コードが 1 である'
}

Run-Test '08' 'install → uninstall → install（連続操作）' {
    Ensure-Clean
    Ensure-Installed

    $uninstallCode = Invoke-Installer @('uninstall', '--yes')
    Assert-ExitCode $uninstallCode @(0) 'アンインストールの終了コードが 0 である'
    Assert-ServiceNotExists 'アンインストール後にサービスが存在しない'
    Assert-FilesNotExist $DefaultInstallPath 'アンインストール後にファイルが存在しない'

    $installCode = Invoke-Installer @('install', '--yes')
    Assert-ExitCode $installCode
    Assert-ServiceRunning '2回目の install 後にサービスが Running 状態である'
    Assert-FirewallRuleExists $DefaultPort
    Assert-FilesExist $DefaultInstallPath

    Ensure-Clean
}

Run-Test '09' '不正ポート（0）は終了コード 1 で失敗する' {
    Ensure-Clean
    $exitCode = Invoke-Installer @('install', '--port', '0', '--yes')
    Assert-ExitCode $exitCode @(1) '不正ポート 0 のインストールが 1 で失敗する'
    Assert-ServiceNotExists 'インストール失敗後にサービスが存在しない'
    Assert-FirewallRuleNotExists 'インストール失敗後に FW 規則が存在しない'
}

Run-Test '10' '不正ポート（65536）は終了コード 1 で失敗する' {
    Ensure-Clean
    $exitCode = Invoke-Installer @('install', '--port', '65536', '--yes')
    Assert-ExitCode $exitCode @(1) '不正ポート 65536 のインストールが 1 で失敗する'
    Assert-ServiceNotExists
}

Run-Test '11' '不正ポート（非数値）は終了コード 1 で失敗する' {
    Ensure-Clean
    $exitCode = Invoke-Installer @('install', '--port', 'abc', '--yes')
    Assert-ExitCode $exitCode @(1) '不正ポート "abc" のインストールが 1 で失敗する'
    Assert-ServiceNotExists
}

Run-Test '12' '不正 CIDR（スラッシュなし）は終了コード 1 で失敗する' {
    Ensure-Clean
    $exitCode = Invoke-Installer @('install', '--subnet', '192.168.0.0', '--yes')
    Assert-ExitCode $exitCode @(1) '不正 CIDR "192.168.0.0" のインストールが 1 で失敗する'
    Assert-ServiceNotExists
    Assert-FirewallRuleNotExists 'インストール失敗後に FW 規則が存在しない（全 IP 開放にならない）'
}

Run-Test '13' '不正 CIDR（プレフィックス超過）は終了コード 1 で失敗する' {
    Ensure-Clean
    $exitCode = Invoke-Installer @('install', '--subnet', '192.168.0.0/33', '--yes')
    Assert-ExitCode $exitCode @(1) '不正 CIDR "192.168.0.0/33" のインストールが 1 で失敗する'
    Assert-ServiceNotExists
}

Run-Test '14' '境界値ポート 1 で正常インストール' {
    Ensure-Clean
    $exitCode = Invoke-Installer @('install', '--port', '1', '--yes')
    Assert-ExitCode $exitCode
    Assert-ServiceRunning
    Assert-FirewallRuleExists 1 'FW 規則がポート 1 で存在する'
    Ensure-Clean
}

Run-Test '15' '境界値ポート 65535 で正常インストール' {
    Ensure-Clean
    $exitCode = Invoke-Installer @('install', '--port', '65535', '--yes')
    Assert-ExitCode $exitCode
    Assert-ServiceRunning
    Assert-FirewallRuleExists 65535 'FW 規則がポート 65535 で存在する'
    Ensure-Clean
}

Run-Test '16' '境界値サブネット 0.0.0.0/0（全許可）で正常インストール' {
    Ensure-Clean
    $exitCode = Invoke-Installer @('install', '--subnet', '0.0.0.0/0', '--yes')
    Assert-ExitCode $exitCode
    Assert-ServiceRunning
    Assert-FirewallRuleSubnet '0.0.0.0/0' 'FW 規則のリモートIPが 0.0.0.0/0 である'
    Ensure-Clean
}

Run-Test '17' 'status コマンド（未インストール）' {
    Ensure-Clean
    $exitCode = Invoke-Installer @('status')
    Assert-ExitCode $exitCode @(0) 'status コマンドが 0 で終了する'
}

Run-Test '18' 'status コマンド（インストール済み）' {
    Ensure-Installed
    $exitCode = Invoke-Installer @('status')
    Assert-ExitCode $exitCode @(0) 'status コマンドが 0 で終了する'
    Ensure-Clean
}

Run-Test '19' 'modify コマンド（ポート変更）' {
    Ensure-Clean
    Ensure-Installed
    $exitCode = Invoke-Installer @('modify', '--port', '7200', '--yes')
    Assert-ExitCode $exitCode
    Assert-ServiceRunning 'ポート変更後にサービスが Running 状態である'
    Assert-FirewallRuleExists 7200 'FW 規則がポート 7200 で存在する'
    Assert-PortListening 7200 'サービスがポート 7200 でリッスンしている'
    Ensure-Clean
}

Run-Test '20' 'modify コマンド（サブネット変更）' {
    Ensure-Clean
    Ensure-Installed
    $exitCode = Invoke-Installer @('modify', '--subnet', '10.0.0.0/8', '--yes')
    Assert-ExitCode $exitCode
    Assert-ServiceRunning 'サブネット変更後にサービスが Running 状態である'
    Assert-FirewallRuleSubnet '10.0.0.0/8' 'FW 規則のリモートIPが 10.0.0.0/8 である'
    Ensure-Clean
}

Run-Test '21' 'modify コマンド（未インストール時は終了コード 1）' {
    Ensure-Clean
    $exitCode = Invoke-Installer @('modify', '--port', '7300', '--yes')
    Assert-ExitCode $exitCode @(1) '未インストール時の modify が 1 で失敗する'
}

Run-Test '22' '--help は終了コード 0 で終了する' {
    $exitCode = Invoke-Installer @('--help')
    Assert-ExitCode $exitCode @(0) '--help が 0 で終了する'
}

Run-Test '23' '不明なコマンドは終了コード 1 で失敗する' {
    $exitCode = Invoke-Installer @('unknown-command')
    Assert-ExitCode $exitCode @(1) '不明なコマンドが 1 で失敗する'
}

# ════════════════════════════════════════════════════════════
#  結果サマリー
# ════════════════════════════════════════════════════════════
Write-Section 'テスト結果サマリー'

$passedTests = $script:Results | Where-Object { $_.Passed }
$failedTests = $script:Results | Where-Object { -not $_.Passed }

foreach ($result in $script:Results) {
    $mark  = if ($result.Passed) { '[PASS]' } else { '[FAIL]' }
    $color = if ($result.Passed) { 'Green' } else { 'Red' }
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
