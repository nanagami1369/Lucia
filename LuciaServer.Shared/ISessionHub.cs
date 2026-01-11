using Lucia.Models.Models;

namespace LuciaServer.Shared;

/// <summary>
/// セッション ハブ
/// </summary>
public interface ISessionHub {

    /// <summary>
    /// セッションをログオフする
    /// </summary>
    /// <param name="sessionId">セッションID</param>
    Task LogOffSession(int sessionId);

    /// <summary>
    /// RDPを再起動する
    /// </summary>
    Task RestartRdp();

    /// <summary>
    /// セッション一覧を返す
    /// </summary>
    Task<SessionInfo[]> GetSessions();

}

/// <summary>
/// セッション クライアント ハブ
/// </summary>
public interface IClientSessionHub {

    /// <summary>
    /// セッションを取得する
    /// </summary>
    /// <param name="sessions">セッション一覧</param>
    public void GetSessions(SessionInfo[] sessions);

}
