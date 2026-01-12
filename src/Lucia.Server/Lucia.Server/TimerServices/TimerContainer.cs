namespace Lucia.Server.TimerServices;

/// <summary>
/// タイマーコンテナの実装
/// </summary>
public class TimerContainer : ITimerContainer {
    private readonly Dictionary<Type, TimerEntry> _entries = [];
    private readonly Lock _lock = new();

    /// <summary>
    /// タイマーを登録する
    /// </summary>
    /// <typeparam name="TKey">キーとなる型</typeparam>
    /// <param name="executeAt">実行予定時刻</param>
    /// <param name="action">実行するアクション</param>
    /// <returns>登録成功時は true、既に登録済みの場合は false</returns>
    public bool Register<TKey>(DateTimeOffset executeAt, Func<Task> action) {
        lock (_lock) {
            var key = typeof(TKey);
            if (_entries.ContainsKey(key)) {
                return false;
            }
            _entries[key] = new TimerEntry(executeAt, action);
            return true;
        }
    }

    /// <summary>
    /// タイマーをキャンセルする
    /// </summary>
    /// <typeparam name="TKey">キーとなる型</typeparam>
    /// <returns>キャンセル成功時は true</returns>
    public bool Cancel<TKey>() {
        lock (_lock) {
            return _entries.Remove(typeof(TKey));
        }
    }

    /// <summary>
    /// 残り時間を取得する
    /// </summary>
    /// <typeparam name="TKey">キーとなる型</typeparam>
    /// <returns>残り時間。未登録の場合は null</returns>
    public TimeSpan? GetRemaining<TKey>() {
        lock (_lock) {
            if (!_entries.TryGetValue(typeof(TKey), out var entry)) {
                return null;
            }
            var remaining = entry.ExecuteAt - DateTimeOffset.Now;
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }
    }

    /// <summary>
    /// 実行時刻を過ぎたエントリを1件取得し削除する
    /// </summary>
    /// <returns>実行すべきアクション。なければ null</returns>
    public Func<Task>? TryDequeue() {
        lock (_lock) {
            var now = DateTimeOffset.Now;

            // 1回のポーリングで1件のみ処理する
            // 複数同時実行による負荷集中を避けるため、次回ポーリングに委ねる
            var dequeue = _entries.FirstOrDefault(kv => kv.Value.ExecuteAt <= now);

            if (dequeue.Key == null) {
                return null;
            }
            _entries.Remove(dequeue.Key);
            return dequeue.Value.Action;
        }
    }
}
