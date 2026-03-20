---
tags: signalr,c#,blazor
updated: 2026-03-20 13:04:20
---

# SignalR HubClient 設計パターン

## 抽象基底クラスによる共通化

Hub ごとに接続管理・再接続・エラーハンドリングを書くのは冗長。
抽象基底クラス `HubClient` に共通処理をまとめ、派生クラスでハンドラ登録のみ実装する。

```csharp
public abstract class HubClient : IAsyncDisposable {

    private readonly HubConnection _connection;

    protected HubClient(NavigationManager navigationManager, string pattern, ILoggerFactory loggerFactory) {
        _connection = new HubConnectionBuilder()
            .WithUrl(new Uri(new Uri(navigationManager.BaseUri), pattern))
            .WithAutomaticReconnect([
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(2),
                TimeSpan.FromMinutes(5),
            ])
            .Build();

        RegisterOnHandler(_connection);
        // ... イベント登録
    }

    // 派生クラスで connection.On<T>() の登録のみ実装
    protected abstract void RegisterOnHandler(HubConnection connection);
}

// 使う側
public class SessionHubClient : HubClient, ISessionHub {
    protected override void RegisterOnHandler(HubConnection connection) {
        connection.On<SessionInfo[]>(nameof(IClientSessionHub.GetSessions), HandleUpdateSession);
    }
}
```

## 指数バックオフ再接続

`WithAutomaticReconnect` に遅延時間の配列を渡すと、指数バックオフ的な再接続が実現できる。
配列の末尾の値は繰り返されない（配列外 = 再接続停止）ため、長期の再試行が必要な場合は十分な要素数を用意すること。

## SafeInvoke パターン

イベントハンドラ内で例外が発生した場合、呼び出し元（SignalR の内部処理）に伝播させないようにラップする：

```csharp
protected void SafeInvoke(Action invokeEvent) {
    try {
        invokeEvent();
    } catch (Exception e) {
        _logger.LogError(e, "イベントハンドラ内で例外が発生しました");
    }
}
```

## HubClientState による UI 状態管理

接続状態を enum で管理し、コンポーネントは `StateChanged` イベントを購読して UI を更新する：

```csharp
public enum HubClientState {
    Initializing, Disconnected, Connecting, Connected, Reconnecting
}

public event Action<HubClientState, Exception?> StateChanged = (_, _) => { };
```

接続完了時に最新データを取得する例：

```csharp
private async void HandleStateChanged(HubClientState state, Exception? _)
{
    if (state is HubClientState.Connected) {
        var sessionInfoList = await SessionHubClient.GetSessions();
        UpdateSession(sessionInfoList);
    }
}
```

## DI 登録パターン

Hub クライアントは Singleton + Scoped の二重登録で、コンポーネントから抽象型・具象型どちらでも取得できる：

```csharp
public static IServiceCollection AddHubClient<THubClient>(this IServiceCollection services)
    where THubClient : HubClient
{
    services.AddSingleton<THubClient>();
    services.AddScoped<HubClient>(p => p.GetRequiredService<THubClient>());
    return services;
}
```

## Hub インターフェースの分離

サーバー呼び出し用とクライアント呼び出し用でインターフェースを分ける：

```csharp
// サーバー側（クライアントが呼ぶ）
public interface ISessionHub {
    Task LogOffSession(int sessionId);
    Task<SessionInfo[]> GetSessions();
}

// クライアント側（サーバーが Push する）
public interface IClientSessionHub {
    void GetSessions(SessionInfo[] sessions);
}
```

`connection.On<T>(nameof(IClientSessionHub.GetSessions), handler)` のように
`nameof` で文字列ハードコードを避けることで、リファクタリング耐性が上がる。
