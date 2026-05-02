---
tags: c#,windows,installer,dotnet
updated: 2026-05-03 00:00:00
---

# .NET アプリの自己コピーアンインストール方式

## 問題

インストール先に配置した `MyApp.Installer.exe` を同じ exe からアンインストールしようとすると、Windows のプロセスローダーが EXE ファイルをメモリマップして保持しているため、実行中は自分自身を含むディレクトリを削除できない（`Directory.Delete` が Access Denied で失敗する）。

---

## 解決策：自己コピー方式

1. 実行中の exe を `%TEMP%\myapp-uninstaller-<GUID>.exe` にコピー
2. TEMP コピーを `uninstall --source "<インストール先>"` で起動（stdout/stderr リダイレクト）
3. 元プロセスは TEMP コピーの完了を待機して exit code を転送して終了
4. TEMP コピーが実際の削除処理を実行（元プロセスはすでに終了してロック解放済み）

```csharp
// 元プロセス側（インストール先の exe が実行）
var tempPath = Path.Combine(Path.GetTempPath(), $"myapp-uninstaller-{Guid.NewGuid()}.exe");
File.Copy(Environment.ProcessPath!, tempPath, overwrite: true);

var psi = new ProcessStartInfo
{
    FileName = tempPath,
    Arguments = $"uninstall --source \"{installDir.TrimEnd('\\')}\"",
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
};
using var process = Process.Start(psi)!;
// stdout/stderr を並行読み取り（逐次読み取りはデッドロックの原因）
var outTask = Task.Run(async () => {
    string? line;
    while ((line = await process.StandardOutput.ReadLineAsync()) != null)
        Console.WriteLine(line);
});
var errTask = Task.Run(async () => {
    string? line;
    while ((line = await process.StandardError.ReadLineAsync()) != null)
        Console.Error.WriteLine(line);
});
await Task.WhenAll(outTask, errTask);
await process.WaitForExitAsync();
if (process.ExitCode != 0) Environment.Exit(process.ExitCode);

// TEMP コピー側（--source が存在する場合のみ実処理を実行）
if (source is not null)
{
    await UninstallAsync(source, progress);
}
```

---

## `PublishSingleFile=true` が必須

framework-dependent で SingleFile を使わない場合、`MyApp.Installer.exe` は native EXE wrapper であり、起動時に同じディレクトリの `MyApp.Installer.dll` を要求する。TEMP にはこの DLL が存在しないため TEMP コピーの起動が失敗する。

```xml
<!-- Release 時のみ有効にする -->
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <PublishSingleFile>true</PublishSingleFile>
  <SelfContained>false</SelfContained>
</PropertyGroup>
```

Debug ビルドでは SingleFile にならないため、自己コピー方式のアンインストールは Release ビルドでのみ動作する。

---

## レジストリ削除はファイル削除より先に行う

`UninstallString` 経由で呼ばれた場合（設定アプリからのアンインストール）、ファイル削除が失敗してもアプリ一覧から確実に消えるよう、レジストリキーはファイル削除の前に削除する。

```csharp
// ① サービス停止・削除
// ② FW 規則削除
// ③ イベントログソース削除
// ④ レジストリ削除 ← ファイルより先
// ⑤ ファイル削除（失敗してもレジストリはすでに消えている）
```

従来「レジストリを最後にすることで再実行可能にする」という設計もあるが、ファイルロックで削除失敗した場合に永遠にアプリ一覧に残り続けるため逆効果になる。

---

## ファイル削除失敗時の cmd.exe フォールバック

元プロセスが終了待機している間はロックが残る。そのまま削除を試みても失敗し続ける。フォールバックとして `cmd.exe` の遅延削除を使う。

```csharp
private static async Task<bool> TryRemoveDirectoryAsync(string directory, int maxRetries = 3)
{
    for (var attempt = 0; attempt < maxRetries; attempt++)
    {
        if (!Directory.Exists(directory)) return true;
        try { Directory.Delete(directory, recursive: true); return true; }
        catch { await Task.Delay(500); }
    }
    return false;
}

private static void ScheduleDirectoryDeletion(string directory)
{
    Process.Start(new ProcessStartInfo
    {
        FileName = "cmd.exe",
        Arguments = $"/C timeout /t 3 /nobreak > nul & rd /s /q \"{directory.TrimEnd('\\')}\"",
        UseShellExecute = false,
        CreateNoWindow = true,
    });
}

// 使い方
var deleted = await TryRemoveDirectoryAsync(installDir);
if (!deleted) ScheduleDirectoryDeletion(installDir);
```

`timeout /t 3` で 3 秒待機（元プロセスが終了してロックが解放されるのを待つ）してから `rd /s /q` でディレクトリを削除する。テスト環境ではロックが発生しないため `TryRemoveDirectoryAsync` が成功し `cmd.exe` は起動しない。
