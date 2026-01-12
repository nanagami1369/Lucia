namespace Lucia.Server.TimerServices;

/// <summary>
/// タイマーエントリ
/// </summary>
/// <param name="ExecuteAt">実行時間</param>
/// <param name="Action">実行する処理</param>
internal record TimerEntry(DateTimeOffset ExecuteAt, Func<Task> Action);
