# インストーラー移行手順書（WiX MSI → 自前 WPF インストーラー）

仕様は `installer-spec.md` を参照。本書はWiXが担っていた機能を自前実装に置き換える際の対応方針を記録する。

---

## 技術スタック

| 役割 | ライブラリ | 採用理由 |
|---|---|---|
| UI | WPF | Windowsネイティブ・枯れた実績 |
| MVVM | CommunityToolkit.Mvvm | インストーラー規模に対してPrismは過剰。ソースジェネレーターでボイラープレート最小 |
| CLIコマンド実行 | ProcessX | `sc.exe` / `netsh` / `reg` の非同期実行。既存プロジェクトで採用済み |

---

## 移行対応一覧

### レジストリ登録（設定アプリへの表示）

**WiXでの実装**: Windows Installer が自動でアンインストールキーを書き込む。

**移行後**: インストール時にレジストリを自前で書き込み、アンインストール時に削除する。詳細な値は `installer-spec.md` 参照。

---

### Windows Service 登録・削除

**WiXでの実装**: `<ServiceInstall>` / `<ServiceControl>` で宣言的に定義。アンインストール時にSCMエントリが残る問題があったため `sc.exe delete LuciaServer` をカスタムアクションで明示実行していた。

**移行後**: `System.ServiceProcess.ServiceInstaller` または `sc.exe` コマンドで実装する。

---

### ファイアウォール規則 追加・削除

**WiXでの実装**: WiX Firewall 拡張が `RemoteAddresses`（接続元IP絞り込み）をサポートしないため、WiX でポートとプロファイルを設定した後、`netsh` でサブネットを追記する2段階構成だった。アンインストール時の削除も WiX では機能せず、`netsh` のカスタムアクションで明示実行していた。

**移行後**: `netsh` コマンドまたは `NetFwTypeLib` COM API で追加・削除を1コマンドで実装できる。

---

### イベントログソース 登録・削除

**WiXでの実装**: `<util:EventSource>` で登録。アンインストール時に削除できない問題があったため `reg delete` をカスタムアクションで明示実行していた。

**移行後**: `System.Diagnostics.EventLog.CreateEventSource` / `DeleteEventSource` で実装する。

---

### バリデーション（PORT / ALLOWED_SUBNET）

**WiXでの実装**: UIダイアログ上のリアルタイム検証と、サイレントインストール時のシーケンス内検証の2段構えで実装していた。

**移行後**: WPF の入力バリデーション（`IDataErrorInfo` 等）で実装する。コマンドライン引数でのサイレントインストールにも対応する場合は引数のバリデーションも追加する。

---

### アップグレード処理（旧バージョン検出）

**WiXでの実装**: `UpgradeCode` と `MajorUpgrade` により旧バージョンを自動検出・削除。

**移行後**: インストール開始時にレジストリの `Uninstall` キーを走査して既存インストールを検出し、アンインストール処理を呼び出してから新規インストールを行う。

---

### UACプロンプト（管理者昇格）

**WiXでの実装**: `Scope="perMachine"` により MSI 実行時に自動でUACプロンプトが表示された。

**移行後**: アプリケーションマニフェストに `requestedExecutionLevel level="requireAdministrator"` を設定する。

---

### MSIキャッシュ（不要・削除）

**WiXでの実装**: インストール後に `C:\Windows\Installer\` へ MSI をキャッシュし、設定アプリからのアンインストール時に参照していた。特定環境で APPCOMPAT シムがキャッシュ書き込みを妨害する問題が発生し、`FixLocalPackage` カスタムアクションで対処していた。

**移行後**: 不要。`UninstallString` にアンインストーラー.exeのパスを登録することで代替する。この問題ごと消える。

---

### ロールバック

**WiXでの実装**: Windows Installer がインストール失敗時に自動で巻き戻す。

**移行後**: 各操作を try-catch で囲み、失敗時は完了済みのステップを逆順でクリーンアップする。

| 順序 | 操作 | 失敗時の巻き戻し |
|---|---|---|
| 1 | ファイルコピー | コピー済みファイルを削除 |
| 2 | サービス登録・開始 | サービス停止・削除 |
| 3 | FWルール追加 | FWルール削除 |
| 4 | イベントログソース登録 | ソース削除 |
| 5 | レジストリ書き込み | レジストリキー削除 |

---

## アンインストーラーの配置と自己削除問題

### 配置方針

アンインストーラーをインストール先フォルダに配置し、`UninstallString` にそのパスを登録する。インストーラー本体とアンインストーラーは同一の実行ファイル（起動引数で動作を切り替え）でよい。

```
C:\Program Files\Lucia\
├── Lucia.Server.exe
├── Lucia.Installer.exe        ← インストール・アンインストール兼用
└── ...
```

`UninstallString` = `"C:\Program Files\Lucia\Lucia.Installer.exe" --uninstall`

### 自己削除問題と解決策

実行中のプロセスは自分自身のファイルを削除できない。以下の手順で対処する。

1. アンインストール開始時に自分自身を `%TEMP%` にコピーする
2. Tempコピーを引数付きで起動する（`--uninstall --source "C:\Program Files\Lucia"`）
3. 元プロセスを終了する
4. Tempコピーがアンインストール処理を実行し（サービス停止・ファイル削除・レジストリ削除等）、終了する
5. Tempコピー自身は `%TEMP%` に残るが、OSの定期クリーンアップに委ねる

NSIS・Inno Setup 等の主要インストーラーフレームワークと同じ標準的な方式。残留ファイルのサイズが小さく実害はない。
