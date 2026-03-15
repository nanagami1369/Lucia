# Lucia

Windows RDSホスト 管理ダッシュボード。RDP セッション管理とサーバー電源操作をブラウザから行える Blazor フルスタック Web アプリケーションです。

## スクリーンショット

![Luciaダッシュボード](https://raw.githubusercontent.com/nanagami1369/Lucia/main/docs/screenshot.png)

## ⚠️ 注意事項

**本アプリケーションは LAN 内での使用のみを想定しています。**

認証・認可・通信の暗号化など、セキュリティに関する考慮は一切行っていません。
アクセスできる者はサーバーのシャットダウンや他ユーザーのセッションログオフを含む、すべての操作を無制限に実行できます。

**インターネットや社外ネットワークへの公開は絶対に行わないでください。**

## 主な機能

### セッション管理
- RDP セッション一覧の表示（ユーザー名・接続状態・ログイン時刻・アイドル時間）
- 指定セッションのログオフ
- RDP サービス（TermService）の再起動

### 電源操作
- 即時シャットダウン
- 即時再起動
- 予約シャットダウン（2時間後に自動実行）
- 予約シャットダウンのキャンセル

### リアルタイム更新
- SignalR によるリアルタイム通信（5 秒ポーリング）
- 接続断時の自動再接続（指数バックオフ：0s / 2s / 10s / 30s / 1m / 2m / 5m）

## 動作環境

- **OS**: Windows のみ（Linux・macOS 非対応）
- **ランタイム**: .NET 10
- **動作形態**: Windows Service として動作

## 技術スタック

| 技術 | 用途 |
|---|---|
| Blazor WebAssembly + ASP.NET Core | フロントエンド UI / サーバーホスト |
| SignalR | リアルタイム双方向通信 |
| [Cassia](https://github.com/danports/cassia) | RDP / ターミナルサービス セッション管理 |
| [ProcessX](https://github.com/Cysharp/ProcessX) | 電源コマンド実行 |
| Windows Event Log | 本番環境ロギング |

## プロジェクト構成

```
src/
├── Lucia.Server/
│   ├── Lucia.Server/           # ASP.NET Core ホスト・SignalR Hub・バックグラウンドサービス
│   └── Lucia.Server.Client/    # Blazor WebAssembly クライアント UI・Hub クライアント
├── Lucia.Services/             # ビジネスロジック（セッション・電源・タイマー管理）
├── Lucia.Models/               # ドメインモデル（SessionInfo・SessionState 等）
└── LuciaServer.Shared/         # Hub インターフェース（ISessionHub・IPowerHub 等）
```

| プロジェクト | 役割 |
|---|---|
| `Lucia.Server` | ASP.NET Core ホスト、SignalR Hub、バックグラウンドサービス |
| `Lucia.Server.Client` | Blazor WebAssembly クライアント UI、Hub クライアント |
| `Lucia.Services` | ビジネスロジック（セッション・電源・タイマー管理） |
| `Lucia.Models` | ドメインモデル（`SessionInfo`、`SessionState` 等） |
| `LuciaServer.Shared` | Hub インターフェース（`ISessionHub`、`IPowerHub` 等） |

## アーキテクチャ

```
Razor コンポーネント
  ↓ (SignalR)
Hub クライアント (SessionHubClient / PowerHubClient)
  ↓ (Hub メソッド呼び出し)
サーバー Hub (SessionHub / PowerHub)
  ↓ (DI)
サービス (SessionService / PowerService)
  ↓ (Windows API)
Windows OS (Cassia で RDP セッション / ProcessX で電源コマンド)
```

`FetchWorker`（バックグラウンドサービス）がサーバー側で 5 秒ごとにセッション情報と予約シャットダウン状態を取得し、接続中のすべてのクライアントへ SignalR でブロードキャストします。

## 開発環境のセットアップ

### 前提条件

- Windows 10 / 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### ビルドと実行

```bash
# ビルド
dotnet build

# 開発モードで実行
dotnet run --project src/Lucia.Server/Lucia.Server/Lucia.Server.csproj
```

ブラウザで `https://localhost:5001`（または起動時に表示される URL）にアクセスします。


## デプロイ（Windows Service）

### 発行

```bash
dotnet publish src/Lucia.Server/Lucia.Server/Lucia.Server.csproj \
  --configuration Release \
  --runtime win-x64 \
  --self-contained false \
  --output ./publish/Lucia
```

> **注意**: .NET 10 では `MapStaticAssets()` の仕様により `dotnet build` では `wwwroot` が生成されません。Windows Service として運用する場合は必ず `dotnet publish` で発行してください。

### インストール

発行後、`publish/Lucia/` に出力された `Installer.ps1` を管理者権限で実行します。

```powershell
# サービスのインストールと起動（ポート 6100 で起動、ファイアウォール開放）
.\publish\Lucia\Installer.ps1 -Action install

# サービスの停止とアンインストール
.\publish\Lucia\Installer.ps1 -Action uninstall
```

インストール後はブラウザで `http://<サーバーIP>:6100` にアクセスします。

## ライセンス

[MIT License](LICENSE)

### 開発中の動作検証（再デプロイ）

開発中に素早く動作確認したい場合は、以下のスクリプトで発行からサービス再起動までを一括実行できます。

```powershell
powershell -ExecutionPolicy Bypass -File scripts\redeploy.ps1
```

既存の `LuciaServer` サービスをアンインストールし、`dotnet publish` 後に再インストールまで自動で行います。UAC ダイアログが表示されるので管理者権限で承認してください。
