using System;

using Lucia.Models.Exceptions;
using Lucia.Services.Sessions;

using Microsoft.AspNetCore.SignalR;

namespace Lucia.Server.Hubs;

/// <summary>
/// セッションハブ
/// </summary>
public class SessionHub : Hub {

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

}
