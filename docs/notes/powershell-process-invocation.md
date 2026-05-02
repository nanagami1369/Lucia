---
tags: powershell,windows
updated: 2026-05-03 00:00:00
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

## stdout/stderr 同時読み取りのデッドロック回避

`ReadToEnd()` で stdout と stderr を順番に読むと、一方のバッファが満杯になり相手の読み取りを待つデッドロックが発生する。一時ファイルへのリダイレクト方式を使う。

```powershell
function Invoke-Exe([string]$exePath, [string[]]$arguments) {
    $argString = ($arguments | ForEach-Object {
        if ($_ -match '[\s"]') { '"' + $_.TrimEnd('\').Replace('"', '\"') + '"' }
        else { $_ }
    }) -join ' '

    $stdoutFile = [System.IO.Path]::GetTempFileName()
    $stderrFile = [System.IO.Path]::GetTempFileName()
    try {
        $process = Start-Process -FilePath $exePath `
                                 -ArgumentList $argString `
                                 -Wait -PassThru -NoNewWindow `
                                 -RedirectStandardOutput $stdoutFile `
                                 -RedirectStandardError  $stderrFile
        $stdout = Get-Content $stdoutFile -Encoding UTF8 -ErrorAction SilentlyContinue
        $stderr = Get-Content $stderrFile -Encoding UTF8 -ErrorAction SilentlyContinue
        return $process.ExitCode
    } finally {
        Remove-Item $stdoutFile, $stderrFile -ErrorAction SilentlyContinue
    }
}
```

**ポイント：**
- `Start-Process -Wait` + `-RedirectStandard*` の組み合わせで、プロセス完了後に一括読み取り
- `Start-Process` はデフォルトで `-NoNewWindow` を指定しないと新しいウィンドウが開く
- `Start-Process` は `-Wait` なしだと `$process.ExitCode` が取得できない

---

## ConsoleAppFramework v5 の引数形式

ConsoleAppFramework v5 は `--key=value` 形式を**サポートしない**。必ず `--key value`（スペース区切り）を使う。

```powershell
# NG
@('command', '--option=value')

# OK
@('command', '--option', 'value')
```
