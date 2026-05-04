using System.Text.Json.Nodes;

var stdinContent = await Console.In.ReadToEndAsync();

JsonNode? root;
try
{
    root = JsonNode.Parse(stdinContent);
}
catch
{
    return;
}

var toolName = root?["tool_name"]?.GetValue<string>();
if (toolName != "Bash")
    return;

var command = root?["tool_input"]?["command"]?.GetValue<string>() ?? string.Empty;

if (command.Contains("git commit"))
{
    Console.Error.WriteLine("エラー: git commit はClaudeによる実行が禁止されています。コミットはユーザー自身が行ってください。");
    Environment.Exit(2);
}

if (command.Contains("git add"))
{
    Console.Error.WriteLine("エラー: git add はClaudeによる実行が禁止されています。ステージングはユーザー自身が行ってください。");
    Environment.Exit(2);
}
