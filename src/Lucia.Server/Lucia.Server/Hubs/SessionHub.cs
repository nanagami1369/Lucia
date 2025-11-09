using Lucia.Models.Models;
using Lucia.Server.Services;

using Microsoft.AspNetCore.SignalR;

namespace SessionMonitor.Hubs;

/// <summary>
/// セッション情報をリアルタイム配信するSignalRハブ
/// </summary>
public class SessionHub : Hub {
    private readonly SessionManagerService _sessionManagerService;

    /// <summary>
    /// SessionHubの新しいインスタンスを初期化します
    /// </summary>
    /// <param name="sessionManager">セッション管理サービス</param>
    public SessionHub(SessionManagerService sessionManager) {
        _sessionManagerService = sessionManager;
    }

    /// <summary>
    /// 現在のセッション一覧を取得します
    /// </summary>
    /// <returns>セッション情報のリスト</returns>
    public List<SessionInfo> GetSessions() {
        return _sessionManagerService.GetActiveSessions();
    }

    /// <summary>
    /// セッションを切断します
    /// </summary>
    /// <param name="sessionId">切断対象のセッションID</param>
    /// <returns>成功した場合true、失敗した場合false</returns>
    public async Task<bool> StopSession(int sessionId) {
        var success = _sessionManagerService.StopSession(sessionId);

        if (success) {
            // 全クライアントにセッション更新を通知
            await Clients.All.SendAsync("SessionsUpdated");
        }

        return success;
    }
}
