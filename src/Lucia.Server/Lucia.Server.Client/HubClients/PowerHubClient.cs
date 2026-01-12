
using LuciaServer.Shared;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace Lucia.Server.Client.HubClients;

public class PowerHubClient : HubClient, IPowerHub {

    /// <summary>
    /// コンストラクター
    /// </summary>
    public PowerHubClient(NavigationManager navigationManager, ILoggerFactory loggerFactory) : base(navigationManager, "powerhub", loggerFactory) {
    }

    /// <summary>
    /// シャットダウン
    /// </summary>
    public async Task Shutdown() {
        await InvokeAsync(nameof(IPowerHub.Shutdown));
    }

    /// <summary>
    /// 再起動
    /// </summary>
    public async Task Restart() {
        await InvokeAsync(nameof(IPowerHub.Restart));
    }

    protected override void RegisterOnHandler(HubConnection connection) {
        // 何もしない
    }
}
