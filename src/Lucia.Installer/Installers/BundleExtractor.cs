using System.IO.Compression;
using System.Reflection;

namespace Lucia.Installer.Installers;

/// <summary>
/// インストーラーに同梱された Lucia.Server のバンドル（zip）を展開するクラス。
/// </summary>
public static class BundleExtractor
{
    /// <summary>埋め込みリソースとして格納されたバンドルの論理名。</summary>
    private const string BundleResourceName = "app-bundle.zip";

    /// <summary>
    /// 埋め込みバンドルを指定ディレクトリに展開する。
    /// </summary>
    /// <param name="targetDirectory">展開先ディレクトリのパス。存在しない場合は作成される。</param>
    /// <exception cref="InvalidOperationException">埋め込みリソースが見つからない場合にスローされる。</exception>
    public static void ExtractTo(string targetDirectory)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(BundleResourceName))
            ?? throw new InvalidOperationException(
                $"埋め込みリソース '{BundleResourceName}' が見つかりません。" +
                "Release 構成でビルドされているか確認してください。");

        Directory.CreateDirectory(targetDirectory);

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);
        zipArchive.ExtractToDirectory(targetDirectory, overwriteFiles: true);
    }
}
