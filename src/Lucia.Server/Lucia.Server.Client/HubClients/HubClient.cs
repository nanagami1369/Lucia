using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace Lucia.Server.Client.HubClients;

/// <summary>
/// セッション用Hubクライアント
/// </summary>
public abstract class HubClient : IAsyncDisposable {

    /// <summary>
    /// ロガー
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Hubコネクション
    /// </summary>
    private readonly HubConnection _connection;

    /// <summary>
    /// 接続状態の変更イベント
    /// </summary>
    public event Action<HubClientState, Exception?> StateChanged = (_, _) => { };

    /// <summary>
    /// コンストラクター
    /// </summary>
    /// <param name="navigationManager">ナビゲーションマネージャー</param>
    /// <param name="pattern">パターン("sessionHub")</param>
    /// <param name="loggerFactory">ロガー</param>
    protected HubClient(NavigationManager navigationManager, string pattern, ILoggerFactory loggerFactory) {

        _logger = loggerFactory.CreateLogger(GetType());

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

        _connection.Reconnecting += HandleReconnecting;
        _connection.Reconnected += HandleReconnected;
        _connection.Closed += HandleClosed;

    }

    /// <summary>
    /// Onイベントハンドラーを登録します。
    /// </summary>
    /// <remarks>初回初期化時のみ実行</remarks>
    protected abstract void RegisterOnHandler(HubConnection connection);

    /// <summary>
    /// 接続開始
    /// </summary>
    public async Task Start() {
        Console.WriteLine("開始");
        if (_connection.State is not HubConnectionState.Disconnected) {
            _logger.LogWarning("停止していないクライアントは開始できません");
            return;
        }

        try {
            SafeInvoke(() => StateChanged(HubClientState.Connecting, null));
            await _connection.StartAsync();
            SafeInvoke(() => StateChanged(HubClientState.Connected, null));
        } catch (Exception e) {
            SafeInvoke(() => StateChanged(HubClientState.Disconnected, e));
            _logger.LogError(e, "接続に失敗しました");
        }
    }

    /// <summary>
    /// 再接続開始
    /// </summary>
    private Task HandleReconnecting(Exception? e) {
        SafeInvoke(() => StateChanged(HubClientState.Reconnecting, e));
        return Task.CompletedTask;
    }

    /// <summary>
    /// 再開
    /// </summary>
    private Task HandleReconnected(string? _) {
        SafeInvoke(() => StateChanged(HubClientState.Connected, null));
        return Task.CompletedTask;
    }

    /// <summary>
    /// 停止
    /// </summary>
    private Task HandleClosed(Exception? e) {
        Console.WriteLine("終了");
        SafeInvoke(() => StateChanged(HubClientState.Disconnected, e));
        return Task.CompletedTask;
    }

    /// <summary>
    /// 破棄処理
    /// </summary>
    public ValueTask DisposeAsync() {
        return _connection.DisposeAsync();
    }

    #region 通信処理系

    /// <summary>
    /// Hub上のメソッドを呼び出します。
    /// </summary>
    /// <param name="methodName">メソッド名称</param>
    /// <param name="args">引数</param>
    protected Task SendAsync(string methodName, params object?[] args) {
        if (_connection.State is not HubConnectionState.Connected) {
            _logger.LogWarning("クライアントは接続中ではありません");
            return Task.CompletedTask;
        }
        return _connection.SendCoreAsync(methodName, args, CancellationToken.None);
    }

    /// <summary>
    /// キャンセルトークン付きでHub上のメソッドを呼び出します。
    /// </summary>
    /// <param name="methodName">メソッド名称</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <param name="args">引数</param>
    protected Task SendAsync(string methodName, CancellationToken cancellationToken, params object?[] args) {
        if (_connection.State is not HubConnectionState.Connected) {
            _logger.LogWarning("クライアントは接続中ではありません");
            return Task.CompletedTask;
        }
        return _connection.SendCoreAsync(methodName, args, cancellationToken);
    }

    #endregion

    #region ユーティリティ系

    /// <summary>
    /// イベントを安全に呼び出す。ハンドラ内の例外は呼び出し元に伝播しない。
    /// </summary>
    protected void SafeInvoke(Action invokeEvent) {
        try {
            invokeEvent();
        } catch (Exception e) {
            _logger.LogError(e, "イベントハンドラ内で例外が発生しました");
        }
    }

    #endregion

}

/// <summary>
/// Hubクライアントのサービス登録
/// </summary>
public static class HubClientExtension {

    /// <summary>
    /// Hubクライアントの登録
    /// </summary>
    public static IServiceCollection AddHubClient<THubClient>(this IServiceCollection services) where THubClient : HubClient {

        services.AddSingleton<THubClient>();
        services.AddScoped<HubClient>(p => p.GetRequiredService<THubClient>());

        return services;

    }

}
