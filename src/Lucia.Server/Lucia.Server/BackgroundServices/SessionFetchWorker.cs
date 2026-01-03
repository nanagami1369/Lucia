using Lucia.Server.Hubs;
using Lucia.Services.Sessions;

using LuciaServer.Shared;

using Microsoft.AspNetCore.SignalR;

namespace Lucia.Server.BackgroundServices;

/// <summary>
/// セッション取得ワーカー
/// </summary>
public class SessionFetchWorker : BackgroundService {

    private readonly ILogger<SessionFetchWorker> logger;
    private readonly IHubContext<SessionHub> sessionHub;
    private readonly ISessionService sessionService;

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
    /// <param name="sessionHub">セッションハブ</param>
    /// <param name="logger">ロガー</param>
    /// <param name="sessionService">セッションサービス</param>
    public SessionFetchWorker(
        IHubContext<SessionHub> sessionHub,
        ILogger<SessionFetchWorker> logger,
        ISessionService sessionService
        ) {
        this.sessionHub = sessionHub;
        this.logger = logger;
        this.sessionService = sessionService;
    }

    public override Task StartAsync(CancellationToken cancellationToken) {
        logger.LogInformation($"{nameof(SessionFetchWorker)} 起動");
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken) {
        logger.LogInformation($"{nameof(SessionFetchWorker)} 終了");
        return base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {

        while (!stoppingToken.IsCancellationRequested) {
            try {
                var session = await Task.Run(() => sessionService.GetSessions());
                await sessionHub.Clients.All.SendAsync(nameof(IClientSessionHub.GetSessions), session, stoppingToken);
                await Task.Delay(5000, stoppingToken);

                // 成功したらエラー回数をリセット
                errorCount = 0;

            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                // エラー回数をカウント
                errorCount++;
                logger.LogError(ex, $"セッション情報の配信中にエラーが発生しました（連続エラー: {errorCount}回）");

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
