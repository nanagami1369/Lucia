---
name: lucia-deploy
description: This skill should be used when the user asks to "デプロイ", "再発行", "redeploy", "deploy", "install", "インストール", or mentions publishing Lucia as a Windows Service. Provides the complete workflow for building Lucia.Installer and deploying it as a Windows Service.
version: 1.0.0
---

# Lucia デプロイスキル

Lucia.Installer をビルドし、Windows Service としてインストール（または再インストール）する。

## デプロイフロー

以下のスクリプトを実行する。UAC 昇格が必要なため、スクリプトが自動で `Start-Process -Verb RunAs` により昇格してインストールを実行する。

```bash
pwsh -ExecutionPolicy Bypass -File .claude/skills/lucia-deploy/scripts/deploy.ps1 2>&1
```

## スクリプトの動作

1. 管理者権限チェック → 非昇格なら `Start-Process -Verb RunAs` で自己を昇格再起動
2. `dotnet publish Lucia.Installer.csproj --configuration Release` を実行
   - csproj 内の `BuildServerBundle` MSBuild Target が Lucia.Server のビルド・zip 化・EmbeddedResource 埋め込みを自動実行
3. `publish/Lucia.Installer/Lucia.Installer.exe install --silent` でインストール実行
   - 既存の `LuciaServer` サービスを停止・削除してから再インストール
   - イベントログソース登録 / ファイアウォールルール設定も含む

## 出力確認

昇格プロセスの出力はログファイルに書き込まれる。スクリプト実行後、ログが表示されない場合は以下で確認する:

```bash
pwsh -NoProfile -Command "Get-Content 'logs/lucia-deploy.log' -Encoding UTF8"
```

## 注意事項

- `pwsh`（PowerShell 7）を使用すること。`powershell`（Windows PowerShell 5.x）は文字化けが発生する。
- スクリプトはリポジトリルートからの相対パスで動作するため、どのホームディレクトリでも利用可能。
- ログファイルは `logs/lucia-deploy.log`（リポジトリルート直下）に出力される。
