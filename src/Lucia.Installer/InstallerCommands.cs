using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using ConsoleAppFramework;
using Lucia.Installer.Models;
using Lucia.Installer.Services;

namespace Lucia.Installer;

/// <summary>Lucia インストーラーのコマンド定義。</summary>
internal class InstallerCommands
{
    /// <summary>Lucia をインストールします。</summary>
    /// <param name="installDir">インストール先ディレクトリ</param>
    /// <param name="port">サービスのポート番号（1〜65535）</param>
    /// <param name="subnet">ファイアウォールの接続元 IP 制限（IPv4 CIDR 形式）</param>
    /// <param name="yes">確認プロンプトをスキップ</param>
    public async Task Install(
        string installDir = @"C:\Program Files\Lucia\",
        int port = 6100,
        string subnet = "192.168.0.0/16",
        bool yes = false)
    {
        if (port < 1 || port > 65535)
        {
            ConsoleApp.LogError("--port には 1〜65535 の整数を指定してください。");
            Environment.Exit(1);
            return;
        }
        if (!IsValidCidr(subnet))
        {
            ConsoleApp.LogError("--subnet には IPv4 CIDR 形式（例: 192.168.0.0/16）を指定してください。");
            Environment.Exit(1);
            return;
        }

        var options = new InstallOptions
        {
            InstallDirectory = installDir,
            Port = port,
            AllowedSubnet = subnet,
        };

        Console.WriteLine($"インストール先 : {options.InstallDirectory}");
        Console.WriteLine($"ポート番号     : {options.Port}");
        Console.WriteLine($"サブネット     : {options.AllowedSubnet}");
        Console.WriteLine();

        if (!ConfirmAction(yes, "上記の設定でインストールを開始しますか？"))
        {
            Console.WriteLine("キャンセルしました。");
            return;
        }

        try
        {
            var installService = new InstallService();
            var progress = new Progress<string>(message => Console.WriteLine(message));
            await installService.InstallAsync(options, progress);
        }
        catch (Exception ex)
        {
            ConsoleApp.LogError($"インストール失敗: {ex.Message}");
            Environment.Exit(2);
        }
    }

