using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.ServiceProcess;
using Cysharp.Diagnostics;
using Lucia.Installer.Models;
using Microsoft.Win32;

namespace Lucia.Installer.Services;

/// <summary>インストール・アンインストール処理の実装。</summary>
public class InstallService : IInstallService
{
    private const string ServiceName = "LuciaServer";
    private const string FirewallRuleName = "LuciaServer";
    private const string EventLogSource = "LuciaServer";
    private const string EventLogName = "Application";
    private const string RegistryUninstallKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Lucia";
    private const string InstallerFileName = "Lucia.Installer.exe";
    private const string ServerExecutableFileName = "Lucia.Server.exe";

    /// <inheritdoc />
    public async Task InstallAsync(InstallOptions options, IProgress<string> progress, CancellationToken cancellationToken = default)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";
        var installerPath = Path.Combine(options.InstallDirectory, InstallerFileName);
        var completedSteps = new Stack<Func<Task>>();
        try
        {
            // Step 1: レジストリ書き込み（先行登録）
            // 意図: 他サービス・他製品の設定を誤って破壊するリスクを避けるため、
            //       自分の存在をまず記録してから後続処理を行う。失敗時はリペアで対応。
            progress.Report("レジストリを更新しています...");
            WriteUninstallRegistry(options.InstallDirectory, installerPath, version, options.Port, options.AllowedSubnet);
            completedSteps.Push(() => { DeleteUninstallRegistry(); return Task.CompletedTask; });

            // Step 2: ファイル展開 + Lucia.Installer.exe 自身をインストール先に配置
            // リペア時にサービスがファイルをロックしているため、展開前に停止する
            progress.Report("ファイルを展開しています...");
            await StopServiceIfRunningAsync();
            ExtractBundle(options.InstallDirectory);
            CopyInstallerToInstallDirectory(options.InstallDirectory);
            completedSteps.Push(() => RemoveDirectoryAsync(options.InstallDirectory));

            // Step 3: Windows Service 登録・開始（既存なら sc config でリペア）
            progress.Report("Windows Service を設定しています...");
            var serviceExecutablePath = Path.Combine(options.InstallDirectory, ServerExecutableFileName);
            var serviceStartArguments = $"--urls http://0.0.0.0:{options.Port}";
            await RegisterOrRepairServiceAsync(serviceExecutablePath, serviceStartArguments);
            completedSteps.Push(RemoveServiceAsync);

            // Step 4: ファイアウォール規則（既存削除→再追加）
            progress.Report("ファイアウォール規則を設定しています...");
            await RemoveFirewallRuleAsync();
            await AddFirewallRuleAsync(options.Port, options.AllowedSubnet);
            completedSteps.Push(RemoveFirewallRuleAsync);

            // Step 5: イベントログソース登録（なければ作成）
            progress.Report("イベントログソースを確認しています...");
            if (!EventLog.SourceExists(EventLogSource))
            {
                EventLog.CreateEventSource(EventLogSource, EventLogName);
            }
            completedSteps.Push(() => { if (EventLog.SourceExists(EventLogSource)) EventLog.DeleteEventSource(EventLogSource); return Task.CompletedTask; });

            progress.Report("完了しました。");
        }
        catch
        {
            progress.Report("エラーが発生しました。ロールバックしています...");
            while (completedSteps.TryPop(out var rollback))
            {
                try { await rollback(); }
                catch { /* ロールバック失敗は無視して継続 */ }
            }
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UninstallAsync(string installDirectory, IProgress<string> progress, CancellationToken cancellationToken = default)
    {
        // Step 1: サービス停止・削除
        progress.Report("Windows Service を停止・削除しています...");
        await RemoveServiceAsync();

        // Step 2: ファイアウォール規則削除
        progress.Report("ファイアウォール規則を削除しています...");
        await RemoveFirewallRuleAsync();

        // Step 3: イベントログソース削除
        progress.Report("イベントログソースを削除しています...");
        if (EventLog.SourceExists(EventLogSource))
        {
            EventLog.DeleteEventSource(EventLogSource);
        }

        // Step 4: レジストリ削除
        // ファイル削除より先に行うことで、Lucia.Installer.exe 自身のロックでファイル削除が失敗しても
        // 設定アプリのインストール済み一覧から必ず消えるようにする。
        progress.Report("レジストリキーを削除しています...");
        DeleteUninstallRegistry();

        // Step 5: ファイル削除（失敗時は cmd.exe で遅延削除にフォールバック）
        // 自己アンインストール時は親プロセスが Lucia.Installer.exe をロックしているため直接削除できない場合がある。
        // その場合は親プロセス終了後に cmd.exe が削除する。
        progress.Report("ファイルを削除しています...");
        var filesDeleted = await TryRemoveDirectoryAsync(installDirectory);
        if (!filesDeleted)
        {
            ScheduleDirectoryDeletion(installDirectory);
        }

        progress.Report("アンインストールが完了しました。");
    }

    /// <inheritdoc />
    public async Task ModifyAsync(int port, string allowedSubnet, IProgress<string> progress, CancellationToken cancellationToken = default)
    {
        var options = ReadInstallOptionsFromRegistry()
            ?? throw new InvalidOperationException("レジストリにインストール情報が見つかりません。インストールが完了していない可能性があります。");

        var executablePath = Path.Combine(options.InstallDirectory, ServerExecutableFileName);
        var installerPath = Path.Combine(options.InstallDirectory, InstallerFileName);
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";

        // Step 1: サービス停止（実行中の場合のみ）
        progress.Report("Windows Service を停止しています...");
        await StopServiceIfRunningAsync();

        // Step 2: ファイアウォール規則更新（既存削除→新設定で再追加）
        // 先にFWを更新することで、レジストリ更新前に失敗してもレジストリの旧ポートを保ったままリトライできる
        progress.Report("ファイアウォール規則を更新しています...");
        await RemoveFirewallRuleAsync();
        await AddFirewallRuleAsync(port, allowedSubnet);

        // Step 3: サービス起動引数更新（ImagePath を直接書き込み）
        progress.Report("Windows Service の起動引数を更新しています...");
        var imagePath = $"\"{executablePath}\" --urls http://0.0.0.0:{port}";
        WriteServiceImagePath(imagePath);

        // Step 4: レジストリ更新（FW・ImagePath が確定してから書き込む）
        progress.Report("レジストリを更新しています...");
        WriteUninstallRegistry(options.InstallDirectory, installerPath, version, port, allowedSubnet);

        // Step 5: サービス起動
        progress.Report("Windows Service を起動しています...");
        await Task.Run(() =>
        {
            using var controller = new ServiceController(ServiceName);
            controller.Start();
            controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
        });

        progress.Report("変更が完了しました。");
    }

    /// <summary>EmbeddedResource の app-bundle.zip を展開して指定ディレクトリにコピーする。</summary>
    private static void ExtractBundle(string installDirectory)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("app-bundle.zip")
            ?? throw new InvalidOperationException(
                "app-bundle.zip が埋め込まれていません。Release ビルドを使用してください。");

        Directory.CreateDirectory(installDirectory);
        using var archive = new ZipArchive(stream);
        if (archive.Entries.Count == 0)
        {
            throw new InvalidOperationException(
                "app-bundle.zip が空です。Release ビルドを再実行してください。");
        }
        archive.ExtractToDirectory(installDirectory, overwriteFiles: true);
    }

