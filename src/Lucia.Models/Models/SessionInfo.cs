namespace Lucia.Models.Models;

/// <summary>
/// Windowsセッション情報
/// </summary>
/// <param name="SessionId">セッションID（例: 1, 2, 3）</param>
/// <param name="UserName">ログインユーザー名（例: DESKTOP-ABC\Administrator）</param>
/// <param name="SessionName">セッション名（例: rdp-tcp#0）</param>
/// <param name="State">接続状態（例: Active, Disconnected等）</param>
/// <param name="LoginTime">ログイン時刻（例: 2025/11/15 09:00:00）</param>
/// <param name="IdleTime">アイドル時間（例: 5時間 35分）</param>
public record SessionInfo(
    int SessionId,
    string UserName,
    string SessionName,
    SessionState State,
    DateTime? LoginTime,
    TimeSpan? IdleTime
);
