# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Lucia** は Windows RDSホスト 管理ダッシュボードです。RDP/ターミナルサービスのセッション管理とサーバー電源操作（シャットダウン・再起動・予約シャットダウン）を提供する Blazor フルスタック Web アプリケーションです。

**対象OS**: Windows のみ（Cassia による RDP API、Windows Service として動作）

## 開発コマンド

```bash
# ビルド
dotnet build

# 実行（開発モード）
dotnet run --project src/Lucia.Server/Lucia.Server/Lucia.Server.csproj

# リリースビルド
dotnet build --configuration Release

# CLI インストーラーのビルド（Windows Service 用）
# src/Lucia.Installer/bin/Release/net10.0-windows/Lucia.Installer.exe に出力される
# 内部で Lucia.Server の publish まで自動実行される
dotnet publish src/Lucia.Installer/Lucia.Installer.csproj --configuration Release
```

## アーキテクチャ

### プロジェクト構成

| プロジェクト | 役割 |
|---|---|
| `Lucia.Server` | ASP.NET Core ホスト、SignalR Hub、バックグラウンドサービス |
| `Lucia.Server.Client` | Blazor WebAssembly クライアント UI、Hub クライアント |
| `Lucia.Services` | ビジネスロジック（セッション・電源・タイマー管理） |
| `Lucia.Models` | ドメインモデルと業務例外 |
| `LuciaServer.Shared` | Hub インターフェース（`ISessionHub`、`IPowerHub` 等） |
| `Lucia.Installer` | CLI インストーラー（純粋コンソールアプリ、将来の GUI 版は別プロジェクト） |

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

## Lucia.Installer のビルド設計原則

**`dotnet publish src/Lucia.Installer/Lucia.Installer.csproj --configuration Release` 単体で完結すること。**

- `OutputType=Exe` の純粋なコンソールアプリ。
- CLI 専用。将来 GUI が必要な場合は別プロジェクトとして CLI を呼び出す薄いシェルを作成する。
- Lucia.Server の publish は `Lucia.Installer.csproj` の `BuildServerBundle` MSBuild Target に実装し、`BeforeTargets="CoreCompile"` で自動実行される（Release のみ）。
- `Lucia.Installer.exe` は Lucia.Server の発行物を EmbeddedResource（`app-bundle.zip`）として内包する。
- インストール先に `Lucia.Installer.exe` を配置しアンインストーラーとして兼用（自己コピー方式で自己削除問題を回避）。
- Debug ビルドではバンドルを埋め込まないため、インストール実処理は Release ビルドで確認する。

## docs/ へのドキュメント記録

- `docs/` 直下: プロジェクト固有の仕様書・設計資料・移行計画等。
- `docs/notes/` : 他プロジェクトでも再利用できる汎用的な技術知見。プロジェクト固有の名称（サービス名・クラス名・パス等）は使わない。書き込む前に許可を求めない。
- `docs/notes/` の各ノートの frontmatter は以下のフォーマットとする：

```markdown
---
tags: blazor,c#,wasm   # トピックを表すタグをカンマ区切りで列挙
updated: 2026-03-20 13:04:20  # 作成・更新日時
---
```

## コーディングルール

- メソッド名、変数名はリーダブルコードを参考にすること
- 変数名は略称禁止、ただし、Linq式の引数、forループのiは許可するものとする
- ドキュメントコメントはすべてのメソッド（引数含む）、プロパティ、フィールドに記載すること

## Blazor WASM / SSR 混在時の注意

- SSR フェーズでは WASM クライアントの DI コンテナが存在しないため、WASM 専用サービスは `@inject` でなく `IServiceProvider.GetService<T>()` で取得し null チェックすること
