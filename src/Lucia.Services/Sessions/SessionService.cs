
using System.Runtime.Versioning;
using System.ServiceProcess;

using Cassia;

using Cysharp.Diagnostics;

using Lucia.Models.Exceptions;
using Lucia.Models.Models;

using Microsoft.Extensions.Logging;

namespace Lucia.Services.Sessions;

/// <summary>
/// セッション管理サービス
/// </summary>
[SupportedOSPlatform("windows")]
public class SessionService : ISessionService {

    /// <summary>
    /// 端末管理サービス
    /// </summary>
    private readonly ITerminalServicesManager manager = new TerminalServicesManager();

    /// <summary>
    /// ロガー
    /// </summary>
    private readonly ILogger<SessionService> logger;

    /// <summary>
    /// 統計ロガー
    /// </summary>
    private readonly StatsLogger<SessionService> statsLogger;

    /// <summary>
    /// コンストラクター
    /// </summary>
    /// <param name="logger">ロガー</param>
    /// <param name="statsLogger">統計ロガー</param>
    public SessionService(ILogger<SessionService> logger, StatsLogger<SessionService> statsLogger) {
        this.logger = logger;
        this.statsLogger = statsLogger;
    }

    /// <summary>
    /// ローカルマシンのユーザーセッション一覧を取得 
    /// </summary>
    /// <returns>セッション情報一覧</returns>
    public SessionInfo[] GetSessions() {

        using var server = manager.GetLocalServer();
        return server
               .GetSessions()
               .Where(s => s.UserAccount != null)
               .Select(s => new SessionInfo(
                   s.SessionId,
                   s.UserAccount.ToString(),
                   s.WindowStationName,
                   MapConnectionState(s.ConnectionState),
                   s.LoginTime,
                   s.IdleTime))
               .ToArray();

        // コネクションの状態をSessionStateに変換
        static SessionState MapConnectionState(ConnectionState cassia) {
            return cassia switch {
                // ユーザーが操作中
                ConnectionState.Active => SessionState.Active,
                ConnectionState.Connected => SessionState.Active,
                ConnectionState.ConnectQuery => SessionState.Active,

                // RDP接続は切れたがセッション存続
                ConnectionState.Disconnected => SessionState.Disconnected,

                // ログイン状態だが操作なし
                ConnectionState.Idle => SessionState.Idle,

                // その他異常系は Idle扱いとする
                ConnectionState.Shadowing => SessionState.Active,      // 別セッション監視中
                ConnectionState.Reset => SessionState.Idle,            // リセット中
                ConnectionState.Initializing => SessionState.Idle,     // 初期化中

                // これらはUserAccountがnullなのでフィルタリングされる想定
                ConnectionState.Listening => SessionState.Idle,
                ConnectionState.Down => SessionState.Idle,

                _ => SessionState.Idle
            };
        }

    }

    /// <summary>
    /// 指定セッションをログオフ
    /// </summary>
    /// <param name="sessionId">セッションID</param>
    public void LogOffSession(int sessionId) {

        bool success = false;
        try {

            logger.LogInformation($"セッションログオフ開始 sessionId={sessionId}");
            using var server = manager.GetLocalServer();
            var session = server.GetSession(sessionId);
            var userAcccount = session.UserAccount;
            if (userAcccount == null) {
                logger.LogError($"システムセッションのログオフは禁止されてます。sessionId={sessionId}");
                throw new SessionException($"システムセッションのログオフは禁止されてます。sessionId={session.SessionId}");
            }
            session.Logoff();
            logger.LogInformation($"セッションログオフ成功 sessionId={sessionId}");
            success = true;
        } catch (Exception ex) {
            logger.LogError(ex, $"エラーが発生しました。sessionId={sessionId}");
            throw;
        } finally {
            statsLogger.LogAction(success);
        }
    }

    /// <summary>
    /// Rdpサービスを再起動
    /// </summary>
    public async Task RestartRdp() {

        bool success = false;
        try {

            logger.LogInformation("RDP再起動開始");
            await ProcessX.StartAsync("net stop TermService /y").WaitAsync();
            await ProcessX.StartAsync("net start TermService").WaitAsync();
            logger.LogInformation("RDP再起動成功");
            success = true;

        } catch (Exception ex) {

            logger.LogError(ex, "エラーが発生しました。");
            throw;

        } finally {

            statsLogger.LogAction(success);

        }
    }

}
