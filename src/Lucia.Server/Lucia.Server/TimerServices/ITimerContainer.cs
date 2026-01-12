namespace Lucia.Server.TimerServices;

/// <summary>
/// タイマーコンテナのインターフェース
/// </summary>
public interface ITimerContainer {
    /// <summary>
    /// タイマーを登録する
    /// </summary>
    /// <typeparam name="TKey">キーとなる型</typeparam>
    /// <param name="executeAt">実行予定時刻</param>
    /// <param name="action">実行するアクション</param>
    /// <returns>登録成功時は true、既に登録済みの場合は false</returns>
    bool Register<TKey>(DateTimeOffset executeAt, Func<Task> action);

    /// <summary>
    /// タイマーをキャンセルする
    /// </summary>
    /// <typeparam name="TKey">キーとなる型</typeparam>
    /// <returns>キャンセル成功時は true</returns>
    bool Cancel<TKey>();

    /// <summary>
    /// 残り時間を取得する
    /// </summary>
    /// <typeparam name="TKey">キーとなる型</typeparam>
    /// <returns>残り時間。未登録の場合は null</returns>
    TimeSpan? GetRemaining<TKey>();

    /// <summary>
    /// 実行時刻を過ぎたエントリを1件取得し削除する
    /// </summary>
    /// <returns>実行すべきアクション。なければ null</returns>
    Func<Task>? TryDequeue();
}
