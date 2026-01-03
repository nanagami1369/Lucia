namespace Lucia.Server.Client.HubClients;

public enum HubClientState {
    /// <summary>
    /// 実行されていない
    /// </summary>
    Initializing,
    /// <summary>
    /// 未接続
    /// </summary>
    Disconnected,
    /// <summary>
    /// 接続中
    /// </summary>
    Connecting,
    /// <summary>
    /// 接続完了
    /// </summary>
    Connected,
    /// <summary>
    /// 再接続中
    /// </summary>
    Reconnecting
}
