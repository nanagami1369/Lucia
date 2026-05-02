---
tags: c#,cli,consoleappframework
updated: 2026-05-03 00:00:00
---

# ConsoleAppFramework v5

## 概要

Cysharp 製のゼロオーバーヘッド CLI フレームワーク。ソースジェネレーターがすべてのコードを生成するため、ランタイム依存 DLL がなく NativeAOT にも対応する。

```bash
dotnet add package ConsoleAppFramework
```

`ConsoleApp` クラス自体もソースジェネレーターが生成するため、ビルド時に `using ConsoleAppFramework;` が解決されれば参照は完結する。IDE の LSP がビルド前に未解決と表示することがあるが、実際のコンパイルは通る。

---

## マルチコマンド構成（クラスベース）

ヘルプ文・エイリアス・パラメーター説明はメソッドの **XML doc コメント** で定義する。`[Option]` 属性は v4 の API で v5 には存在しない。

```csharp
// Program.cs
var app = ConsoleApp.Create();
app.Add<MyCommands>();
await app.RunAsync(args);

// コマンドクラス
internal class MyCommands
{
    /// <summary>何かをインストールします。</summary>
    /// <param name="port">-p, ポート番号</param>
    /// <param name="yes">確認をスキップ</param>
    public async Task Install(int port = 8080, bool yes = false) { ... }

    /// <summary>何かをアンインストールします。</summary>
    public async Task Uninstall(bool yes = false) { ... }
}
```

`<param>` の先頭に `-p,` のように書くとエイリアスとして登録される。

---

## パラメーター名の変換規則

メソッドのパラメーター名は自動で `--lower-kebab-case` に変換される。

| C# パラメーター | CLI オプション |
|---|---|
| `port` | `--port` |
| `installDir` | `--install-dir` |
| `allowedSubnet` | `--allowed-subnet` |

オプション名の比較は**大文字・小文字を区別しない**が、小文字で渡す方が高速。

---

## bool はフラグ

`bool` 型パラメーターはフラグ扱い。`--yes` と渡すだけで `true` になる（`--yes true` も可）。

---

## nullable で省略可能な int

`int?` にするとオプション未指定時に `null` が来る（現在値を維持したいときに使う）。

```csharp
public async Task Modify(int? port = null, string? subnet = null, bool yes = false) { ... }
```

---

## 終了コード

メソッドの戻り値で制御できる：
- `void` / `Task` → 例外なし時は 0
- `int` / `Task<int>` → その値が `Environment.ExitCode` になる
- 例外が伝播すると常に 1

`Environment.Exit(code)` を直接呼ぶこともできる（lambda 内で `return` の前後に配置）。

---

## `[Hidden]` で内部パラメーターをヘルプ非表示

```csharp
public async Task Uninstall(bool yes = false, [Hidden] string? source = null) { ... }
```

`--source` は `--help` に表示されなくなる。内部専用フラグに使う。

---

## ラムダ式は doc コメントが使えない

C# の仕様上、ラムダ式・ローカル関数は XML doc コメントをサポートしない。`app.Add("cmd", () => ...)` のラムダ方式ではヘルプの説明・エイリアスを設定できない。説明が必要な場合はクラスのメソッドを使う。

```csharp
// NG: doc コメントが機能しない
/// <summary>これは無視される</summary>
app.Add("install", (int port = 8080) => { ... });

// OK
app.Add<MyCommands>();

internal class MyCommands {
    /// <summary>説明がヘルプに表示される</summary>
    public void Install(int port = 8080) { ... }
}
```

---

## 不明コマンドの扱い

`app.Add<T>()` で root コマンド（`[Command("")]`）が登録されている場合、不明な引数が来ると root コマンドが実行されて **exit 0** になる。不明コマンドで exit 1 を返したい場合は ConsoleAppFramework に渡す前に事前チェックする。

```csharp
string[] known = ["install", "uninstall", "modify", "status", "--help", "-h", "--version"];
if (args.Length > 0 && !known.Contains(args[0]))
{
    Console.Error.WriteLine($"エラー: 不明なコマンド '{args[0]}'");
    Environment.Exit(1);
    return;
}
var app = ConsoleApp.Create();
app.Add<MyCommands>();
await app.RunAsync(args);
```

---

## `--key=value` 非サポート

ConsoleAppFramework v5 は `--key=value` 形式をパースしない。スペース区切りの `--key value` を使う。

```
# NG
MyApp.exe install --port=8080

# OK
MyApp.exe install --port 8080
```

スクリプトから呼ぶ場合も同様。`@('install', '--port=8080')` ではなく `@('install', '--port', '8080')` で渡す。
