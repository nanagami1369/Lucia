
using Lucia.Models.Exceptions;
using Lucia.Services.Power;

using LuciaServer.Shared;

using Microsoft.AspNetCore.SignalR;

namespace Lucia.Server.Hubs;

public class PowerHub : Hub<IPowerClientHub>, IPowerHub {

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
        try {
            await powerService.Shutdown();
        } catch (UserBaseException ex) {
            throw new HubException(ex.Message, ex);
        }
    }

    /// <summary>
    /// 再起動
    /// </summary>
    public async Task Restart() {
        try {
            await powerService.Restart();
        } catch (UserBaseException ex) {
            throw new HubException(ex.Message, ex);
        }
    }

    /// <summary>
    /// シャットダウンを予約する
    /// </summary>
    /// <param name="executeAt">実行予定時刻</param>
    public Task RegisterScheduleShutdown(DateTimeOffset executeAt) {
        try {
            powerService.RegisterScheduleShutdown(executeAt);
            return Task.CompletedTask;
        } catch (UserBaseException ex) {
            throw new HubException(ex.Message, ex);
        }
    }

    /// <summary>
    /// 現在の予約シャットダウンがあったら返す。無ければnull
    /// </summary>
    public Task CancelScheduleShutdown() {
        try {
            powerService.CancelScheduleShutdown();
            return Task.CompletedTask;
        } catch (UserBaseException ex) {
            throw new HubException(ex.Message, ex);
        }
    }

    /// <summary>
    /// 現在の予約シャットダウンがあったら返す。無ければnull
    /// </summary>
    public Task<TimeSpan?> GetScheduleShutdown() {
        try {
            return Task.FromResult(powerService.GetScheduleShutdown());
        } catch (UserBaseException ex) {
            throw new HubException(ex.Message, ex);
        }
    }
}
