# Modals

## コンポーネント構成

| ファイル | 役割 |
|---|---|
| `Modal.razor` | ベースモーダル。`<dialog>` 要素のライフサイクル（JS interop）を一手に担う |
| `IModalContext.cs` | `Cancel()` / `Close()` を公開するインターフェース。子コンテンツから呼び出す際に使用 |
| `ConfirmModal.razor` | OK/キャンセルの確認ダイアログ。`Modal` を使って実装した使用例 |

---

## Modal.razor の使い方

### 基本パターン（ConfirmModal スタイル）

`Modal` を `@ref` で保持し、`Open()` / `Close()` / `Cancel()` をコードから呼ぶ。

```razor
<Modal @ref="modal" OnCancel="HandleCancel" OnClose="HandleClose">
    <p>内容</p>
    <button @onclick="async () => await context.Cancel()">キャンセル</button>
    <button @onclick="async () => await context.Close()">OK</button>
</Modal>

@code {
    private Modal? modal;

    private async Task ShowModal() => await modal!.Open();
    private void HandleCancel() { /* キャンセル時処理 */ }
    private void HandleClose()  { /* OK時処理 */ }
}
```

`ChildContent` の型は `RenderFragment<IModalContext>` であり、テンプレート引数 `context` として `IModalContext` が渡される。
`context.Cancel()` / `context.Close()` をボタンの onclick に渡すことで、子コンテンツ内から閉じる操作ができる。

### Locked モーダル（ユーザーが閉じられないモーダル）

`Locked="true"` を付けると ESC キー・バックドロップクリックによる閉鎖をブロックする。
プログラムからの `Close()` は引き続き動作する。

```razor
<Modal @ref="modal" Locked="true">
    <p>処理中...</p>
</Modal>
```

---

## 実装上の注意

### Open() は OnAfterRenderAsync より前に呼んでよい

`Modal` の JS モジュール（`showModal` / `close`）は `OnAfterRenderAsync(firstRender=true)` で非同期ロードされる。
`Open()` がロード完了前に呼ばれた場合、`pendingOpen` フラグに記録され、ロード完了後に自動で `showModal` が呼ばれる。
呼び出し側がタイミングを気にする必要はない。

### @rendermode を二重に付けない

`Modal` は `@rendermode InteractiveWebAssembly` を持つ。
`Modal` を使うコンポーネント側には **`@rendermode` を付けない**こと。

付けると両者が独立した Interactive Root になり、`OnAfterRenderAsync` の実行順序が保証されなくなる。
結果として `Open()` 呼び出し時に `jsModule` が未初期化のまま `pendingOpen` も機能しないケースが生じる。

`Modal` を使うコンポーネントは、すでに Interactive なコンテキスト（`@rendermode InteractiveWebAssembly` を持つ親）の中に配置すること。

### DI サービスは @inject でなく IServiceProvider 経由で取得する

SSR（サーバーサイドレンダリング）フェーズでは、Blazor WASM クライアント側のDIコンテナが存在しない。
Blazor WASM 専用サービス（例: `HubConnectionStatusService`）を `@inject` で直接注入すると、
SSR フェーズで `InvalidOperationException` が発生する。

```csharp
// NG: SSR フェーズで例外
@inject HubConnectionStatusService StatusService

// OK: OnAfterRenderAsync(firstRender=true) 内で取得
@inject IServiceProvider Services

var service = Services.GetService<HubConnectionStatusService>();
if (service == null) { return; } // SSR フェーズでは null になる
```

### StateHasChanged → Open/Close の順序

外部イベントでモーダルの内容と表示状態を同時に更新する場合、
`StateHasChanged` を先に `await` してからモーダルの開閉を行うこと。
逆順だとモーダルが表示された瞬間に古いコンテンツが見える。

```csharp
// OK
await InvokeAsync(StateHasChanged); // 内容を先に更新
await modal.Open();                  // その後で表示

// NG
await modal.Open();                  // 古い内容のまま表示される
await InvokeAsync(StateHasChanged);
```

---

## ブラウザ互換性メモ

### closedby 属性（2026-03-15 時点）

`closedby="none"` はバックドロップクリックと ESC によるダイアログ閉鎖を禁止する HTML 属性だが、
**Firefox は未対応**であり `InvalidCharacterError` が発生する。

現在の `Locked=true` 時の実装:
- `closedby` 属性を出力しない（`null` を渡すと Blazor が属性ごと省略する）
- ESC 防止は `@oncancel:preventDefault="Locked"` で代替

Firefox が `closedby` をサポートした時点で `Modal.razor` の `null` を `"none"` に変更すること。
