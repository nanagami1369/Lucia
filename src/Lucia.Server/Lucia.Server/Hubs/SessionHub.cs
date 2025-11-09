using Lucia.Models.Models;
using Lucia.Server.Services;

using Microsoft.AspNetCore.SignalR;

namespace SessionMonitor.Hubs;

/// <summary>
/// セッション情報をリアルタイム配信するSignalRハブ
/// </summary>
public class SessionHub : Hub {
    private readonly SessionManagerService _sessionManager;
    private readonly ILogger<SessionHub> _logger;

    /// <summary>
    /// SessionHubの新しいインスタンスを初期化します
    /// </summary>
    /// <param name="sessionManager">セッション管理サービス</param>
    /// <param name="logger">ロガー</param>
    public SessionHub(SessionManagerService sessionManager, ILogger<SessionHub> logger) {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    /// <summary>
    /// クライアント接続時の処理
    /// </summary>
    public override async Task OnConnectedAsync() {
        _logger.LogInformation("クライアント接続: ConnectionId={ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// クライアント切断時の処理
    /// </summary>
    /// <param name="exception">例外情報（正常切断時はnull）</param>
    public override async Task OnDisconnectedAsync(Exception? exception) {
        if (exception != null) {
            _logger.LogWarning(exception, "クライアント切断（異常終了）: ConnectionId={ConnectionId}", Context.ConnectionId);
        } else {
            _logger.LogInformation("クライアント切断: ConnectionId={ConnectionId}", Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// 現在のセッション一覧を取得します
    /// </summary>
    /// <returns>セッション情報のリスト</returns>
    public List<SessionInfo> GetSessions() {
        _logger.LogDebug("GetSessions呼び出し: ConnectionId={ConnectionId}", Context.ConnectionId);
        return _sessionManager.GetActiveSessions();
    }

    /// <summary>
    /// セッションを切断します
    /// </summary>
    /// <param name="sessionId">切断対象のセッションID</param>
    /// <returns>成功した場合true、失敗した場合false</returns>
    public async Task<bool> StopSession(int sessionId) {
        _logger.LogInformation("StopSession呼び出し: SessionId={SessionId}, ConnectionId={ConnectionId}", sessionId, Context.ConnectionId);

        var success = _sessionManager.StopSession(sessionId);

        if (success) {
            _logger.LogInformation("セッション切断成功、全クライアントに通知: SessionId={SessionId}", sessionId);
            // 全クライアントにセッション更新を通知
            await Clients.All.SendAsync("SessionsUpdated");
        }

        return success;
    }
}
