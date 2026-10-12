using Lucia.Services.Timer;

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
    /// <param name="reminders">本実行前に実行する事前処理。登録時点で残り時間が Before 未満のものは無視される</param>
    /// <returns>登録成功時は true、既に登録済みの場合は false</returns>
    public bool Register<TKey>(DateTimeOffset executeAt, Func<Task> action, params TimerReminder[] reminders) {
        lock (_lock) {
            var key = typeof(TKey);
            if (_entries.ContainsKey(key)) {
                return false;
            }

            // 登録時点で既に事前処理の時刻を過ぎているものは、即時実行されてしまうため登録しない
            var remaining = executeAt - DateTimeOffset.Now;
            var pendingReminders = reminders
                .Where(r => r.Before <= remaining)
                .ToList();

            _entries[key] = new TimerEntry(executeAt, action, pendingReminders);
            return true;
        }
    }

    /// <summary>
    /// タイマーをキャンセルする（未実行の事前処理も含めて破棄される）
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
    /// 実行時刻を過ぎたエントリまたは事前処理を1件取得する
    /// </summary>
    /// <returns>実行すべきアクション。なければ null</returns>
    public Func<Task>? TryDequeue() {
        lock (_lock) {
            var now = DateTimeOffset.Now;

            // 1回のポーリングで1件のみ処理する
            // 複数同時実行による負荷集中を避けるため、次回ポーリングに委ねる

            // 本実行を優先する。本実行時刻を過ぎていれば未実行の事前処理は意味がないため破棄する
            var dueEntry = _entries.FirstOrDefault(kv => kv.Value.ExecuteAt <= now);
            if (dueEntry.Key != null) {
                _entries.Remove(dueEntry.Key);
                return dueEntry.Value.Action;
            }

            foreach (var entry in _entries.Values) {
                var dueReminder = entry.PendingReminders.FirstOrDefault(r => entry.ExecuteAt - r.Before <= now);
                if (dueReminder != null) {
                    entry.PendingReminders.Remove(dueReminder);
                    return dueReminder.Action;
                }
            }

            return null;
        }
    }
}
