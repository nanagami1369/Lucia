
using System.Runtime.Versioning;
using System.ServiceProcess;

using Cassia;

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
    public void RestartRdp() {

        bool success = false;
        try {

            logger.LogInformation($"RDP再起動開始");

            using var service = new ServiceController("TermService");

            switch (service.Status) {
                // 止まってたら開始する
                case ServiceControllerStatus.Stopped:
                case ServiceControllerStatus.StopPending:
                    StartService(service);
                    break;
                // 動いてたりポーズだったら再起動
                case ServiceControllerStatus.StartPending:
                case ServiceControllerStatus.Running:
                case ServiceControllerStatus.ContinuePending:
                case ServiceControllerStatus.PausePending:
                case ServiceControllerStatus.Paused:
                    // 停止
                    var serviceOriginalStatus = StopService(service);
                    // トップサービスは何であれ開始してほしいでもともと開始していた事にする
                    serviceOriginalStatus = serviceOriginalStatus with { status = ServiceControllerStatus.Running };
                    // 復旧させる
                    ReStoreService(service, serviceOriginalStatus);
                    break;
            }
            logger.LogInformation($"RDP再起動成功");
            success = true;
        } catch (Exception ex) {
            logger.LogError(ex, $"エラーが発生しました。");
            throw;
        } finally {
            statsLogger.LogAction(success);
        }

        // サービスを開始する
        static void StartService(ServiceController service) {

            service.Start();
            service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));

        }

        // サービスを停止する
        // args:
        // service サービス
        // returns: サービス停止前のもとの状態
        static ServiceOriginalStatusMapForStopping StopService(ServiceController service) {

            // サービスが停止済みなら再起ループから抜ける
            if (service.Status == ServiceControllerStatus.Stopped) {
                return new ServiceOriginalStatusMapForStopping(service.ServiceName, service.Status, []);
            }

            var originalServiceStatus = service.Status;

            // 処理を停止できないものがあるなら停止前にエラー
            if (!service.CanStop) {
                throw new ServiceException($"停止できないサービスが含まれてます serviceName={service.DisplayName}");
            }

            // 子要素を停止
            var dependentServiceOriginalStatusList = new List<ServiceOriginalStatusMapForStopping>();
            foreach (var dependentService in service.DependentServices) {
                using (dependentService) {
                    dependentServiceOriginalStatusList.Add(StopService(dependentService));
                }
            }

            // 子要素の停止が終わったら自分を停止
            if (service.Status != ServiceControllerStatus.Stopped) {
                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            }

            return new ServiceOriginalStatusMapForStopping(service.ServiceName, originalServiceStatus, dependentServiceOriginalStatusList);
        }

        // サービスを復旧する
        // args:
        // service サービス
        // serviceOriginalStatusMapForStopping サービス停止前のもとの状態
        static void ReStoreService(ServiceController service, ServiceOriginalStatusMapForStopping serviceOriginalStatusMapForStopping) {

            // 下から停止していたのなら再開する必要は無い
            // 停止中のサービスの子も停止なので処理を停止
            if (serviceOriginalStatusMapForStopping.status == ServiceControllerStatus.Stopped) {
                return;
            }

            // 各サービスを復旧させる
            switch (serviceOriginalStatusMapForStopping.status) {
                // 下から停止していたのなら再開する必要は無い
                // 停止中のサービスの子も停止なので処理を停止
                case ServiceControllerStatus.Stopped:
                case ServiceControllerStatus.StopPending:
                    return;
                // 復帰中、開始中はすべて開始に
                case ServiceControllerStatus.StartPending:
                case ServiceControllerStatus.Running:
                case ServiceControllerStatus.ContinuePending:
                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                    break;
                case ServiceControllerStatus.PausePending:
                case ServiceControllerStatus.Paused:
                    // ポーズ系は一度開始してから一時停止
                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                    service.Pause();
                    service.WaitForStatus(ServiceControllerStatus.Paused, TimeSpan.FromSeconds(30));
                    break;
            }

            // 子要素を復旧
            foreach (var originalStatusMap in serviceOriginalStatusMapForStopping.dependentServices) {
                using (var dependentService = new ServiceController(originalStatusMap.ServiceName)) {
                    ReStoreService(dependentService, originalStatusMap);
                }
            }

        }
    }

    /// <summary>
    /// サービス停止中のステータス監視のためのクラス
    /// </summary>
    private record ServiceOriginalStatusMapForStopping(
            string ServiceName,
            ServiceControllerStatus status,
            List<ServiceOriginalStatusMapForStopping> dependentServices);

}
