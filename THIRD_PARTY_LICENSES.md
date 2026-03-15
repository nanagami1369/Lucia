# サードパーティライセンス一覧

本ドキュメントは、Lucia が使用するサードパーティライブラリのライセンス情報をまとめたものです。

---

## 直接依存パッケージ

### 非 Microsoft 製サードパーティ

---

#### Cassia 2.0.0.60

- **用途**: Windows RDP セッション管理（Cassia ライブラリ経由で Terminal Services API を利用）
- **ライセンス**: MIT License
- **著作権**: Copyright (c) 2008-2017 Dan Ports
- **リポジトリ**: https://github.com/danports/cassia

```
MIT License

Copyright (c) 2008 - 2017 Dan Ports

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

#### ProcessX 1.5.6

- **用途**: 電源操作コマンド（shutdown / restart）の非同期プロセス実行
- **ライセンス**: MIT License
- **著作権**: Copyright (c) Cysharp, Inc.
- **リポジトリ**: https://github.com/Cysharp/ProcessX

```
MIT License

Copyright (c) 2020 Cysharp, Inc.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

### Microsoft 製パッケージ

以下のパッケージはすべて MIT License の下で提供されています。
著作権: Copyright (c) Microsoft Corporation. All rights reserved.
ライセンス参照: https://github.com/dotnet/aspnetcore/blob/main/LICENSE.txt

| パッケージ名 | バージョン | 用途 | リポジトリ |
|---|---|---|---|
| Microsoft.AspNetCore.Components.WebAssembly | 10.0.5 | Blazor WASM クライアント基盤 | https://github.com/dotnet/aspnetcore |
| Microsoft.AspNetCore.Components.WebAssembly.Server | 10.0.5 | Blazor WASM ホスティング（サーバー側） | https://github.com/dotnet/aspnetcore |
| Microsoft.AspNetCore.SignalR.Client | 10.0.5 | SignalR クライアント（リアルタイム通信） | https://github.com/dotnet/aspnetcore |
| Microsoft.Extensions.Hosting.WindowsServices | 10.0.5 | Windows Service としてのホスティング | https://github.com/dotnet/runtime |
| Microsoft.Extensions.Logging.Abstractions | 10.0.5 | ロギング抽象化インターフェース | https://github.com/dotnet/runtime |
| System.ServiceProcess.ServiceController | 10.0.5 | Windows サービス制御 | https://github.com/dotnet/runtime |

---

#### Microsoft.TypeScript.MSBuild 5.9.3

- **用途**: TypeScript ファイルのビルド時コンパイル（ビルド時専用・実行時には含まれない）
- **ライセンス**: Microsoft Software License Terms（プロプライエタリライセンス）
- **備考**: `PrivateAssets="all"` 指定により配布物には含まれないビルドツールです
- **著作権**: Copyright (c) Microsoft Corporation

---

## 推移的依存パッケージ（Transitive Dependencies）

以下は直接参照していないが間接的に使用される Microsoft 製パッケージです。
すべて MIT License / Copyright (c) Microsoft Corporation です。

| パッケージ名 | バージョン |
|---|---|
| Microsoft.AspNetCore.Authorization | 10.0.5 |
| Microsoft.AspNetCore.Components | 10.0.5 |
| Microsoft.AspNetCore.Components.Analyzers | 10.0.5 |
| Microsoft.AspNetCore.Components.Forms | 10.0.5 |
| Microsoft.AspNetCore.Components.Web | 10.0.5 |
| Microsoft.AspNetCore.Connections.Abstractions | 10.0.5 |
| Microsoft.AspNetCore.Http.Connections.Client | 10.0.5 |
| Microsoft.AspNetCore.Http.Connections.Common | 10.0.5 |
| Microsoft.AspNetCore.Metadata | 10.0.5 |
| Microsoft.AspNetCore.SignalR.Client.Core | 10.0.5 |
| Microsoft.AspNetCore.SignalR.Common | 10.0.5 |
| Microsoft.AspNetCore.SignalR.Protocols.Json | 10.0.5 |
| Microsoft.Extensions.Configuration | 10.0.5 |
| Microsoft.Extensions.Configuration.Abstractions | 10.0.5 |
| Microsoft.Extensions.Configuration.Binder | 10.0.5 |
| Microsoft.Extensions.Configuration.FileExtensions | 10.0.5 |
| Microsoft.Extensions.Configuration.Json | 10.0.5 |
| Microsoft.Extensions.DependencyInjection | 10.0.5 |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.5 |
| Microsoft.Extensions.Diagnostics | 10.0.5 |
| Microsoft.Extensions.Diagnostics.Abstractions | 10.0.5 |
| Microsoft.Extensions.Features | 10.0.5 |
| Microsoft.Extensions.FileProviders.Abstractions | 10.0.5 |
| Microsoft.Extensions.FileProviders.Physical | 10.0.5 |
| Microsoft.Extensions.FileSystemGlobbing | 10.0.5 |
| Microsoft.Extensions.Logging | 10.0.5 |
| Microsoft.Extensions.Options | 10.0.5 |
| Microsoft.Extensions.Options.ConfigurationExtensions | 10.0.5 |
| Microsoft.Extensions.Primitives | 10.0.5 |
| Microsoft.Extensions.Validation | 10.0.5 |
| Microsoft.JSInterop | 10.0.5 |
| Microsoft.JSInterop.WebAssembly | 10.0.5 |
| System.Diagnostics.EventLog | 10.0.5 |

---

## ライセンス要約

| ライセンス | 対象パッケージ数 | 主な義務 |
|---|---|---|
| MIT License | Cassia、ProcessX、および Microsoft 製パッケージ全般 | 著作権表示とライセンス文の保持 |
| Microsoft Software License Terms | Microsoft.TypeScript.MSBuild（ビルド時のみ） | Microsoft のライセンス条項に従う |

---

*最終更新: 2026-03-15*