    /// <summary>Lucia をアンインストールします。</summary>
    /// <param name="yes">確認プロンプトをスキップ</param>
    /// <param name="source">内部専用: TEMP コピー実行時のインストール先</param>
    public async Task Uninstall(bool yes = false, [Hidden] string? source = null)
    {
        // --source は内部専用フラグ: TEMP コピーが実際の削除処理を実行するために使用する
        if (source is not null)
        {
            try
            {
                var installService = new InstallService();
                var progress = new Progress<string>(message => Console.WriteLine(message));
                await installService.UninstallAsync(source, progress);
            }
            catch (Exception ex)
            {
                ConsoleApp.LogError($"アンインストール失敗: {ex.Message}");
                Environment.Exit(2);
            }
            return;
        }

        var installOptions = InstallService.ReadInstallOptionsFromRegistry();
        if (installOptions is null)
        {
            ConsoleApp.LogError("Lucia はインストールされていません。");
            Environment.Exit(1);
            return;
        }

        Console.WriteLine($"インストール先: {installOptions.InstallDirectory}");
        Console.WriteLine();

        if (!ConfirmAction(yes, "Lucia をアンインストールしますか？"))
        {
            Console.WriteLine("キャンセルしました。");
            return;
        }

        // 自己コピー方式: 元プロセスがインストール先の exe をロックしたままでは削除できないため、
        // TEMP に自己コピーして委譲し、TEMP コピーがインストール先を削除する。
        var currentExePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("現在のプロセスパスを取得できませんでした。");
        var tempPath = Path.Combine(Path.GetTempPath(), $"lucia-uninstaller-{Guid.NewGuid()}.exe");
        File.Copy(currentExePath, tempPath, overwrite: true);

        var sourceArg = Path.TrimEndingDirectorySeparator(installOptions.InstallDirectory);
        var processStartInfo = new ProcessStartInfo
        {
            FileName = tempPath,
            Arguments = $"uninstall --source \"{sourceArg}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(processStartInfo)!;
        var outputTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardOutput.ReadLineAsync()) != null)
                Console.WriteLine(line);
        });
        var errorTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardError.ReadLineAsync()) != null)
                Console.Error.WriteLine(line);
        });
        await Task.WhenAll(outputTask, errorTask);
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) Environment.Exit(process.ExitCode);
    }

    /// <summary>設定を変更します（ポート・サブネット）。</summary>
    /// <param name="port">新しいポート番号（省略時は現在の設定を維持）</param>
    /// <param name="subnet">新しいサブネット（省略時は現在の設定を維持）</param>
    /// <param name="yes">確認プロンプトをスキップ</param>
    public async Task Modify(int? port = null, string? subnet = null, bool yes = false)
    {
        var currentOptions = InstallService.ReadInstallOptionsFromRegistry();
        if (currentOptions is null)
        {
            ConsoleApp.LogError("Lucia はインストールされていません。");
            Environment.Exit(1);
            return;
        }

        var newPort = port ?? currentOptions.Port;
        var newSubnet = subnet ?? currentOptions.AllowedSubnet;

        if (newPort < 1 || newPort > 65535)
        {
            ConsoleApp.LogError("--port には 1〜65535 の整数を指定してください。");
            Environment.Exit(1);
            return;
        }
        if (!IsValidCidr(newSubnet))
        {
            ConsoleApp.LogError("--subnet には IPv4 CIDR 形式（例: 192.168.0.0/16）を指定してください。");
            Environment.Exit(1);
            return;
        }

        Console.WriteLine($"ポート番号 : {currentOptions.Port} → {newPort}");
        Console.WriteLine($"サブネット : {currentOptions.AllowedSubnet} → {newSubnet}");
        Console.WriteLine();

        if (!ConfirmAction(yes, "上記の設定で変更を適用しますか？"))
        {
            Console.WriteLine("キャンセルしました。");
            return;
        }

        try
        {
            var installService = new InstallService();
            var progress = new Progress<string>(message => Console.WriteLine(message));
            await installService.ModifyAsync(newPort, newSubnet, progress);
        }
        catch (Exception ex)
        {
            ConsoleApp.LogError($"設定変更失敗: {ex.Message}");
            Environment.Exit(2);
        }
    }

    /// <summary>現在のインストール状態を表示します。</summary>
    public void Status()
    {
        var options = InstallService.ReadInstallOptionsFromRegistry();
        if (options is not null)
        {
            Console.WriteLine("インストール状態: インストール済み");
            Console.WriteLine($"  インストール先: {options.InstallDirectory}");
            Console.WriteLine($"  ポート番号    : {options.Port}");
            Console.WriteLine($"  サブネット    : {options.AllowedSubnet}");
        }
        else
        {
            Console.WriteLine("インストール状態: 未インストール");
        }
    }

    /// <summary>--yes フラグがある場合は即座に true を返し、ない場合はユーザーに確認を求める。</summary>
    private static bool ConfirmAction(bool yes, string prompt)
    {
        if (yes) return true;
        Console.Write($"{prompt} [y/N]: ");
        var input = Console.ReadLine()?.Trim().ToLowerInvariant();
        return input == "y" || input == "yes";
    }

    /// <summary>IPv4 CIDR 表記として正しいかどうかを検証する。</summary>
    private static bool IsValidCidr(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var parts = value.Split('/');
        if (parts.Length != 2) return false;
        if (!IPAddress.TryParse(parts[0], out var address)) return false;
        if (address.AddressFamily != AddressFamily.InterNetwork) return false;
        if (!int.TryParse(parts[1], out var prefix)) return false;
        return prefix >= 0 && prefix <= 32;
    }
}
