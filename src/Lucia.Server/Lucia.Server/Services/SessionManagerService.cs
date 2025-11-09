using Cassia;

using Lucia.Models.Models;

namespace Lucia.Server.Services;

/// <summary>
/// Windowsセッション管理サービス
/// </summary>
public class SessionManagerService {
    private readonly ITerminalServicesManager _manager;

    /// <summary>
    /// SessionManagerの新しいインスタンスを初期化します
    /// </summary>
    public SessionManagerService() {
        _manager = new TerminalServicesManager();
    }

    /// <summary>
    /// アクティブなセッション一覧を取得します
    /// </summary>
    /// <returns>セッション情報のリスト</returns>
    public List<SessionInfo> GetActiveSessions() {
        var sessions = new List<SessionInfo>();

        try {
            using var server = _manager.GetLocalServer();
            server.Open();

            foreach (var session in server.GetSessions()) {
                sessions.Add(new SessionInfo(
                    SessionId: session.SessionId,
                    UserName: session.UserAccount?.ToString() ?? "N/A",
                    SessionName: session.WindowStationName,
                    State: session.ConnectionState.ToString(),
                    LoginTime: session.LoginTime,
                    IdleTime: session.IdleTime
                ));
            }
        } catch (Exception ex) {
            Console.WriteLine($"Error getting sessions: {ex.Message}");
        }

        return sessions;
    }

    /// <summary>
    /// 指定されたセッションIDのセッションを切断します
    /// </summary>
    /// <param name="sessionId">切断対象のセッションID</param>
    /// <returns>成功した場合true、失敗した場合false</returns>
    public bool StopSession(int sessionId) {
        try {
            using var server = _manager.GetLocalServer();
            server.Open();

            var session = server.GetSession(sessionId);
            session.Logoff();
            return true;
        } catch (Exception ex) {
            Console.WriteLine($"Error stopping session {sessionId}: {ex.Message}");
            return false;
        }
    }
}
