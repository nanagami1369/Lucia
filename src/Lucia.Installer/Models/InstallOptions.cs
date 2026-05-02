namespace Lucia.Installer.Models;

/// <summary>インストール設定パラメータ。</summary>
public class InstallOptions
{
    /// <summary>インストール先ディレクトリの絶対パス。</summary>
    public string InstallDirectory { get; set; } = @"C:\Program Files\Lucia\";

    /// <summary>サービスが待ち受けるポート番号（1〜65535）。</summary>
    public int Port { get; set; } = 6100;

    /// <summary>ファイアウォールの接続元を制限する IPv4 CIDR 表記のサブネット。</summary>
    public string AllowedSubnet { get; set; } = "192.168.0.0/16";
}
