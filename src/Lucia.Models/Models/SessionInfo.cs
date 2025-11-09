namespace Lucia.Models.Models;

/// <summary>
/// Windowsセッション情報
/// </summary>
/// <param name="SessionId">セッションID</param>
/// <param name="UserName">ログインユーザー名</param>
/// <param name="SessionName">セッション名（例: rdp-tcp#0）</param>
/// <param name="State">接続状態（Active, Disconnected等）</param>
/// <param name="LoginTime">ログイン時刻</param>
/// <param name="IdleTime">アイドル時間</param>
public record SessionInfo(
    int SessionId,
    string UserName,
    string SessionName,
    string State,
    DateTime? LoginTime,
    TimeSpan? IdleTime
);
