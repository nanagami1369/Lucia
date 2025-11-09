using Cassia;

using Lucia.Models.Models;

namespace Lucia.Server.Services;

/// <summary>
/// Windowsセッション管理サービス
/// </summary>
public class SessionManagerService {
    private readonly ITerminalServicesManager _manager;
    private readonly ILogger<SessionManagerService> _logger;

    /// <summary>
    /// SessionManagerの新しいインスタンスを初期化します
    /// </summary>
    /// <param name="logger">ロガー</param>
    public SessionManagerService(ILogger<SessionManagerService> logger) {
        _manager = new TerminalServicesManager();
        _logger = logger;
    }

    /// <summary>
    /// アクティブなセッション一覧を取得します
    /// </summary>
    /// <returns>セッション情報のリスト</returns>
    public List<SessionInfo> GetActiveSessions() {
        var sessions = new List<SessionInfo>();

        try {
            _logger.LogDebug("セッション情報の取得を開始");

            using var server = _manager.GetLocalServer();
            server.Open();

            foreach (var session in server.GetSessions()) {

                var userAccount = session.UserAccount;

                if (userAccount == null) {
                    continue;
                }

                sessions.Add(new SessionInfo(
                    SessionId: session.SessionId,
                    UserName: userAccount.ToString(),
                    SessionName: session.WindowStationName,
                    State: session.ConnectionState.ToString(),
                    LoginTime: session.LoginTime,
                    IdleTime: session.IdleTime
                ));
            }

            _logger.LogDebug("セッション情報の取得成功: {Count}件", sessions.Count);
        } catch (Exception ex) {
            _logger.LogError(ex, "セッション情報の取得中にエラーが発生しました");
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
            _logger.LogInformation("セッション切断を開始: SessionId={SessionId}", sessionId);

            using var server = _manager.GetLocalServer();
            server.Open();

            var session = server.GetSession(sessionId);
            if (session.UserAccount == null) {
                throw new Exception($"そのセッションは削除できません SessionId={sessionId}");
            }
            session.Logoff();

            _logger.LogInformation("セッション切断成功: SessionId={SessionId}", sessionId);
            return true;
        } catch (Exception ex) {
            _logger.LogError(ex, "セッション切断中にエラーが発生しました: SessionId={SessionId}", sessionId);
            return false;
        }
    }
}
