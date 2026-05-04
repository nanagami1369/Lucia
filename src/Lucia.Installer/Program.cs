using System.Diagnostics;
using System.Security.Principal;
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

// 管理者権限が必要なコマンドは、権限不足の場合に runas で自己再起動して UAC プロンプトを表示する
string[] requiresAdminCommands = ["install", "uninstall", "modify"];
if (requiresAdminCommands.Contains(args[0]))
{
    var identity = WindowsIdentity.GetCurrent();
    var principal = new WindowsPrincipal(identity);
    if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
    {
        var currentExePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("現在のプロセスパスを取得できませんでした。");
        var quotedArgs = args.Select(arg => arg.Contains(' ') ? $"\"{arg}\"" : arg);
        var processStartInfo = new ProcessStartInfo
        {
            FileName = currentExePath,
            Arguments = string.Join(" ", quotedArgs),
            Verb = "runas",
            UseShellExecute = true,
        };
        Process.Start(processStartInfo);
        return;
    }
}

var app = ConsoleApp.Create();
app.Add<InstallerCommands>();
await app.RunAsync(args);
