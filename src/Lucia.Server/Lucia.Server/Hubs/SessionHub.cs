using System;

using Lucia.Models.Exceptions;
using Lucia.Models.Models;
using Lucia.Services.Sessions;

using LuciaServer.Shared;

using Microsoft.AspNetCore.SignalR;

namespace Lucia.Server.Hubs;

/// <summary>
/// セッションハブ
/// </summary>
public class SessionHub : Hub<IClientSessionHub>, ISessionHub {

    /// <summary>
    /// セッションサービス
    /// </summary>
    private readonly ISessionService sessionService;

    /// <summary>
    /// セッションハブ
    /// </summary>
    /// <param name="sessionService">セッションサービス</param>
    public SessionHub(ISessionService sessionService) {
        this.sessionService = sessionService;
    }

    /// <summary>
    /// セッションをログオフする
    /// </summary>
    /// <param name="sessionId">セッションID</param>
    public async Task LogOffSession(int sessionId) {
        try {
            await Task.Run(() => sessionService.LogOffSession(sessionId));
        } catch (UserBaseException ex) {
            throw new HubException(ex.Message, ex);
        }

    }

    /// <summary>
    /// RDPを再起動する
    /// </summary>
    public async Task RestartRdp() {
        try {
            await Task.Run(() => sessionService.RestartRdp());
        } catch (UserBaseException ex) {
            throw new HubException(ex.Message, ex);
        }
    }

    /// <summary>
    /// セッション一覧を返す
    /// </summary>
    public async Task<SessionInfo[]> GetSessions() {
        try {
            return await Task.Run(() => sessionService.GetSessions());
        } catch (UserBaseException ex) {
            throw new HubException(ex.Message, ex);
        }
    }


}
