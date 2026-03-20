# Windows Firewall と PowerShell

## ファイアウォールルールの検索

`netsh advfirewall` で作成したルールを `Get-NetFirewallRule -Name` で取得できない。

`-Name` は内部 ID を検索する。`netsh` は**表示名**でルールを作成するため `-DisplayName` を使う。

```powershell
# NG
Get-NetFirewallRule -Name 'MyApp'

# OK
Get-NetFirewallRule -DisplayName 'MyApp' -ErrorAction SilentlyContinue
```

---

## サブネット表記：CIDR とドット表記

CIDR 表記（例: `10.0.0.0/8`）でファイアウォールルールを作成すると、Windows Firewall は内部でドット表記（`10.0.0.0/255.0.0.0`）に変換して保存する。そのまま文字列比較すると失敗する。

CIDR プレフィックス長をドット表記に変換して両方で比較する。

```powershell
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

# 比較時：CIDR とドット表記の両方を試みる
$rule = Get-NetFirewallRule -DisplayName 'MyApp' -ErrorAction SilentlyContinue
$addressFilter = $rule | Get-NetFirewallAddressFilter
$actual = ($addressFilter.RemoteAddress | ForEach-Object { $_.ToString() }) -join ','

$matchesDirect = $actual -like "*$expectedSubnet*"
$matchesDotted = $false
if ($expectedSubnet -match '^(.+)/(\d+)$') {
    $networkAddr = $Matches[1]
    $dottedMask  = ConvertTo-DottedMask ([int]$Matches[2])
    $matchesDotted = $actual -like "*$networkAddr/$dottedMask*"
}
$matched = $matchesDirect -or $matchesDotted
```

**注意：** PowerShell の `-shl` 演算子は符号付き整数として動作するため、`0xFFFFFFFF -shl N` は負値になる。ビット演算ではなくバイト単位のループで計算すること。
