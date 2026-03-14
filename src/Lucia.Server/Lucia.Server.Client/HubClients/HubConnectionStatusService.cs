namespace Lucia.Server.Client.HubClients;

/// <summary>
/// 全HubClientの接続状態を集約するサービス
/// </summary>
public class HubConnectionStatusService : IDisposable {

    /// <summary>
    /// 各HubClientとその現在の状態の辞書
    /// </summary>
    private readonly Dictionary<HubClient, HubClientState> _clientStates;

    /// <summary>
    /// 各HubClientに登録したハンドラーの辞書（Dispose時の登録解除に使用）
    /// </summary>
    private readonly Dictionary<HubClient, Action<HubClientState, Exception?>> _handlers;

    /// <summary>
    /// 全体の接続状態が変化したときに発生
    /// </summary>
    public event Action<HubClientState, Exception?> OverallStateChanged = (_, _) => { };

    /// <summary>
    /// 現在の全体接続状態
    /// </summary>
    public HubClientState OverallState { get; private set; } = HubClientState.Initializing;

    /// <summary>
    /// 直近の例外（Disconnected状態時のエラー情報）
    /// </summary>
    public Exception? LastException { get; private set; }

    /// <summary>
    /// コンストラクター
    /// </summary>
    /// <param name="hubClients">DI登録された全HubClientの列挙</param>
    public HubConnectionStatusService(IEnumerable<HubClient> hubClients) {
        var clientList = hubClients.ToList();
        _clientStates = clientList.ToDictionary(client => client, _ => HubClientState.Initializing);
        _handlers = new Dictionary<HubClient, Action<HubClientState, Exception?>>();

        foreach (var client in clientList) {
            var capturedClient = client;
            Action<HubClientState, Exception?> handler = (state, exception) => HandleClientStateChanged(capturedClient, state, exception);
            _handlers[client] = handler;
            client.StateChanged += handler;
        }
    }

    /// <summary>
    /// HubClientの状態変化ハンドラー
    /// </summary>
    /// <param name="client">状態が変化したHubClient</param>
    /// <param name="state">新しい状態</param>
    /// <param name="exception">発生した例外（ある場合）</param>
    private void HandleClientStateChanged(HubClient client, HubClientState state, Exception? exception) {
        _clientStates[client] = state;
        if (exception != null) {
            LastException = exception;
        }

        var newOverallState = ComputeOverallState();
        if (newOverallState == OverallState) { return; }

        OverallState = newOverallState;
        OverallStateChanged(OverallState, LastException);
    }

    /// <summary>
    /// 全HubClientの状態から最も深刻な状態を算出します
    /// </summary>
    private HubClientState ComputeOverallState() =>
        _clientStates.Values
            .OrderByDescending(GetStatePriority)
            .First();

    /// <summary>
    /// 状態の深刻度を数値で返します（値が大きいほど深刻）
    /// </summary>
    /// <param name="state">評価する状態</param>
    private static int GetStatePriority(HubClientState state) => state switch {
        HubClientState.Disconnected => 4,
        HubClientState.Reconnecting => 3,
        HubClientState.Connecting   => 2,
        HubClientState.Initializing => 1,
        HubClientState.Connected    => 0,
        _                           => 0,
    };

    /// <summary>
    /// 破棄処理：各HubClientのイベント購読を解除します
    /// </summary>
    public void Dispose() {
        foreach (var (client, handler) in _handlers) {
            client.StateChanged -= handler;
        }
        _handlers.Clear();
    }

}
