---
tags: blazor,c#,wasm,signalr
updated: 2026-03-20 13:04:20
---

# Blazor WASM/SSR 混在時の DI パターン

## 問題

Blazor の SSR フェーズ（サーバーサイドレンダリング）では、WASM クライアント用の DI コンテナが存在しない。
`@inject SessionHubClient` のように直接インジェクトすると、SSR フェーズで例外が発生する。

## 解決策：`IServiceProvider.GetService<T>()` + null チェック

```razor
@inject IServiceProvider Services
@rendermode InteractiveWebAssembly

@code {
    private SessionHubClient? SessionHubClient { get; set; }

    protected override void OnAfterRender(bool firstRender)
    {
        if (!firstRender) { return; }
        // WASM フェーズでのみ DI コンテナが存在するため null チェック必須
        if (Services.GetService<SessionHubClient>() is { } hubClient)
        {
            SessionHubClient = hubClient;
            SessionHubClient.UpdateSession += UpdateSession;
        }
    }
}
```

**ポイント：**
- `@inject` ではなく `IServiceProvider.GetService<T>()` で取得する
- `OnAfterRender` かつ `firstRender` のときだけ初期化する（SSR フェーズでは `OnAfterRender` は呼ばれない）
- `IDisposable.Dispose()` でイベントハンドラを必ず解除する

## `@rendermode InteractiveWebAssembly` の指定

コンポーネントに `@rendermode InteractiveWebAssembly` を指定することで、SSR フェーズ後に WASM で再レンダリングされる。
SignalR やブラウザ API を使うコンポーネントには必須。

```razor
@rendermode InteractiveWebAssembly
```

## ナビゲーション例外を無効化

Blazor WASM のナビゲーション処理で `NavigationException` が throw される場合がある（Firefox で顕在化）。
`csproj` で以下を設定すると抑制できる：

```xml
<BlazorDisableThrowNavigationException>true</BlazorDisableThrowNavigationException>
```
