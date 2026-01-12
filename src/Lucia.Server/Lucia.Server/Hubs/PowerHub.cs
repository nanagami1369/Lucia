using Lucia.Services.Power;

using LuciaServer.Shared;

using Microsoft.AspNetCore.SignalR;

namespace Lucia.Server.Hubs;

public class PowerHub : Hub, IPowerHub {

    /// <summary>
    /// 電源管理サービス
    /// </summary>
    private readonly IPowerService powerService;

    /// <summary>
    /// 電源管理ハブ
    /// </summary>
    /// <param name="sessionService">電源管理サービス</param>
    public PowerHub(IPowerService sessionService) {
        this.powerService = sessionService;
    }

    /// <summary>
    /// シャットダウン
    /// </summary>
    public async Task Shutdown() {
        await powerService.Shutdown();
    }

    /// <summary>
    /// 再起動
    /// </summary>
    public async Task Restart() {
        await powerService.Restart();
    }

}
