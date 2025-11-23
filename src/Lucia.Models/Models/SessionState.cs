namespace Lucia.Models.Models;

/// <summary>
/// セッション状態
/// </summary>
public enum SessionState {

    /// <summary>
    /// アクティブ（ユーザーが利用中）
    /// </summary>
    Active = 0,
    /// <summary>
    /// 切断状態（RDP接続を切断したが再接続可能）
    /// </summary>
    Disconnected = 1,

    /// <summary>
    /// アイドル状態（ログイン中だが操作なし）
    /// </summary>
    Idle = 1

}
