using Lucia.Installer.Models;

namespace Lucia.Installer.Services;

/// <summary>インストール・アンインストール処理のインターフェース。</summary>
public interface IInstallService
{
    /// <summary>
    /// インストール処理を実行する。失敗時は完了済みステップを逆順でロールバックする。
    /// </summary>
    /// <param name="options">インストール設定パラメータ。</param>
    /// <param name="progress">進捗メッセージの受信先。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task InstallAsync(InstallOptions options, IProgress<string> progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// アンインストール処理を実行する。
    /// </summary>
    /// <param name="installDirectory">アンインストール対象のインストール先ディレクトリ。</param>
    /// <param name="progress">進捗メッセージの受信先。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task UninstallAsync(string installDirectory, IProgress<string> progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// 設定変更処理を実行する。ポート・サブネットを変更してサービスを再起動する。ファイルは触らない。
    /// </summary>
    /// <param name="port">新しいサービスポート番号（1〜65535）。</param>
    /// <param name="allowedSubnet">新しい接続元制限サブネット（IPv4 CIDR 形式）。</param>
    /// <param name="progress">進捗メッセージの受信先。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task ModifyAsync(int port, string allowedSubnet, IProgress<string> progress, CancellationToken cancellationToken = default);
}
