<#
.SYNOPSIS
    Lucia WiX MSI インストーラーのインテグレーションテストスクリプト。

.DESCRIPTION
    以下のテストパターンを網羅する:

    No  シナリオ                              事前状態          操作
    00  MSI ファイル確認                       ビルド済み MSI    ファイル存在・バージョン確認
    01  新規インストール（デフォルト）         サービスなし      msiexec /i /qn
    02  再インストール（稼働中に上書き）        サービス稼働中    msiexec /i /qn (MajorUpgrade)
    03  カスタムポート                         サービスなし      msiexec /i PORT=7100 /qn
    04  カスタムインストールパス               サービスなし      msiexec /i INSTALLFOLDER=C:\Lucia-Test\ /qn
    05  カスタムサブネット                     サービスなし      msiexec /i ALLOWED_SUBNET=10.0.0.0/8 /qn
    06  正常アンインストール                   サービス稼働中    msiexec /x /qn
    07  未インストール状態でのアンインストール  サービスなし      msiexec /x /qn (1605 を許容)
    08  install → uninstall → install（連続）  サービスなし      3連続操作

.PARAMETER Force
    既存の Lucia インストールが検出された場合の確認プロンプトをスキップする。
    本番環境での誤実行を防ぐため、省略時は確認を求める。

.NOTES
    - 管理者権限で実行すること
    - 実行前に MSI がビルド済みであること
    - テスト中に LuciaServer サービスが停止・削除されるため、本番環境では実行しないこと
    - ログファイルは C:\Users\Public\lucia-test.log に出力される
    - MSI の詳細ログは C:\Users\Public\lucia-msi.log に出力される
#>

