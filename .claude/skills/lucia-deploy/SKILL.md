---
name: lucia-deploy
description: >
  開発中のバージョンをビルドして開発マシンに再インストールするための開発者向けスキル。
  ユーザーへの配布・通常インストール用ではない。
  Use ONLY when the developer asks to rebuild and reinstall the current working branch on their own machine:
  "デプロイ", "再インストール", "redeploy", "再発行" in a development context.
  Do NOT use for end-user installation instructions or production deployment guidance.
version: 3.0.0
---

# Lucia デプロイスキル

> **このスキルは開発者専用です。**
> 開発中のブランチをビルドして開発マシンに再インストールするためのものです。
> エンドユーザーへの配布・通常インストールには使いません。

CLI インストーラー (`Lucia.Installer.exe`) をビルドして Windows Service として再インストールする。

## デプロイフロー

以下のスクリプトを実行する。UAC 昇格が必要なため、スクリプトが自動で `Start-Process -Verb RunAs` により昇格してインストールを実行する。

```bash
pwsh -ExecutionPolicy Bypass -File .claude/skills/lucia-deploy/scripts/deploy.ps1 2>&1
```

## スクリプトの動作

1. 管理者権限チェック → 非昇格なら `Start-Process -Verb RunAs` で自己を昇格再起動
2. `dotnet publish src/Lucia.Installer/Lucia.Installer.csproj --configuration Release` を実行
   - csproj 内の `BuildServerBundle` MSBuild Target が Lucia.Server の publish → `app-bundle.zip` 生成を自動実行
3. `Lucia.Installer.exe uninstall --yes` でアンインストール（未インストール時はスキップ）
4. `Lucia.Installer.exe install --yes` でインストール
   - Windows Service 登録・起動、ファイアウォール規則設定、イベントログソース登録、レジストリ登録を含む

## 出力確認

昇格プロセスの出力はログファイルに書き込まれる。スクリプト実行後、ログが表示されない場合は以下で確認する:

```bash
pwsh -NoProfile -Command "Get-Content 'logs/lucia-deploy.log' -Encoding UTF8"
```

## 注意事項

- `pwsh`（PowerShell 7）を使用すること。`powershell`（Windows PowerShell 5.x）は文字化けが発生する。
- スクリプトはリポジトリルートからの相対パスで動作するため、どのホームディレクトリでも利用可能。
- ログファイルは `logs/lucia-deploy.log`（リポジトリルート直下）に出力される。
- インストール先・ポート・サブネットはデフォルト値（`C:\Program Files\Lucia\`、`6100`、`192.168.0.0/16`）で固定。変更する場合は `Lucia.Installer.exe install --help` で確認してスクリプトを修正すること。
