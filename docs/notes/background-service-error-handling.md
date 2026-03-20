---
tags: aspnetcore,c#,background-service
updated: 2026-03-20 13:04:20
---

# BackgroundService のエラーハンドリングパターン

## 問題

`BackgroundService.ExecuteAsync` 内で例外が発生した場合、デフォルトでは
ホストプロセスが停止しない（ログに出るだけでサービスは止まらない）。
エラーが連続して発生し続けてもゾンビ状態のサービスが残る。

## 連続エラーカウントによる自動停止パターン

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
    int errorCount = 0;
    const int maxErrorCount = 5;

    while (!stoppingToken.IsCancellationRequested) {
        try {
            await DoWork(stoppingToken);
            await Task.Delay(5000, stoppingToken);
            errorCount = 0; // 成功したらリセット
        } catch (OperationCanceledException) {
            break; // 正常停止
        } catch (Exception ex) {
            errorCount++;
            logger.LogError(ex, $"エラーが発生しました（連続: {errorCount}回）");

            if (errorCount >= maxErrorCount) {
                logger.LogCritical($"連続エラーが {maxErrorCount} 回に達したため停止します");
                break;
            }

            // エラー時は少し待機（エラー回数が多いほど長く待つ）
            await Task.Delay(1000 * Math.Max(3, errorCount));
        }
    }
}
```

## ポイント

- `OperationCanceledException` は正常停止なので `break` で抜ける（再スローしない）
- 成功時はカウンターをリセットする（間欠的エラーで停止しないように）
- エラー時の待機時間はエラー回数に比例させると、連続障害時のリソース消費を抑えられる
- 停止前に `LogCritical` で記録することで、Windows イベントログに CRITICAL レベルで残る

## StartAsync / StopAsync のログ

```csharp
public override Task StartAsync(CancellationToken cancellationToken) {
    logger.LogInformation($"{nameof(FetchWorker)} 起動");
    return base.StartAsync(cancellationToken);
}

public override Task StopAsync(CancellationToken cancellationToken) {
    logger.LogInformation($"{nameof(FetchWorker)} 終了");
    return base.StopAsync(cancellationToken);
}
```

起動・停止時のログを残すことで、Windows サービスのイベントログから動作状況を追跡できる。
