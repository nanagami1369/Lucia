using Lucia.Models.Abstracts;
using Lucia.Models.Models;

namespace Lucia.Services.Sessions;

/// <summary>
/// セッション管理サービス
/// </summary>
public interface ISessionService : IService {

    /// <summary>
    /// ローカルマシンのユーザーセッション一覧を取得
    /// </summary>
    /// <returns>セッション情報一覧</returns>
    SessionInfo[] GetSessions();

    /// <summary>
    /// 指定セッションをログオフ
    /// </summary>
    /// <param name="sessionId">セッションID</param>
    void LogOffSession(int sessionId);

    /// <summary>
    /// Terminal Service を再起動、成功時true
    /// </summary>
    Task RestartRdp();

    /// <summary>
    /// 操作中の全ユーザーセッションのデスクトップにメッセージを表示する
    /// </summary>
    /// <param name="caption">メッセージのタイトル</param>
    /// <param name="message">メッセージ本文</param>
    void NotifyActiveSessions(string caption, string message);
}
