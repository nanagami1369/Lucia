---
tags: aspnetcore,c#,windows-service,installer
updated: 2026-03-20 13:04:20
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

```csharp
// Program.cs
var app = ConsoleApp.Create();
app.Add<InstallerCommands>();
app.Run(args);

// コマンドクラス
public class InstallerCommands {
    [Command("install")]
    public void Install(
        int port = 8080,
        string installPath = @"C:\Program Files\MyApp",
        string allowedSubnet = "192.168.0.0/16",
        bool silent = false) { ... }

    [Command("uninstall")]
    public void Uninstall(
        string installPath = @"C:\Program Files\MyApp",
        bool silent = false) { ... }
}
```

呼び出し：
```bash
MyApp.Installer.exe install --port 8080 --install-path "C:\Program Files\MyApp" --silent
```

**注意：** ConsoleAppFramework v5 は `--key=value` 形式を**サポートしない**。`--key value` を使う。