    /// <summary>実行中の Lucia.Installer.exe をインストール先にコピーする。アンインストーラーとして兼用するため。</summary>
    private static void CopyInstallerToInstallDirectory(string installDirectory)
    {
        var currentExePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("インストーラーのパスを取得できませんでした。");
        var destinationPath = Path.Combine(installDirectory, InstallerFileName);
        File.Copy(currentExePath, destinationPath, overwrite: true);
    }

    /// <summary>
    /// Windows Service を登録または設定更新（リペア）して開始する。
    /// 既存サービスがある場合は sc config で設定を上書きし、なければ sc create する。
    /// sc.exe への引数引用符問題を回避するため、ImagePath はレジストリに直接書き込む。
    /// </summary>
    private static async Task RegisterOrRepairServiceAsync(string executablePath, string startArguments)
    {
        // sc.exe config / create でサービスを登録または更新する。
        // binPath= のクォート問題があるため、ImagePath は後でレジストリに直接上書きする。
        var placeholderBinPath = $"\"{executablePath}\"";
        try
        {
            await RunProcessAsync($"sc.exe config {ServiceName} binPath= {placeholderBinPath} start= auto DisplayName= \"Lucia Session Monitor\"");
        }
        catch
        {
            await RunProcessAsync($"sc.exe create {ServiceName} binPath= {placeholderBinPath} start= auto DisplayName= \"Lucia Session Monitor\"");
        }
        await RunProcessAsync($"sc.exe description {ServiceName} \"Lucia Windows RDS ホスト管理ダッシュボード サービス\"");

        // sc.exe の引用符エスケープに依存せず、ImagePath を直接レジストリに書き込む。
        // SCM は ImagePath の値をそのまま CreateProcess のコマンドラインとして使用する。
        // 実行ファイルパスを "..." で囲み、引数をスペース区切りで続ける形式が正しい。
        var imagePath = $"\"{executablePath}\" {startArguments}";
        WriteServiceImagePath(imagePath);

        // ServiceController で停止完了を確認してから起動する。
        // sc.exe stop は停止命令を送るだけで完了を待たないため競合しやすい。
        await Task.Run(() => StopAndStartService(executablePath));
    }

