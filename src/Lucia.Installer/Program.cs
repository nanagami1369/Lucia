using System.Text;
using ConsoleAppFramework;
using Lucia.Installer;
using Lucia.Installer.Services;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

// 引数なし: ステータス表示（ConsoleAppFramework はデフォルトでヘルプを表示するため先行処理）
if (args.Length == 0)
{
    ShowDefaultStatus();
    return;
}

// 不明なコマンドを ConsoleAppFramework に渡す前に検出する
// ConsoleAppFramework は不明コマンドをヘルプ表示+exit 0 で処理するため独自にチェックする
string[] knownCommands = ["install", "uninstall", "modify", "status", "--help", "-h", "--version"];
if (!knownCommands.Contains(args[0]))
{
    Console.Error.WriteLine($"エラー: 不明なコマンド '{args[0]}'");
    Console.Error.WriteLine("使い方を確認するには: Lucia.Installer.exe --help");
    Environment.Exit(1);
    return;
}

var app = ConsoleApp.Create();
app.Add<InstallerCommands>();
await app.RunAsync(args);

static void ShowDefaultStatus()
{
    var options = InstallService.ReadInstallOptionsFromRegistry();
    if (options is not null)
    {
        Console.WriteLine("Lucia はインストール済みです。");
        Console.WriteLine($"  インストール先: {options.InstallDirectory}");
        Console.WriteLine($"  ポート番号    : {options.Port}");
        Console.WriteLine($"  サブネット    : {options.AllowedSubnet}");
        Console.WriteLine();
        Console.WriteLine("利用可能なコマンド: install / uninstall / modify / status / --help");
    }
    else
    {
        Console.WriteLine("Lucia はインストールされていません。");
        Console.WriteLine("インストールするには: Lucia.Installer.exe install");
    }
}
