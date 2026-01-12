using System.Runtime.Versioning;

using Cysharp.Diagnostics;

using Lucia.Services;

using Microsoft.Extensions.Logging;

namespace Lucia.Server.Hubs;

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
    /// コンストラクター
    /// </summary>
    /// <param name="logger">ロガー</param>
    /// <param name="statsLogger">統計ロガー</param>
    public PowerService(ILogger<PowerService> logger, StatsLogger<PowerService> statsLogger) {
        this.logger = logger;
        this.statsLogger = statsLogger;
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

}
