---
tags: msbuild,c#,installer
updated: 2026-03-20 13:04:20
---

# MSBuild EmbeddedResource による自動バンドル生成

## 目的

インストーラー（`MyApp.Installer.exe`）に配布物（`MyApp.Server` の発行物）を埋め込み、
`dotnet publish MyApp.Installer --configuration Release` 単体で完結させる。

## 構成

```xml
<!-- MyApp.Installer.csproj -->

<PropertyGroup>
  <ServerProject>$(MSBuildProjectDirectory)\..\MyApp.Server\MyApp.Server.csproj</ServerProject>
  <AppPublishDir>$(MSBuildProjectDirectory)\app-publish\</AppPublishDir>
  <AppBundleZip>$(MSBuildProjectDirectory)\app-bundle.zip</AppBundleZip>
</PropertyGroup>

<!-- Release 時のみ zip を EmbeddedResource に追加 -->
<ItemGroup Condition="'$(Configuration)' == 'Release'">
  <EmbeddedResource Include="$(AppBundleZip)" LogicalName="app-bundle.zip" />
</ItemGroup>

<!-- CoreCompile より前に実行される MSBuild Target -->
<Target Name="BuildServerBundle" BeforeTargets="CoreCompile"
        Condition="'$(Configuration)' == 'Release'">
  <Message Text="MyApp.Server をビルドしてバンドルを生成しています..." Importance="high" />
  <RemoveDir Directories="$(AppPublishDir)" />
  <Delete Files="$(AppBundleZip)" Condition="Exists('$(AppBundleZip)')" />
  <MSBuild Projects="$(ServerProject)"
           Targets="Restore;Publish"
           Properties="Configuration=Release;RuntimeIdentifier=win-x64;SelfContained=false;PublishDir=$(AppPublishDir)" />
  <ZipDirectory SourceDirectory="$(AppPublishDir)" DestinationFile="$(AppBundleZip)" Overwrite="true" />
</Target>
```

## ポイント

- `BeforeTargets="CoreCompile"` で C# コンパイル前に zip を生成することで、
  `EmbeddedResource` として確実に埋め込まれる
- `Condition="'$(Configuration)' == 'Release'"` で Debug ビルドをスキップ
  （Debug ビルドでは zip を埋め込まない）
- `LogicalName="app-bundle.zip"` でリソース名を固定
  （デフォルトはフルパスになりアクセスしにくい）

## ランタイム側での展開

```csharp
// アセンブリに埋め込まれたリソースを展開する
var assembly = Assembly.GetExecutingAssembly();
using var stream = assembly.GetManifestResourceStream("app-bundle.zip")
    ?? throw new InvalidOperationException("app-bundle.zip が埋め込まれていません");
ZipArchive archive = new(stream);
archive.ExtractToDirectory(targetPath, overwriteFiles: true);
```

## 注意：ZipArchive.ExtractToDirectory のタイムスタンプ

展開後のファイルのタイムスタンプは、ZIP に記録されたビルド時刻のまま保持される（展開時刻にならない）。
デプロイ成否の確認にファイルタイムスタンプは使えない。プロセス起動時刻で判断すること。
