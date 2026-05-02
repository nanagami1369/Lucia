# インストーラー移行手順書（WiX MSI → CLI インストーラー）

仕様は `installer-spec.md` を参照。本書はWiXが担っていた機能を自前実装に置き換えた際の対応方針を記録する。

---

## 技術スタック

| 役割 | ライブラリ | 採用理由 |
|---|---|---|
| CLIフレームワーク | ConsoleAppFramework v5 | ソースジェネレーターによるゼロオーバーヘッドのサブコマンドルーティング・ヘルプ自動生成 |
| CLIコマンド実行 | ProcessX | `sc.exe` / `netsh` の非同期実行。既存プロジェクトで採用済み |
| サービス制御 | System.ServiceProcess.ServiceController | サービス起動・停止の完了待機 |

WPF・CommunityToolkit.Mvvm は使用しない。将来 GUI が必要な場合は本プロジェクト（CLI）を内部で呼び出す薄いシェルを別プロジェクトとして作成する。

---

## 移行対応一覧

### レジストリ登録（設定アプリへの表示）

**WiXでの実装**: Windows Installer が自動でアンインストールキーを書き込む。

**移行後**: インストール時にレジストリを自前で書き込み、アンインストール時に削除する。詳細な値は `installer-spec.md` 参照。

---

### Windows Service 登録・削除

**WiXでの実装**: `<ServiceInstall>` / `<ServiceControl>` で宣言的に定義。アンインストール時にSCMエントリが残る問題があったため `sc.exe delete LuciaServer` をカスタムアクションで明示実行していた。

**移行後**: `sc.exe` コマンドで登録・削除を実装。

**`sc.exe` の引用符問題**: `sc.exe` の `binPath=` へのクォート渡しが不安定なため、`sc create/config` ではプレースホルダーを渡し、その後 `HKLM\SYSTEM\CurrentControlSet\Services\LuciaServer\ImagePath` をレジストリに直接書き込む。

---

### ファイアウォール規則 追加・削除

**WiXでの実装**: WiX Firewall 拡張が `RemoteAddresses`（接続元IP絞り込み）をサポートしないため、WiX でポートとプロファイルを設定した後、`netsh` でサブネットを追記する2段階構成だった。アンインストール時の削除も WiX では機能せず、`netsh` のカスタムアクションで明示実行していた。

**移行後**: `netsh advfirewall firewall add/delete rule` で追加・削除を1コマンドで実装。

**`0.0.0.0/0` の扱い**: `netsh` は `remoteip=0.0.0.0/0` を解釈できない。`ALLOWED_SUBNET` が `0.0.0.0/0`（全許可）の場合は `remoteip=Any` に変換して渡す。

---

### イベントログソース 登録・削除

**WiXでの実装**: `<util:EventSource>` で登録。アンインストール時に削除できない問題があったため `reg delete` をカスタムアクションで明示実行していた。

**移行後**: `System.Diagnostics.EventLog.CreateEventSource` / `DeleteEventSource` で実装する。

---

### バリデーション（PORT / ALLOWED_SUBNET）

**WiXでの実装**: UIダイアログ上のリアルタイム検証と、サイレントインストール時のシーケンス内検証の2段構えで実装していた。

**移行後**: コマンドライン引数のパース後にバリデーションを実施。不正値は終了コード `1` で即時終了し、エラー内容を標準エラー出力に出力する。

---

### アップグレード処理（旧バージョン検出）

**WiXでの実装**: `UpgradeCode` と `MajorUpgrade` により旧バージョンを自動検出・削除。

**移行後**: `install` コマンド実行時にレジストリの `Uninstall` キーを確認して既存インストールを検出。既存サービスが稼働中の場合は展開前に停止し、`sc config` で上書き更新（削除→再作成ではなくリペア方式）を行う。

---

### UACプロンプト（管理者昇格）

**WiXでの実装**: `Scope="perMachine"` により MSI 実行時に自動でUACプロンプトが表示された。

**移行後**: `app.manifest` に `requestedExecutionLevel level="requireAdministrator"` を設定する。

---

### MSIキャッシュ（不要・削除）

**WiXでの実装**: インストール後に `C:\Windows\Installer\` へ MSI をキャッシュし、設定アプリからのアンインストール時に参照していた。特定環境で APPCOMPAT シムがキャッシュ書き込みを妨害する問題が発生し、`FixLocalPackage` カスタムアクションで対処していた。

**移行後**: 不要。`UninstallString` にアンインストーラー .exe のパスを登録することで代替する。この問題ごと消える。

---

### ロールバック

**WiXでの実装**: Windows Installer がインストール失敗時に自動で巻き戻す。

**移行後**: 各操作を try-catch で囲み、失敗時は完了済みのステップを逆順でクリーンアップする。

| 順序 | 操作 | 失敗時の巻き戻し |
|---|---|---|
| 1 | レジストリ書き込み | レジストリキー削除 |
| 2 | ファイルコピー | コピー済みファイルを削除 |
| 3 | サービス登録・開始 | サービス停止・削除 |
| 4 | FWルール追加 | FWルール削除 |
| 5 | イベントログソース登録 | ソース削除 |

---

## アンインストーラーの配置と自己削除問題

### 配置方針

アンインストーラーをインストール先フォルダに配置し、`UninstallString` にそのパスを登録する。インストーラー本体とアンインストーラーは同一の実行ファイル（サブコマンドで動作を切り替え）。

```
C:\Program Files\Lucia\
├── Lucia.Server.exe
├── Lucia.Installer.exe     ← インストール・アンインストール兼用
└── ...
```

`UninstallString` = `"C:\Program Files\Lucia\Lucia.Installer.exe" uninstall`

### 自己削除問題と解決策

実行中の .NET プロセスは自分自身の EXE ファイルをメモリマップしているため、実行中はファイルを削除できない（Access Denied）。以下の手順で対処する。

1. アンインストール開始時に自分自身を `%TEMP%\lucia-uninstaller-<GUID>.exe` にコピーする
2. TEMPコピーを `uninstall --source "<インストール先>"` で起動する（stdout/stderr リダイレクトで同一コンソールへ出力）
3. 元プロセスは TEMPコピーの完了を待機して exit code を転送する
4. TEMPコピーがアンインストール処理を実行する（サービス停止・レジストリ削除・ファイル削除等）
5. TEMPコピー自身は `%TEMP%` に残るが、OSの定期クリーンアップに委ねる

**SingleFile publish が必須**: `PublishSingleFile=true` でなければ native EXE wrapper がコピー先に `Lucia.Installer.dll` を要求するが TEMP には存在しないため起動に失敗する。

### ファイル削除のロック問題とフォールバック

元プロセスが `Lucia.Installer.exe` のメモリマップを保持したまま終了待機しているため、TEMPコピーがインストール先フォルダの直接削除に失敗する場合がある。この場合のフォールバック：

1. レジストリは先に削除済みのため設定アプリのリストから即座に消える
2. `cmd.exe /C timeout /t 3 /nobreak > nul & rd /s /q "<インストール先>"` を起動して終了
3. 元プロセス・TEMPコピーが終了してロックが解放された後（約3秒後）に `cmd.exe` がフォルダを削除する

NSIS・Inno Setup 等の主要インストーラーフレームワークと同じ標準的な自己コピー方式。
