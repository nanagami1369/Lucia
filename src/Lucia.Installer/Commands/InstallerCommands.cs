using System.ServiceProcess;
using ConsoleAppFramework;
using Lucia.Installer.Installers;

namespace Lucia.Installer.Commands;

/// <summary>
/// Lucia インストーラーの CLI コマンド群。
/// </summary>
public class InstallerCommands
{
    /// <summary>Windows Service 名。</summary>
    private const string ServiceName = "LuciaServer";

    /// <summary>ファイアウォールルール名。</summary>
    private const string FirewallRuleName = "LuciaServer";

    /// <summary>
    /// Lucia を Windows Service としてインストールする。
    /// 既存のサービスがある場合は停止・削除してから再インストールする。
    /// </summary>
    /// <param name="port">-p, サービスがバインドするポート番号。</param>
    /// <param name="installPath">-i, ファイルを配置するインストール先ディレクトリ。</param>
    /// <param name="allowedSubnet">-s, ファイアウォールで接続を許可するIPレンジ（CIDR表記）。</param>
    /// <param name="silent">確認プロンプトを表示せずに即座にインストールを実行する。</param>
    [Command("install")]
    public void Install(
        int port = 6100,
        string installPath = @"C:\Program Files\Lucia",
        string allowedSubnet = "192.168.0.0/16",
        bool silent = false)
    {
        Console.WriteLine("=== Lucia インストール ===");
        Console.WriteLine($"  インストール先: {installPath}");
        Console.WriteLine($"  ポート        : {port}");
        Console.WriteLine($"  許可サブネット: {allowedSubnet}");

        if (!silent && !ConfirmContinue("インストールを開始します。続行しますか？")) {
            return;
        }

        var exePath = Path.Combine(installPath, "Lucia.Server.exe");
        var serviceBinaryPath = $"\"{exePath}\" --urls http://0.0.0.0:{port}";

        Console.WriteLine("\n[1/5] 既存サービスを停止・削除しています...");
        var existingService = ServiceInstaller.FindService(ServiceName);
        if (existingService is not null)
        {
            Console.WriteLine($"  既存のサービス '{ServiceName}' を停止・削除しています...");
            StopServiceIfRunning(existingService);
            ServiceInstaller.Delete(ServiceName);
        }

        Console.WriteLine("[2/5] ファイルを展開しています...");
        BundleExtractor.ExtractTo(installPath);

        Console.WriteLine("[3/5] イベントログソースを登録しています...");
        EventLogInstaller.Register(ServiceName);

        Console.WriteLine("[4/5] Windows Service を登録しています...");
        ServiceInstaller.Create(ServiceName, serviceBinaryPath);

        Console.WriteLine("  サービスを起動しています...");
        using var service = new ServiceController(ServiceName);
        service.Start();
        service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));

        Console.WriteLine("[5/5] ファイアウォールルールを設定しています...");
        FirewallInstaller.RemoveRule(FirewallRuleName);
        FirewallInstaller.AddRule(FirewallRuleName, port, allowedSubnet);

        Console.WriteLine($"\n=== インストール完了 ===");
        Console.WriteLine($"  URL: http://localhost:{port}");
    }

    /// <summary>
    /// Lucia の Windows Service をアンインストールする。
    /// サービスの停止・削除、ファイアウォールルールの削除、ファイルの削除を行う。
    /// </summary>
    /// <param name="installPath">-i, アンインストール対象のインストール先ディレクトリ。</param>
    /// <param name="silent">確認プロンプトを表示せずに即座にアンインストールを実行する。</param>
    [Command("uninstall")]
    public void Uninstall(
        string installPath = @"C:\Program Files\Lucia",
        bool silent = false)
    {
        Console.WriteLine("=== Lucia アンインストール ===");
        Console.WriteLine($"  インストール先: {installPath}");

        if (!silent && !ConfirmContinue("アンインストールを実行します。続行しますか？"))
            return;

        Console.WriteLine("\n[1/4] Windows Service を停止・削除しています...");
        var existingService = ServiceInstaller.FindService(ServiceName);
        if (existingService is not null)
        {
            StopServiceIfRunning(existingService);
            ServiceInstaller.Delete(ServiceName);
        }
        else
        {
            Console.WriteLine($"  警告: サービス '{ServiceName}' が見つかりませんでした。スキップします。");
        }

        Console.WriteLine("[2/4] ファイアウォールルールを削除しています...");
        FirewallInstaller.RemoveRule(FirewallRuleName);

        Console.WriteLine("[3/4] イベントログソースを削除しています...");
        EventLogInstaller.Unregister(ServiceName);

        Console.WriteLine("[4/4] ファイルを削除しています...");
        if (Directory.Exists(installPath))
            Directory.Delete(installPath, recursive: true);
        else
            Console.WriteLine($"  警告: ディレクトリ '{installPath}' が見つかりませんでした。スキップします。");

        Console.WriteLine("\n=== アンインストール完了 ===");
    }

    /// <summary>
    /// サービスが実行中の場合に停止して停止完了を待機する。
    /// </summary>
    /// <param name="service">対象の <see cref="ServiceController"/>。</param>
    private static void StopServiceIfRunning(ServiceController service)
    {
        if (service.Status == ServiceControllerStatus.Running)
            service.Stop();
        service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// ユーザーに [y/N] の確認を求め、"y" を入力した場合のみ true を返す。
    /// </summary>
    /// <param name="message">表示する確認メッセージ。</param>
    /// <returns>ユーザーが "y" を入力した場合は true、それ以外は false。</returns>
    private static bool ConfirmContinue(string message)
    {
        Console.Write($"{message} [y/N] ");
        var answer = Console.ReadLine();
        if (!string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("キャンセルしました。");
            return false;
        }
        return true;
    }
}
