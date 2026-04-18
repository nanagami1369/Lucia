using System.Net;
using WixToolset.Dtf.WindowsInstaller;

/// <summary>インストーラーのカスタムバリデーションアクション。</summary>
public class CustomActions
{
    /// <summary>
    /// ポート番号と許可サブネットの入力値を検証する。
    /// 検証失敗時は VALIDATION_ERROR_MSG プロパティにエラーメッセージをセットし、CONFIG_VALID を "0" にする。
    /// </summary>
    /// <param name="session">MSI セッション。プロパティの読み書きに使用する。</param>
    /// <returns>常に <see cref="ActionResult.Success"/> を返す。エラーは CONFIG_VALID プロパティで通知する。</returns>
    [CustomAction]
    public static ActionResult ValidateConfig(Session session)
    {
        session["CONFIG_VALID"] = "1";

        if (!IsValidPort(session["PORT"]))
        {
            session["VALIDATION_ERROR_MSG"] = "ポート番号は 1〜65535 の数値で入力してください。";
            session["CONFIG_VALID"] = "0";
            return ActionResult.Success;
        }

        if (!IsValidCidr(session["ALLOWED_SUBNET"]))
        {
            session["VALIDATION_ERROR_MSG"] = "許可サブネットは CIDR 形式（例: 192.168.0.0/16）で入力してください。";
            session["CONFIG_VALID"] = "0";
        }

        return ActionResult.Success;
    }

    /// <summary>
    /// ポート番号と許可サブネットを検証し、不正値の場合はインストールを中断する。
    /// UI を持たない /qn（サイレント）インストールで不正なプロパティが渡された場合に備え、
    /// InstallExecuteSequence から呼び出す。
    /// </summary>
    /// <param name="session">MSI セッション。プロパティの読み取りとエラーメッセージ送信に使用する。</param>
    /// <returns>
    /// バリデーション通過時は <see cref="ActionResult.Success"/>。
    /// 不正値検出時は <see cref="ActionResult.Failure"/>（MSI がインストールを中断する）。
    /// </returns>
    [CustomAction]
    public static ActionResult ValidateConfigExecute(Session session)
    {
        if (!IsValidPort(session["PORT"]))
        {
            var record = new Record(0);
            record.FormatString = "Lucia インストールエラー: ポート番号は 1〜65535 の数値で入力してください。（入力値: " + session["PORT"] + "）";
            session.Message(InstallMessage.Error, record);
            return ActionResult.Failure;
        }

        if (!IsValidCidr(session["ALLOWED_SUBNET"]))
        {
            var record = new Record(0);
            record.FormatString = "Lucia インストールエラー: 許可サブネットは CIDR 形式（例: 192.168.0.0/16）で入力してください。（入力値: " + session["ALLOWED_SUBNET"] + "）";
            session.Message(InstallMessage.Error, record);
            return ActionResult.Failure;
        }

        return ActionResult.Success;
    }

    /// <summary>文字列が有効なポート番号（1〜65535）かどうかを検証する。</summary>
    /// <param name="portString">検証する文字列。null や空文字は無効とみなす。</param>
    /// <returns>有効なポート番号であれば true。</returns>
    internal static bool IsValidPort(string? portString)
    {
        return int.TryParse(portString, out int port) && port >= 1 && port <= 65535;
    }

    /// <summary>文字列が有効な IPv4 CIDR 表記かどうかを検証する。</summary>
    /// <param name="cidr">検証する文字列（例: 192.168.0.0/16）。</param>
    /// <returns>有効な CIDR 表記であれば true。</returns>
    internal static bool IsValidCidr(string? cidr)
    {
        if (string.IsNullOrEmpty(cidr)) return false;
        var parts = cidr!.Split('/');
        if (parts.Length != 2) return false;
        if (!IPAddress.TryParse(parts[0], out _)) return false;
        if (!int.TryParse(parts[1], out int prefixLength)) return false;
        return prefixLength >= 0 && prefixLength <= 32;
    }
}
