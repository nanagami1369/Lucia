# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Lucia** は Windows サーバー管理ダッシュボードです。RDP/ターミナルサービスのセッション管理とサーバー電源操作（シャットダウン・再起動・予約シャットダウン）を提供する Blazor フルスタック Web アプリケーションです。

**対象OS**: Windows のみ（Cassia による RDP API、Windows Service として動作）

## 開発コマンド

```bash
# ビルド
dotnet build

# 実行（開発モード）
dotnet run --project src/Lucia.Server/Lucia.Server/Lucia.Server.csproj

# リリースビルド
dotnet build --configuration Release
```

## アーキテクチャ

### プロジェクト構成

| プロジェクト | 役割 |
|---|---|
| `Lucia.Server` | ASP.NET Core ホスト、SignalR Hub、バックグラウンドサービス |
| `Lucia.Server.Client` | Blazor WebAssembly クライアント UI、Hub クライアント |
| `Lucia.Services` | ビジネスロジック（セッション・電源・タイマー管理） |
| `Lucia.Models` | ドメインモデル（`SessionInfo`、`SessionState` 等） |
| `LuciaServer.Shared` | Hub インターフェース（`ISessionHub`、`IPowerHub` 等） |

### データフロー

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

### リアルタイム通信

- `FetchWorker`（バックグラウンドサービス）が定期的にセッション情報をポーリングし、SignalR でブロードキャスト
- クライアント側は指数バックオフ（8 回リトライ）で自動再接続

### サービス層の構造

`src/Lucia.Services/` 以下はドメイン別フォルダで整理されている:
- `Sessions/` — `SessionService`（Cassia 経由の RDP セッション管理）
- `Power/` — `PowerService`（ProcessX 経由の電源操作）
- `Timer/` — `TimerService`（スケジュール管理）
- `Abstracts/` — `IService` 等の共通インターフェース

すべてのサービスは `StatsLogger` を通じてログを記録し、本番環境では Windows イベントログに出力される。
