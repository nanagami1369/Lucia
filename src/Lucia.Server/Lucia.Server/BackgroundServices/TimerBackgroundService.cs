using Lucia.Server.TimerServices;

namespace Lucia.Server.BackgroundServices;

/// <summary>
/// タイマー実行用バックグラウンドサービス
/// </summary>
public class TimerBackgroundService : BackgroundService {
    private readonly ITimerContainer _container;
    private readonly ILogger<TimerBackgroundService> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(1);

    public TimerBackgroundService(
        ITimerContainer container,
        ILogger<TimerBackgroundService> logger
    ) {
        _container = container;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        _logger.LogInformation($"{nameof(TimerBackgroundService)} 開始");

        using var timer = new PeriodicTimer(_pollInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken)) {
            var action = _container.TryDequeue();
            if (action != null) {
                try {
                    await action();
                } catch (Exception ex) {
                    _logger.LogError(ex, $"{nameof(TimerBackgroundService)} 実行中にエラー");
                }
            }
        }
    }
}
