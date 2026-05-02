using System.Text;
using ConsoleAppFramework;
using Lucia.Installer;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

// 引数なし: install を実行する
// ダブルクリック起動でもインストーラーとして機能するようにするため。
if (args.Length == 0)
{
    args = ["install"];
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
