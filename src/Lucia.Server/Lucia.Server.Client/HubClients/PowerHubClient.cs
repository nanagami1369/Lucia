

using Lucia.Models.Models;

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
    /// 予約シャットダウンの更新時に発生
    /// </summary>
    public event Action<TimeSpan?> UpdateScheduleShutdown = (_) => { };

    /// <summary>
    /// 予約シャットダウンの更新時に発生
    /// </summary>
    /// <param name="remainingTime">残り時間</param>
    private void HandleUpdateScheduleShutdown(TimeSpan? remainingTime) {
        SafeInvoke(() => UpdateScheduleShutdown(remainingTime));
    }

    protected override void RegisterOnHandler(HubConnection connection) {
        connection.On<TimeSpan?>(nameof(IPowerClientHub.GetScheduleShutdown), HandleUpdateScheduleShutdown);
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

    /// <summary>
    /// シャットダウンを予約する
    /// </summary>
    /// <param name="executeAt">実行予定時刻</param>
    public async Task RegisterScheduleShutdown(DateTimeOffset executeAt) {
        await InvokeAsync(nameof(IPowerHub.RegisterScheduleShutdown), executeAt);
    }

    /// <summary>
    /// 現在の予約シャットダウンをキャンセル
    /// </summary>
    public async Task CancelScheduleShutdown() {
        await InvokeAsync(nameof(IPowerHub.CancelScheduleShutdown));
    }

    /// <summary>
    /// 現在の予約シャットダウンがあったら返す。無ければnull
    /// </summary>
    public async Task<TimeSpan?> GetScheduleShutdown() {
        return await InvokeAsync<TimeSpan?>(nameof(IPowerHub.GetScheduleShutdown));
    }
}
