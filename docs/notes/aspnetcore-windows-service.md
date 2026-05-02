---
tags: aspnetcore,c#,windows-service,installer
updated: 2026-05-03 00:00:00
---

# ASP.NET Core を Windows Service として動作させる

## 必要パッケージ

```xml
<PackageReference Include="Microsoft.Extensions.Hosting.WindowsServices" Version="10.0.5" />
```

## 設定

```csharp
var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
    Args = args,
    // Windows Service として実行するとき、カレントディレクトリが
    // System32 等になるため、ContentRootPath を明示的に指定する
    ContentRootPath = AppContext.BaseDirectory,
});

// Windows Service 対応を有効化
builder.Host.UseWindowsService();
```

## ContentRootPath の重要性

`UseWindowsService()` を使っても `ContentRootPath` を指定しないと、
サービス起動時のカレントディレクトリ（通常 `C:\Windows\System32`）が使われる。
`wwwroot` や `appsettings.json` が見つからなくなるため **必ず `AppContext.BaseDirectory` を指定**すること。

## Windows Service の登録

```csharp
// sc コマンドで登録
// binPath にはポート番号などの起動引数も含める
sc create MyApp binPath= "\"C:\Program Files\MyApp\MyApp.exe\" --urls http://0.0.0.0:6100"
```

C# から登録する場合は `ServiceInstaller.Create()` を実装するか、
`System.ServiceProcess.ServiceController` + `sc` コマンドを組み合わせる。

## イベントログへの出力

本番環境（非 Development）でのみ Windows イベントログに出力する：

```csharp
if (!builder.Environment.IsDevelopment()) {
    builder.Logging.AddEventLog(settings => {
        settings.SourceName = "MyApp";
        settings.LogName = "Application";
    });
}
```

イベントログソースの事前登録が必要（管理者権限必須）：

```csharp
// インストーラー側で実行
if (!EventLog.SourceExists(sourceName)) {
    EventLog.CreateEventSource(sourceName, logName);
}
```

## ConsoleAppFramework v5 でのインストーラー CLI 設計

詳細は `consoleappframework-v5.md` を参照。サービス登録と組み合わせる場合の最小構成：

```csharp
// Program.cs（引数なし = ステータス表示、不明コマンドは事前チェック）
if (args.Length == 0) { ShowStatus(); return; }

string[] known = ["install", "uninstall", "modify", "status", "--help", "-h", "--version"];
if (!known.Contains(args[0])) {
    Console.Error.WriteLine($"不明なコマンド: {args[0]}");
    Environment.Exit(1); return;
}

var app = ConsoleApp.Create();
app.Add<InstallerCommands>();
await app.RunAsync(args);

// コマンドクラス（メソッド名が自動で kebab-case コマンド名になる）
internal class InstallerCommands {
    /// <summary>MyApp をインストールします。</summary>
    /// <param name="port">ポート番号</param>
    /// <param name="yes">確認プロンプトをスキップ</param>
    public async Task Install(int port = 8080, bool yes = false) { ... }

    /// <summary>MyApp をアンインストールします。</summary>
    /// <param name="yes">確認プロンプトをスキップ</param>
    /// <param name="source">内部専用: TEMP コピー実行時のインストール先</param>
    public async Task Uninstall(bool yes = false, [Hidden] string? source = null) { ... }
}
```

**重要な注意点：**
- `--key=value` 形式は**サポートしない**。`--key value` を使う
- `app.Add<T>()` を使う場合、ラムダ式の doc コメントは機能しない（C# 仕様上の制限）。クラスのメソッドに書く
- ConsoleAppFramework に不明コマンドを渡すと、root コマンドが呼ばれて exit 0 になる。exit 1 が必要なら上記のように事前チェックする
