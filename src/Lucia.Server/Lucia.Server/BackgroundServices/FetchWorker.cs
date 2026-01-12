using Lucia.Server.Hubs;
using Lucia.Services.Power;
using Lucia.Services.Sessions;

using LuciaServer.Shared;

using Microsoft.AspNetCore.SignalR;

namespace Lucia.Server.BackgroundServices;

/// <summary>
/// データ取得ワーカー
/// </summary>
public class FetchWorker : BackgroundService {

    private readonly ILogger<FetchWorker> logger;
    private readonly IHubContext<SessionHub> sessionHub;
    private readonly ISessionService sessionService;
    private readonly IHubContext<PowerHub> powerHub;
    private readonly IPowerService powerService;

    /// <summary>
    /// エラー回数
    /// </summary>
    private int errorCount = 0;

    /// <summary>
    /// 最大エラー回数、これを超えたら処理を停止する
    /// </summary>
    private readonly int maxErrorCount = 5;


    /// <summary>
    /// コンストラクター
    /// </summary>
    /// <param name="logger">ロガー</param>
    /// <param name="sessionHub">セッションハブ</param>
    /// <param name="sessionService">セッションサービス</param>
    /// <param name="sessionHub">電源管理ハブ</param>
    /// <param name="powerService">電源管理サービス</param>
    public FetchWorker(
        ILogger<FetchWorker> logger,
        IHubContext<SessionHub> sessionHub,
        ISessionService sessionService,
        IHubContext<PowerHub> powerHub,
        IPowerService powerService
        ) {
        this.sessionHub = sessionHub;
        this.logger = logger;
        this.sessionService = sessionService;
        this.powerHub = powerHub;
        this.powerService = powerService;
    }

    public override Task StartAsync(CancellationToken cancellationToken) {
        logger.LogInformation($"{nameof(FetchWorker)} 起動");
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken) {
        logger.LogInformation($"{nameof(FetchWorker)} 終了");
        return base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {

        while (!stoppingToken.IsCancellationRequested) {
            try {

                // セッションサービス
                var session = await Task.Run(() => sessionService.GetSessions());
                await sessionHub.Clients.All.SendAsync(nameof(IClientSessionHub.GetSessions), session, stoppingToken);

                // 電源管理サービス
                var scheduleShutdown = powerService.GetScheduleShutdown();
                await powerHub.Clients.All.SendAsync(nameof(IPowerClientHub.GetScheduleShutdown), scheduleShutdown, stoppingToken);

                await Task.Delay(5000, stoppingToken);

                // 成功したらエラー回数をリセット
                errorCount = 0;

            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                // エラー回数をカウント
                errorCount++;
                logger.LogError(ex, $"情報の配信中にエラーが発生しました（連続エラー: {errorCount}回）");

                // 連続エラーが一定回数を超えたら停止
                if (errorCount >= maxErrorCount) {
                    logger.LogCritical($"連続エラーが{maxErrorCount}回に達したため、配信を停止します");
                    break;
                }

                await Task.Delay(1000 * Math.Max(3, errorCount));

            }
        }

    }

}
