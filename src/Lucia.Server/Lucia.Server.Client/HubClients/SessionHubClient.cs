using Lucia.Models.Models;

using LuciaServer.Shared;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace Lucia.Server.Client.HubClients;

/// <summary>
/// セッションハブクライアント
/// </summary>
public class SessionHubClient : HubClient {

    /// <summary>
    /// コンストラクター
    /// </summary>
    public SessionHubClient(
        NavigationManager navigationManager,
        ILoggerFactory loggerFactory)
        : base(navigationManager, "sessionhub", loggerFactory) {
    }

    /// <summary>
    /// セッションの更新時発生
    /// </summary>
    public Action<SessionInfo[]> UpdateSession = _ => { };

    /// <summary>
    /// セッションの更新時発生
    /// </summary>
    public void HandleUpdateSession(SessionInfo[] sessionList) {
        SafeInvoke(() => UpdateSession(sessionList));
    }

    protected override void RegisterOnHandler(HubConnection connection) {

        connection.On<SessionInfo[]>(nameof(IClientSessionHub.GetSessions), HandleUpdateSession);

    }

}
