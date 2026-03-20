# PowerShell Tips

## 文字コード：pwsh vs powershell

`powershell`（Windows PowerShell 5.x）でスクリプトを実行すると日本語が文字化けする。

**必ず `pwsh`（PowerShell 7）を使う。**

```bash
# NG
powershell -ExecutionPolicy Bypass -File script.ps1

# OK
pwsh -ExecutionPolicy Bypass -File script.ps1
```

---

## ZIP 展開とファイルタイムスタンプ

`ZipArchive.ExtractToDirectory` でファイルを展開すると、展開後のファイルは ZIP 内に記録されたタイムスタンプ（例: コンパイル時刻）のまま保持される。展開時刻にはならない。

「デプロイ後もファイルタイムスタンプが古い」のは正常動作。デプロイ成否の判断にはファイルタイムスタンプではなく**プロセス起動時刻**を使う。

```powershell
$wmiProcess = Get-WmiObject Win32_Process -Filter "Name='MyApp.exe'"
$startTime = [System.Management.ManagementDateTimeConverter]::ToDateTime($wmiProcess.CreationDate)
Write-Host "Process Started: $startTime"
```

---

## インテグレーションテストスクリプトの構成パターン

```powershell
#Requires -RunAsAdministrator

param(
    [string]$TargetExe = '...',
    [string]$LogFile   = 'C:\Users\Public\my-test.log'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ─── ログ ────────────────────────────────────────────────────
function Write-Log([string]$message, [string]$color = 'White') {
    Write-Host $message -ForegroundColor $color
    Add-Content -Path $LogFile -Value $message -Encoding UTF8
}
function Write-Pass([string]$msg) { Write-Log "  [PASS] $msg" 'Green' }
function Write-Fail([string]$msg) { Write-Log "  [FAIL] $msg" 'Red' }
function Write-Info([string]$msg) { Write-Log "  [INFO] $msg" 'Gray' }

# ─── アサーション ────────────────────────────────────────────
function Assert-True([bool]$condition, [string]$description) {
    if ($condition) { Write-Pass $description; return $true }
    else            { Write-Fail $description; return $false }
}

# ─── テストランナー ──────────────────────────────────────────
$script:Results = [System.Collections.Generic.List[PSCustomObject]]::new()

function Run-Test([string]$number, [string]$name, [scriptblock]$body) {
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
    $script:Results.Add([PSCustomObject]@{ No = $number; Name = $name; Passed = $passed })
}

# ─── テスト ──────────────────────────────────────────────────
Run-Test '01' 'シナリオ名' {
    # セットアップ
    # 操作
    # アサーション
    Assert-True ($someCondition) '期待する状態の説明'
}

# ─── サマリー ────────────────────────────────────────────────
$failed = $script:Results | Where-Object { -not $_.Passed }
foreach ($r in $script:Results) {
    $mark  = if ($r.Passed) { '[PASS]' } else { '[FAIL]' }
    $color = if ($r.Passed) { 'Green' }  else { 'Red' }
    Write-Log ("  {0} Test {1} : {2}" -f $mark, $r.No, $r.Name) $color
}
Write-Log ("合計: {0} / {1} 通過" -f ($script:Results.Count - $failed.Count), $script:Results.Count)
if ($failed.Count -gt 0) { exit 1 } else { exit 0 }
```
