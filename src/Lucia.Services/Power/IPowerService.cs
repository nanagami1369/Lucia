using Lucia.Models.Abstracts;

namespace Lucia.Services.Power;

/// <summary>
/// 電源管理サービス
/// </summary>
public interface IPowerService : IService {

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
    public void RegisterScheduleShutdown(DateTimeOffset executeAt);


    /// <summary>
    /// 現在の予約シャットダウンをキャンセル
    /// </summary>
    void CancelScheduleShutdown();

    /// <summary>
    /// 現在の予約シャットダウンがあったら返す。無ければnull
    /// </summary>
    TimeSpan? GetScheduleShutdown();

}
