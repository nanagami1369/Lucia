namespace Lucia.Services.Timer;

/// <summary>
/// 本実行より前に一度だけ実行する事前処理
/// </summary>
/// <param name="Before">本実行のどれだけ前に実行するか</param>
/// <param name="Action">実行する処理</param>
public record TimerReminder(TimeSpan Before, Func<Task> Action);