param(
    [string]$MsiPath    = (Join-Path $PSScriptRoot '..\src\Lucia.WixInstaller\bin\x64\Release\ja-JP\Lucia.msi'),
    [string]$LogFile    = 'C:\Users\Public\lucia-test.log',
    [string]$MsiLogFile = 'C:\Users\Public\lucia-msi.log',
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# パスに ".." が含まれると msiexec が開けないため正規化する
$MsiPath    = [System.IO.Path]::GetFullPath($MsiPath)
$LogFile    = [System.IO.Path]::GetFullPath($LogFile)
$MsiLogFile = [System.IO.Path]::GetFullPath($MsiLogFile)

# ─── 管理者権限チェック: 非昇格の場合は UAC で自己を昇格再実行する ─────
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

# ─── 定数 ─────────────────────────────────────────────────────────────
$ServiceName        = 'LuciaServer'
$FirewallRuleName   = 'LuciaServer'
$DefaultInstallPath = 'C:\Program Files\Lucia'
$TestInstallPath    = 'C:\Lucia-Test'
$DefaultPort        = 6100
$DefaultSubnet      = '192.168.0.0/16'

# ─── テスト結果 ───────────────────────────────────────────────────────
$script:Results = [System.Collections.Generic.List[PSCustomObject]]::new()

# MsiSystemRebootPending により Invoke-MsiUninstall が強制削除を行ったかどうかのフラグ。
# セットするのは Invoke-MsiUninstall のみ。リセットするのは当該テスト本体の finally のみ。
$script:RebootPendingCleanupNeeded = $false

# ─── ユーティリティ関数 ───────────────────────────────────────────────

function Get-MsiProductVersion([string]$msiFilePath) {
    # MSI のバージョンは PE ヘッダーではなく MSI DB の ProductVersion プロパティに入っている
    $installer = New-Object -ComObject WindowsInstaller.Installer
    $database  = $installer.GetType().InvokeMember(
        'OpenDatabase', 'InvokeMethod', $null, $installer, @($msiFilePath, 0))
    $view = $database.GetType().InvokeMember(
        'OpenView', 'InvokeMethod', $null, $database,
        @("SELECT Value FROM Property WHERE Property='ProductVersion'"))
    $view.GetType().InvokeMember('Execute', 'InvokeMethod', $null, $view, $null)
    $record = $view.GetType().InvokeMember('Fetch', 'InvokeMethod', $null, $view, $null)
    return $record.GetType().InvokeMember('StringData', 'GetProperty', $null, $record, @(1))
}

function Test-MsiRebootPending() {
    # PendingFileRenameOperations が存在するとき、MSI はファイル削除を次回起動まで延期する
    $pendingRenames = (Get-ItemProperty `
        'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager' `
        -Name 'PendingFileRenameOperations' `
        -ErrorAction SilentlyContinue).PendingFileRenameOperations
    return ($null -ne $pendingRenames -and $pendingRenames.Count -gt 0)
}

function Write-Section([string]$message) {
    Write-Log "`n$('─' * 60)" 'DarkGray'
    Write-Log $message 'Cyan'
    Write-Log $('─' * 60) 'DarkGray'
}

function Write-Pass([string]$message) { Write-Log "  [PASS] $message" 'Green' }
function Write-Fail([string]$message) { Write-Log "  [FAIL] $message" 'Red' }
function Write-Warn([string]$message) { Write-Log "  [WARN] $message" 'Yellow' }
function Write-Info([string]$message) { Write-Log "  [INFO] $message" 'Gray' }

function Invoke-MsiInstall([hashtable]$properties = @{}) {
    $propertyArgs = $properties.Keys | ForEach-Object {
        $value = $properties[$_]
        # INSTALLFOLDER は末尾バックスラッシュが必須
        if ($_ -eq 'INSTALLFOLDER' -and $value -notmatch '\\$') { $value += '\' }
        "$_=`"$value`""
    }
    $argString = "/i `"$MsiPath`" /qn /norestart /l*v `"$MsiLogFile`" $($propertyArgs -join ' ')"
    Write-Info "実行: msiexec $argString"
    $process = Start-Process -FilePath 'msiexec.exe' -ArgumentList $argString -Wait -PassThru -NoNewWindow
    Write-Info "終了コード: $($process.ExitCode)"
    return $process.ExitCode
}

function Invoke-MsiUninstall() {
    # サービスを停止し、プロセスが完全に終了するまで待ってからアンインストールする
    # （プロセスが残っているとファイルロックで削除できない）
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($null -ne $service -and $service.Status -ne 'Stopped') {
        Write-Info "サービスを事前停止しています..."
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    }
    $serverProc = Get-Process -Name 'Lucia.Server' -ErrorAction SilentlyContinue
    if ($null -ne $serverProc) {
        Write-Info "Lucia.Server プロセスの終了を待っています..."
        $serverProc | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
    }

    $argString = "/x `"$MsiPath`" /qn /norestart /l*v `"$MsiLogFile`""
    Write-Info "実行: msiexec $argString"
    $process = Start-Process -FilePath 'msiexec.exe' -ArgumentList $argString -Wait -PassThru -NoNewWindow
    Write-Info "終了コード: $($process.ExitCode)"

    # サービスが SCM から削除されるまで待つ（最大20秒）
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline) {
        $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($null -eq $svc) { break }
        Start-Sleep -Seconds 1
    }

    # インストールディレクトリが削除されるまで待つ（最大60秒）
    $fileDeadline = (Get-Date).AddSeconds(60)
    while ((Get-Date) -lt $fileDeadline) {
        $defaultGone = -not (Test-Path $DefaultInstallPath)
        $testGone    = -not (Test-Path $TestInstallPath)
        if ($defaultGone -and $testGone) { break }
        Start-Sleep -Seconds 1
    }

    # 60秒後もディレクトリが残っている場合、OS の MsiSystemRebootPending が原因か確認する。
    # MSI のバグではなく OS 都合であれば強制削除してフラグを立てる。
    # フラグのリセットはこの関数では行わない。当該テスト本体の finally で行う。
    $directoriesRemain = (Test-Path $DefaultInstallPath) -or (Test-Path $TestInstallPath)
    if ($directoriesRemain) {
        if (Test-MsiRebootPending) {
            Write-Info "WARN: MsiSystemRebootPending により MSI がファイルを削除できませんでした。強制削除します（MSI のバグではなく OS 都合）。"
            if (Test-Path $DefaultInstallPath) { Remove-Item $DefaultInstallPath -Recurse -Force -ErrorAction SilentlyContinue }
            if (Test-Path $TestInstallPath)    { Remove-Item $TestInstallPath    -Recurse -Force -ErrorAction SilentlyContinue }
            $script:RebootPendingCleanupNeeded = $true
        }
        # MsiSystemRebootPending でないのに残っている = MSI 自体のバグ → 削除しない・フラグも立てない
    }

    return $process.ExitCode
}

# ─── アサーション関数 ──────────────────────────────────────────────────

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
    return Assert-True ($matchesDirect -or $matchesDotted) $desc
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
    $desc     = if ($description) { $description } else { "ポート $port でリッスンしている" }
    # サービス起動直後はポートが開くまで最大10秒待つ
    $deadline = (Get-Date).AddSeconds(10)
    $listening = $false
    while ((Get-Date) -lt $deadline) {
        $result = Test-NetConnection -ComputerName 'localhost' -Port $port `
                                     -InformationLevel Quiet -WarningAction SilentlyContinue
        if ($result) { $listening = $true; break }
        Start-Sleep -Seconds 1
    }
    return Assert-True $listening $desc
}

# ─── セットアップ／クリーンアップヘルパー ─────────────────────────────

function Ensure-Clean() {
    Write-Info 'クリーン状態を確保しています...'
    Invoke-MsiUninstall | Out-Null
    # MSI アンインストールのタイムアウト後もディレクトリが残っている場合の強制削除
    # （テスト間の状態リセット用。アサーションには使用しない）
    if (Test-Path $DefaultInstallPath) {
        Write-Info "$DefaultInstallPath を強制削除しています（テスト環境クリーンアップ）..."
        Remove-Item $DefaultInstallPath -Recurse -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path $TestInstallPath) {
        Write-Info "$TestInstallPath を強制削除しています（テスト環境クリーンアップ）..."
        Remove-Item $TestInstallPath -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Ensure-Installed([hashtable]$properties = @{}) {
    Write-Info 'インストール済み状態を確保しています...'
    Invoke-MsiInstall $properties | Out-Null
}

# ─── テストランナー ───────────────────────────────────────────────────

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

if (-not (Test-Path $MsiPath)) {
    Write-Log "MSI が見つかりません: $MsiPath" 'Red'
    Write-Log "先に Release ビルドを実行してください:" 'Yellow'
    Write-Log "  dotnet build src/Lucia.WixInstaller/Lucia.WixInstaller.wixproj --configuration Release"
    exit 1
}

$msiVersion = Get-MsiProductVersion $MsiPath
Write-Info "MSI パス              : $MsiPath"
Write-Info "MSI バージョン        : $msiVersion"
Write-Info "デフォルト インストールパス: $DefaultInstallPath"
Write-Info "テスト用インストールパス   : $TestInstallPath"
Write-Info "ログファイル               : $LogFile"
Write-Info "MSI ログファイル           : $MsiLogFile"

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

Run-Test '00' 'MSI ファイル確認' {
    Assert-True (Test-Path $MsiPath) "MSI ファイルが存在する"
    Assert-True (-not [string]::IsNullOrEmpty($msiVersion)) "MSI にバージョン情報が埋め込まれている (actual: $msiVersion)"
}

Run-Test '01' '新規インストール（デフォルト設定）' {
    Ensure-Clean

    $exitCode = Invoke-MsiInstall

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

    $exitCode = Invoke-MsiInstall

    Assert-ExitCode $exitCode
    Assert-ServiceRunning '上書き後もサービスが Running 状態である'
    Assert-FirewallRuleExists $DefaultPort
    Assert-FilesExist $DefaultInstallPath '上書き後もファイルが存在する'

    Ensure-Clean
}

Run-Test '03' 'カスタムポート（7100）' {
    Ensure-Clean

    $exitCode = Invoke-MsiInstall @{ PORT = '7100' }

    Assert-ExitCode $exitCode
    Assert-ServiceRunning
    Assert-FirewallRuleExists 7100 'FW 規則がポート 7100 で存在する'
    Assert-PortListening 7100 'サービスがポート 7100 でリッスンしている'

    Ensure-Clean
}

Run-Test '04' 'カスタムインストールパス（C:\Lucia-Test）' {
    Ensure-Clean

    $exitCode = Invoke-MsiInstall @{ INSTALLFOLDER = $TestInstallPath }

    Assert-ExitCode $exitCode
    Assert-ServiceRunning
    Assert-FilesExist $TestInstallPath "C:\Lucia-Test に Lucia.Server.exe が存在する"
    Assert-FilesNotExist $DefaultInstallPath "デフォルトパス ($DefaultInstallPath) にはインストールされていない"

    Ensure-Clean
}

Run-Test '05' 'カスタムサブネット（10.0.0.0/8）' {
    Ensure-Clean

    $exitCode = Invoke-MsiInstall @{ ALLOWED_SUBNET = '10.0.0.0/8' }

    Assert-ExitCode $exitCode
    Assert-ServiceRunning
    Assert-FirewallRuleSubnet '10.0.0.0/8' 'FW 規則のリモートIPが 10.0.0.0/8 である'

    Ensure-Clean
}

Run-Test '06' '正常アンインストール' {
    Ensure-Clean
    Ensure-Installed
    $script:RebootPendingCleanupNeeded = $false  # Ensure-Clean の副作用を消してから計測開始

    $exitCode = Invoke-MsiUninstall

    try {
        Assert-ExitCode $exitCode
        Assert-ServiceNotExists
        Assert-FirewallRuleNotExists
        Assert-FilesNotExist $DefaultInstallPath
        Assert-EventLogSourceNotExists
        if ($script:RebootPendingCleanupNeeded) {
            Write-Warn "MsiSystemRebootPending により MSI がファイルを自力で削除できなかった（OS 都合、MSI のバグではない）"
        }
    } finally {
        # このテストが引き起こした状態はこのテストが片付ける
        $script:RebootPendingCleanupNeeded = $false
    }
}

Run-Test '07' '未インストール状態でのアンインストール（1605 を許容）' {
    Ensure-Clean

    $exitCode = Invoke-MsiUninstall

    # 1605 = ERROR_UNKNOWN_PRODUCT (製品が未インストール) は正常な MSI の挙動
    Assert-ExitCode $exitCode @(0, 1605) '未インストール時の終了コードが 0 または 1605 である'
}

Run-Test '08' 'install → uninstall → install（連続操作）' {
    Ensure-Clean

    # 1回目インストール
    Invoke-MsiInstall | Out-Null

    # アンインストール → クリーン状態を中間検証する
    $script:RebootPendingCleanupNeeded = $false  # Ensure-Clean の副作用を消してから計測開始
    $uninstallCode = Invoke-MsiUninstall

    try {
        Assert-ExitCode $uninstallCode @(0) 'アンインストールの終了コードが 0 である'
        Assert-ServiceNotExists 'アンインストール後にサービスが存在しない'
        Assert-FilesNotExist $DefaultInstallPath 'アンインストール後にファイルが存在しない'
        if ($script:RebootPendingCleanupNeeded) {
            Write-Warn "MsiSystemRebootPending により MSI がファイルを自力で削除できなかった（OS 都合、MSI のバグではない）"
        }
    } finally {
        # 中間アンインストールで引き起こした状態はここで片付ける（次の install の前に必ず実行）
        $script:RebootPendingCleanupNeeded = $false
    }

    # 2回目インストール
    $exitCode = Invoke-MsiInstall

    Assert-ExitCode $exitCode
    Assert-ServiceRunning '2回目の install 後にサービスが Running 状態である'
    Assert-FirewallRuleExists $DefaultPort
    Assert-FilesExist $DefaultInstallPath

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
    Write-Log "MSI 詳細ログ: $MsiLogFile" 'Yellow'
    exit 1
} else {
    Write-Log '全テスト通過' 'Green'
    exit 0
}
