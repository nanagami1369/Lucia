using Lucia.Services.Timer;

namespace Lucia.Server.TimerServices;

/// <summary>
/// タイマーサービスの実装
/// </summary>
public class TimerService<TKey> : ITimerService<TKey> {
    private readonly ITimerContainer _container;

    public TimerService(ITimerContainer container) {
        _container = container;
    }

    /// <summary>
    /// タイマーを登録する
    /// </summary>
    /// <param name="executeAt">実行予定時刻</param>
    /// <param name="action">実行するアクション</param>
    /// <returns>登録成功時は true、既に登録済みの場合は false</returns>
    public bool Register(DateTimeOffset executeAt, Func<Task> action) {
        return _container.Register<TKey>(executeAt, action);
    }

    /// <summary>
    /// タイマーをキャンセルする
    /// </summary>
    /// <returns>キャンセル成功時は true</returns>
    public bool Cancel() {
        return _container.Cancel<TKey>();
    }

    /// <summary>
    /// 残り時間を取得する
    /// </summary>
    /// <typeparam name="TKey">キーとなる型</typeparam>
    /// <returns>残り時間。未登録の場合は null</returns>
    public TimeSpan? GetRemaining() {
        return _container.GetRemaining<TKey>();
    }
}
