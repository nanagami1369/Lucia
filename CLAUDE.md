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

# リリースビルド＆発行（Windows Service 用）
# publish/Lucia フォルダに出力される
# .NET 10 では MapStaticAssets() の仕様により dotnet build では wwwroot が生成されないため、
# Windows Service として運用する場合は必ずこのコマンドで発行すること
dotnet publish src/Lucia.Server/Lucia.Server/Lucia.Server.csproj --configuration Release --runtime win-x64 --self-contained false --output ./publish/Lucia
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

**`dotnet publish Lucia.Installer --configuration Release` 単体で完結すること。**

- Lucia.Installer は一般ユーザーが配布された `.exe` を実行してインストールする。
- インストーラーのビルドが `redeploy.ps1` などの開発者スクリプトの事前実行を前提にしてはならない。
- `redeploy.ps1` は開発者が AI を経由して変更を継続的に検証するための補助スクリプトであり、一般ユーザーには関係しない。
- Lucia.Server のビルド・zip 化・EmbeddedResource への埋め込みはすべて `Lucia.Installer.csproj` の `BuildServerBundle` MSBuild Target に実装し、`BeforeTargets="CoreCompile"` で自動実行されるようにする。
- `redeploy.ps1` はビルドを呼び出すだけでよく、zip 生成や2フェーズ管理を PowerShell 側に持ち込んではならない。

## コーディングルール

- メソッド名、変数名はリーダブルコードを参考にすること
- 変数名は略称禁止、ただし、Linq式の引数、forループのiは許可するものとする
- ドキュメントコメントはすべてのメソッド（引数含む）、プロパティ、フィールドに記載すること

## Blazor WASM / SSR 混在時の注意

- SSR フェーズでは WASM クライアントの DI コンテナが存在しないため、WASM 専用サービスは `@inject` でなく `IServiceProvider.GetService<T>()` で取得し null チェックすること