    /// <summary>
    /// SCM のサービスエントリに ImagePath を直接書き込む。
    /// sc.exe の引用符解釈に依存せず確実な形式で設定するために使用する。
    /// </summary>
    private static void WriteServiceImagePath(string imagePath)
    {
        using var serviceKey = Registry.LocalMachine.OpenSubKey(
            $@"SYSTEM\CurrentControlSet\Services\{ServiceName}", writable: true)
            ?? throw new InvalidOperationException($"サービスレジストリキーが見つかりません: SYSTEM\\CurrentControlSet\\Services\\{ServiceName}");
        serviceKey.SetValue("ImagePath", imagePath);
    }

    /// <summary>
    /// サービスが実行中の場合のみ停止して完了を待つ。存在しない・停止済みの場合は何もしない。
    /// ファイル展開前のロック解除に使用する。
    /// </summary>
    private static async Task StopServiceIfRunningAsync()
    {
        try
        {
            await Task.Run(() =>
            {
                using var controller = new ServiceController(ServiceName);
                if (controller.Status == ServiceControllerStatus.Stopped ||
                    controller.Status == ServiceControllerStatus.StopPending)
                {
                    return;
                }
                controller.Stop();
                controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            });
        }
        catch
        {
            // サービスが存在しない場合・停止失敗は無視する（初回インストール時は存在しない）
        }
    }

    /// <summary>
    /// サービスを停止してから起動する。停止完了を待ってから起動することで競合を防ぐ。
    /// </summary>
    private static void StopAndStartService(string executablePath)
    {
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException($"サービス実行ファイルが見つかりません: {executablePath}");
        }

