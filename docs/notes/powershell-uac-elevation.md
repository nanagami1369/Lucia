# PowerShell UAC 昇格パターン

## ログファイル経由の昇格パターン

管理者操作（サービス管理・Program Files への書き込み・ファイアウォール操作）には昇格が必要。
非昇格シェルから昇格 exe を直接 `&` で呼ぶと、プロセスが正しく追跡されず出力も exit code も消える。

```powershell
$LogFile = 'C:\Users\Public\my-script.log'

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Set-Content $LogFile -Value '' -Encoding UTF8
    $argList = "-ExecutionPolicy Bypass -File `"$PSCommandPath`""
    Start-Process pwsh -ArgumentList $argList -Verb RunAs -Wait
    Get-Content $LogFile -Encoding UTF8 | ForEach-Object { Write-Host $_ }
    exit
}

function Write-Log([string]$message) {
    Write-Host $message
    Add-Content $LogFile -Value $message -Encoding UTF8
}
```

**ポイント：**
- `Start-Process ... -Verb RunAs -Wait` で UAC 昇格しつつ完了を待機
- 昇格プロセスの出力を `C:\Users\Public\` 以下のログファイルに書き出す（全ユーザーから書き込み可能）
- 元プロセスがログを読んで表示することで、非昇格シェルから出力を確認できる

---

## `requireAdministrator` な exe の呼び出し

`app.manifest` に `requireAdministrator` を宣言した exe を PowerShell の `&` 演算子で呼ぶと：
- 非昇格シェルからの呼び出しで UAC ダイアログが裏で表示され、誰もクリックできない
- `$LASTEXITCODE` が空になり、ファイルは一切更新されない

**方法 A（スクリプト自身を昇格）**：ログファイル経由の昇格パターンを使い、スクリプト全体を昇格させてから `&` で呼ぶ。

**方法 B（`Start-Process -Verb RunAs`）**：出力不要なら直接昇格実行。

```powershell
$process = Start-Process $exePath -ArgumentList $args -Verb RunAs -Wait -PassThru
if ($process.ExitCode -ne 0) { ... }
```

---

## bash から pwsh を UAC 昇格で呼ぶ（ログファイル経由）

bash のシングルクォートで PowerShell スクリプトブロック全体を囲むと、bash の変数展開が起きないため Windows パスのバックスラッシュも PowerShell の `$変数` もそのまま渡せる。

```bash
pwsh -Command '
$LogFile    = "C:\Users\Public\my-test.log"
$TestScript = "C:\path\to\script.ps1"
Remove-Item $LogFile -ErrorAction SilentlyContinue
Start-Process pwsh -Verb RunAs -Wait -ArgumentList @("-ExecutionPolicy", "Bypass", "-File", $TestScript, "-LogFile", $LogFile)
if (Test-Path $LogFile) { Get-Content $LogFile -Encoding UTF8 } else { Write-Host "Log file not created" }
'
```
