using System.Diagnostics;

namespace Lucia.Installer.Installers;

/// <summary>
/// Windows ファイアウォールのルールを追加・削除するクラス。
/// netsh advfirewall コマンドを呼び出して設定を行う。
/// </summary>
public static class FirewallInstaller
{
    /// <summary>
    /// 指定ポートへのインバウンド TCP 接続を許可するファイアウォールルールを追加する。
    /// </summary>
    /// <param name="ruleName">追加するルールの表示名。</param>
    /// <param name="port">許可するローカルポート番号。</param>
    /// <param name="allowedSubnet">接続を許可するリモートIPレンジ（例: "192.168.0.0/16"）。</param>
    public static void AddRule(string ruleName, int port, string allowedSubnet)
    {
        RunNetsh(
            $"advfirewall firewall add rule " +
            $"name=\"{ruleName}\" " +
            $"dir=in action=allow protocol=TCP " +
            $"localport={port} " +
            $"remoteip={allowedSubnet} " +
            $"profile=private");
    }

    /// <summary>
    /// 指定名のファイアウォールルールを削除する。ルールが存在しない場合は何もしない。
    /// </summary>
    /// <param name="ruleName">削除するルールの表示名。</param>
    public static void RemoveRule(string ruleName)
    {
        // ルールが存在しない場合も正常終了とする（--silent 時のクリーンアップのため終了コードを無視）
        RunNetsh($"advfirewall firewall delete rule name=\"{ruleName}\"", ignoreExitCode: true);
    }

    /// <summary>
    /// netsh コマンドを実行する。
    /// </summary>
    /// <param name="arguments">netsh に渡す引数文字列。</param>
    /// <param name="ignoreExitCode">true のとき、0 以外の終了コードでも例外をスローしない。</param>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="ignoreExitCode"/> が false のとき、netsh が 0 以外の終了コードを返した場合にスローされる。
    /// </exception>
    private static void RunNetsh(string arguments, bool ignoreExitCode = false)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.Start();
        process.WaitForExit();

        if (!ignoreExitCode && process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            throw new InvalidOperationException(
                $"netsh が終了コード {process.ExitCode} で失敗しました。\n{error}");
        }
    }
}
