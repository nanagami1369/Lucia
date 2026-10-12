using System.Runtime.Versioning;

using Cysharp.Diagnostics;

using Lucia.Models.Exceptions;
using Lucia.Services.Sessions;
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
    /// 予約シャットダウンの事前通知を行うタイミング（実行の何分前か）
    /// </summary>
    private static readonly TimeSpan ScheduleShutdownNoticeBefore = TimeSpan.FromMinutes(5);

    /// <summary>
    /// タイマーサービス
    /// </summary>
    private readonly ITimerService<PowerService> timerService;

    /// <summary>
    /// セッション管理サービス
    /// </summary>
    private readonly ISessionService sessionService;

    /// <summary>
    /// コンストラクター
    /// </summary>
    /// <param name="logger">ロガー</param>
    /// <param name="statsLogger">統計ロガー</param>
    /// <param name="timerService">タイマーサービス</param>
    /// <param name="sessionService">セッション管理サービス</param>
    public PowerService(ILogger<PowerService> logger, StatsLogger<PowerService> statsLogger, ITimerService<PowerService> timerService, ISessionService sessionService) {
        this.logger = logger;
        this.statsLogger = statsLogger;
        this.timerService = timerService;
        this.sessionService = sessionService;
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
            var shutdownNotice = new TimerReminder(ScheduleShutdownNoticeBefore, () => NotifyScheduleShutdown(executeAt));
            if (!timerService.Register(executeAt, Shutdown, shutdownNotice)) {
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

    /// <summary>
    /// 予約シャットダウンが近いことをホスト上の操作中セッションに通知する
    /// </summary>
    /// <param name="executeAt">シャットダウン実行予定時刻</param>
    private Task NotifyScheduleShutdown(DateTimeOffset executeAt) {
        var executeAtLocal = executeAt.ToLocalTime();
        sessionService.NotifyActiveSessions(
            "Lucia - 予約シャットダウン",
            $"約{ScheduleShutdownNoticeBefore.TotalMinutes:0}分後（{executeAtLocal:HH:mm}）にこのサーバーはシャットダウンされます。{Environment.NewLine}"
            + "作業中のデータを保存してください。");
        return Task.CompletedTask;
    }

}