        using var controller = new ServiceController(ServiceName);
        if (controller.Status != ServiceControllerStatus.Stopped &&
            controller.Status != ServiceControllerStatus.StopPending)
        {
            controller.Stop();
        }
        controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
        try
        {
            controller.Start();
        }
        catch (InvalidOperationException ex)
        {
            var win32Detail = ex.InnerException?.Message ?? "(詳細なし)";
            throw new InvalidOperationException(
                $"サービスの起動に失敗しました。\n  Win32 エラー: {win32Detail}\n  ImagePath: \"{executablePath}\"", ex);
        }
        controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
    }

    /// <summary>プロセスを実行し、失敗時はコマンド・終了コード・stderr を含むメッセージで例外をスローする。</summary>
    private static async Task RunProcessAsync(string command)
    {
        try
        {
            await ProcessX.StartAsync(command).WaitAsync();
        }
        catch (ProcessErrorException ex)
        {
            var stderr = ex.ErrorOutput is { Length: > 0 } output ? string.Join('\n', output) : "(stderr なし)";
            throw new InvalidOperationException(
                $"コマンド失敗 (ExitCode:{ex.ExitCode})\n  コマンド: {command}\n  stderr: {stderr}", ex);
        }
    }

    /// <summary>Windows Service を停止して削除する。</summary>
    private static async Task RemoveServiceAsync()
    {
        try { await ProcessX.StartAsync($"sc.exe stop {ServiceName}").WaitAsync(); } catch { /* 停止失敗は無視 */ }
        try { await ProcessX.StartAsync($"sc.exe delete {ServiceName}").WaitAsync(); } catch { /* 削除失敗は無視 */ }
    }

    /// <summary>ファイアウォール規則を追加する。</summary>
    private static async Task AddFirewallRuleAsync(int port, string allowedSubnet)
    {
        // netsh は 0.0.0.0/0 を解釈できないため Any に変換する
        var remoteIp = allowedSubnet == "0.0.0.0/0" ? "Any" : allowedSubnet;
        await ProcessX.StartAsync($"netsh advfirewall firewall add rule name=\"{FirewallRuleName}\" protocol=TCP localport={port} dir=in action=allow profile=private remoteip={remoteIp}").WaitAsync();
    }

    /// <summary>ファイアウォール規則を削除する。</summary>
    private static async Task RemoveFirewallRuleAsync()
    {
        try { await ProcessX.StartAsync($"netsh advfirewall firewall delete rule name=\"{FirewallRuleName}\"").WaitAsync(); } catch { /* 削除失敗は無視 */ }
    }

    /// <summary>アンインストール情報をレジストリの 64-bit ハイブに書き込む。</summary>
    private static void WriteUninstallRegistry(string installDirectory, string installerPath, string version, int port, string allowedSubnet)
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var uninstallKey = baseKey.CreateSubKey(RegistryUninstallKeyPath)
            ?? throw new InvalidOperationException("レジストリキーの作成に失敗しました。");

        uninstallKey.SetValue("DisplayName", "Lucia");
        uninstallKey.SetValue("DisplayVersion", version);
        uninstallKey.SetValue("Publisher", "nanagami1369");
        uninstallKey.SetValue("UninstallString", $"\"{installerPath}\" uninstall");
        uninstallKey.SetValue("ModifyPath", $"\"{installerPath}\" modify");
        uninstallKey.SetValue("InstallLocation", installDirectory);
        uninstallKey.SetValue("Port", port, RegistryValueKind.DWord);
        uninstallKey.SetValue("AllowedSubnet", allowedSubnet);
        uninstallKey.SetValue("NoModify", 0, RegistryValueKind.DWord);
        uninstallKey.SetValue("NoRepair", 0, RegistryValueKind.DWord);
    }

    /// <summary>
    /// レジストリから現在のインストール設定を読み取る。
    /// リペア時にインストール先・ポート・サブネットを復元するために使用する。
    /// </summary>
    /// <returns>読み取ったインストールオプション。レジストリが存在しない場合は null。</returns>
    public static InstallOptions? ReadInstallOptionsFromRegistry()
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var uninstallKey = baseKey.OpenSubKey(RegistryUninstallKeyPath);
        if (uninstallKey is null) return null;

        var installDirectory = uninstallKey.GetValue("InstallLocation") as string;
        var port = uninstallKey.GetValue("Port") as int?;
        var allowedSubnet = uninstallKey.GetValue("AllowedSubnet") as string;

        if (installDirectory is null || port is null || allowedSubnet is null) return null;

        return new InstallOptions
        {
            InstallDirectory = installDirectory,
            Port = port.Value,
            AllowedSubnet = allowedSubnet,
        };
    }

    /// <summary>アンインストール情報のレジストリキーを削除する。</summary>
    private static void DeleteUninstallRegistry()
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            baseKey.DeleteSubKeyTree(RegistryUninstallKeyPath, throwOnMissingSubKey: false);
        }
        catch { /* 削除失敗は無視 */ }
    }

    /// <summary>指定ディレクトリを再帰的に削除する。インストールのロールバック用。失敗時は例外を伝播する。</summary>
    private static async Task RemoveDirectoryAsync(string directory, int maxRetries = 3)
    {
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            if (!Directory.Exists(directory)) return;
            try
            {
                Directory.Delete(directory, recursive: true);
                return;
            }
            catch when (attempt < maxRetries - 1)
            {
                await Task.Delay(500);
            }
        }
    }

    /// <summary>
    /// 指定ディレクトリの削除を試みる。成功すれば true、失敗すれば false を返す（例外を伝播しない）。
    /// アンインストール用。失敗時は <see cref="ScheduleDirectoryDeletion"/> でフォールバックする。
    /// </summary>
    private static async Task<bool> TryRemoveDirectoryAsync(string directory, int maxRetries = 3)
    {
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            if (!Directory.Exists(directory)) return true;
            try
            {
                Directory.Delete(directory, recursive: true);
                return true;
            }
            catch
            {
                await Task.Delay(500);
            }
        }
        return false;
    }

    /// <summary>
    /// cmd.exe を使い、一定時間後にディレクトリを削除するよう委譲する。
    /// 自己アンインストール時に Lucia.Installer.exe が自身をロックして直接削除できない場合のフォールバック。
    /// 親プロセスが終了するまで待機してから削除する。
    /// </summary>
    private static void ScheduleDirectoryDeletion(string directory)
    {
        var escapedDir = directory.TrimEnd('\\');
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/C timeout /t 3 /nobreak > nul & rd /s /q \"{escapedDir}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        });
    }
}
