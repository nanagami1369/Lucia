namespace Lucia.Services.Timer;

/// <summary>
/// タイマーサービスのインターフェース
/// </summary>
public interface ITimerService<Tkey> {

    /// <summary>
    /// タイマーを登録する
    /// </summary>
    /// <typeparam name="TKey">キーとなる型</typeparam>
    /// <param name="executeAt">実行予定時刻</param>
    /// <param name="action">実行するアクション</param>
    /// <param name="reminders">本実行前に実行する事前処理。登録時点で残り時間が Before 未満のものは無視される</param>
    /// <returns>登録成功時は true、既に登録済みの場合は false</returns>
    bool Register(DateTimeOffset executeAt, Func<Task> action, params TimerReminder[] reminders);

    /// <summary>
    /// タイマーをキャンセルする
    /// </summary>
    /// <typeparam name="TKey">キーとなる型</typeparam>
    /// <returns>キャンセル成功時は true</returns>
    bool Cancel();

    /// <summary>
    /// 残り時間を取得する
    /// </summary>
    /// <typeparam name="TKey">キーとなる型</typeparam>
    /// <returns>残り時間。未登録の場合は null</returns>
    TimeSpan? GetRemaining();

}
