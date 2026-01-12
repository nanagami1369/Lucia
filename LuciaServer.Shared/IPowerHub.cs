namespace LuciaServer.Shared;

/// <summary>
/// 電源管理 ハブ
/// </summary>
public interface IPowerHub {

    /// <summary>
    /// シャットダウン
    /// </summary>
    Task Restart();

    /// <summary>
    /// 再起動
    /// </summary>
    Task Shutdown();

    /// <summary>
    /// シャットダウンを予約する
    /// </summary>
    /// <param name="executeAt">実行予定時刻</param>
    Task RegisterScheduleShutdown(DateTimeOffset executeAt);

    /// <summary>
    /// 現在の予約シャットダウンをキャンセル
    /// </summary>
    Task CancelScheduleShutdown();

    /// <summary>
    /// 現在の予約シャットダウンがあったら返す。無ければnull
    /// </summary>
    Task<TimeSpan?> GetScheduleShutdown();

}

public interface IPowerClientHub {

    /// <summary>
    /// 現在の予約シャットダウンをキャンセル
    /// </summary>
    Task<TimeSpan?> GetScheduleShutdown();

}
