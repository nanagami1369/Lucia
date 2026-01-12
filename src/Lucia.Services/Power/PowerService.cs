using System.Runtime.Versioning;

using Cysharp.Diagnostics;

using Lucia.Models.Exceptions;
using Lucia.Services.Timer;

using Microsoft.Extensions.Logging;

namespace Lucia.Services.Power;

/// <summary>
/// 電源管理サービス
/// </summary>
[SupportedOSPlatform("windows")]
public class PowerService : IPowerService {

    /// <summary>
    /// ロガー
    /// </summary>
    private readonly ILogger<PowerService> logger;

    /// <summary>
    /// 統計ロガー
    /// </summary>
    private readonly StatsLogger<PowerService> statsLogger;

    /// <summary>
    /// タイマーサービス
    /// </summary>
    private readonly ITimerService<PowerService> timerService;

    /// <summary>
    /// コンストラクター
    /// </summary>
    /// <param name="logger">ロガー</param>
    /// <param name="statsLogger">統計ロガー</param>
    /// <param name="timerService">タイマーサービス</param>
    public PowerService(ILogger<PowerService> logger, StatsLogger<PowerService> statsLogger, ITimerService<PowerService> timerService) {
        this.logger = logger;
        this.statsLogger = statsLogger;
        this.timerService = timerService;
    }

    /// <summary>
    /// シャットダウン
    /// </summary>
    public async Task Shutdown() {

        bool success = false;
        try {

            logger.LogInformation("シャットダウン開始");
            await ProcessX.StartAsync("shutdown.exe /s /t 0").WaitAsync();
            logger.LogInformation("シャットダウン成功");
            success = true;

        } catch (Exception ex) {

            logger.LogError(ex, "シャットダウン処理でエラーが発生しました");
            throw;

        } finally {

            statsLogger.LogAction(success);

        }
    }

    /// <summary>
    /// 再起動
    /// </summary>
    public async Task Restart() {

        bool success = false;
        try {

            logger.LogInformation("再起動開始");
            await ProcessX.StartAsync("shutdown.exe /r /t 0").WaitAsync();
            logger.LogInformation("再起動成功");
            success = true;

        } catch (Exception ex) {

            logger.LogError(ex, "再起動処理でエラーが発生しました");
            throw;

        } finally {

            statsLogger.LogAction(success);

        }
    }

    /// <summary>
    /// シャットダウンを予約する
    /// </summary>
    /// <param name="executeAt">実行予定時刻</param>
    public void RegisterScheduleShutdown(DateTimeOffset executeAt) {

        bool success = false;
        try {

            logger.LogInformation($"シャットダウン予約開始 executeAt={executeAt:O}");
            if (!timerService.Register(executeAt, Shutdown)) {
                logger.LogError($"既に予約済みです。現在の予約時刻={executeAt:O}");
                throw new PowerException("すでに予約済みです。");
            }
            logger.LogInformation($"シャットダウン予約成功 executeAt={executeAt:O}");
            success = true;

        } catch (Exception ex) {

            logger.LogError(ex, $"シャットダウン予約処理でエラーが発生しました。executeAt={executeAt:O}");
            throw;

        } finally {

            statsLogger.LogAction(success);

        }
    }

    /// <summary>
    /// 現在の予約シャットダウンをキャンセル
    /// </summary>
    public void CancelScheduleShutdown() {

        bool success = false;
        try {

            logger.LogInformation("シャットダウン予約キャンセル開始");
            timerService.Cancel();
            logger.LogInformation("シャットダウン予約キャンセル成功");
            success = true;

        } catch (Exception ex) {

            logger.LogError(ex, "シャットダウン予約キャンセル処理でエラーが発生しました");
            throw;

        } finally {

            statsLogger.LogAction(success);

        }
    }

    /// <summary>
    /// 現在の予約シャットダウンがあったら返す。無ければnull
    /// </summary>
    public TimeSpan? GetScheduleShutdown() {
        return timerService.GetRemaining();
    }

}
