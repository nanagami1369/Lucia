---
tags: powershell,windows
updated: 2026-03-20 13:04:20
---

# PowerShell プロセス起動・引数渡し

## スペースを含むパスの引数渡し

`C:\Program Files\App` のようなパスを `ProcessStartInfo.Arguments` に渡すと、スペースで分割されて別引数と解釈される。

```powershell
function Invoke-Exe([string]$exePath, [string[]]$arguments) {
    $argString = ($arguments | ForEach-Object {
        if ($_ -match ' ') { "`"$_`"" } else { $_ }
    }) -join ' '

    $psi = [System.Diagnostics.ProcessStartInfo]::new($exePath)
    $psi.Arguments              = $argString
    $psi.UseShellExecute        = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError  = $true
    $psi.CreateNoWindow         = $true

    $process = [System.Diagnostics.Process]::Start($psi)
    $stdout  = $process.StandardOutput.ReadToEnd()
    $stderr  = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    return $process.ExitCode
}

# 呼び出し側：引数を配列の別要素として渡す
Invoke-Exe $exePath @('install', '--path', 'C:\Program Files\App', '--silent')
```

**ポイント：**
- 配列要素を個別に渡し、スペースを含む要素のみ `"..."` でクォートしてから結合
- `Start-Process -ArgumentList @(...)` はスペースを自動クォートしないため `ProcessStartInfo` を使う

---

## ConsoleAppFramework v5 の引数形式

ConsoleAppFramework v5 は `--key=value` 形式を**サポートしない**。必ず `--key value`（スペース区切り）を使う。

```powershell
# NG
@('command', '--option=value')

# OK
@('command', '--option', 'value')
```
