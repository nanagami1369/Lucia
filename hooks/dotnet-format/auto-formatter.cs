using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;

var workingDir = Directory.GetCurrentDirectory();
var stateFile = Path.Combine(workingDir, "hooks", "state", "dotnet-format", "pending.json");

if (!File.Exists(stateFile))
    return;

JsonObject? state;
try { state = JsonNode.Parse(await File.ReadAllTextAsync(stateFile)) as JsonObject; }
catch { return; }

var files = (state?["files"] as JsonArray)?
    .Select(f => (f as JsonValue)?.GetValue<string>())
    .Where(f => !string.IsNullOrEmpty(f))
    .ToArray();

if (files is null || files.Length == 0)
    return;

var includeArgs = string.Join(" ", files.Select(f => $"--include \"{f}\""));

using var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = $"format whitespace Lucia.sln {includeArgs} --verbosity quiet --no-restore",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        WorkingDirectory = workingDir,
    }
};

process.Start();
var readStdout = process.StandardOutput.ReadToEndAsync();
var readStderr = process.StandardError.ReadToEndAsync();
await process.WaitForExitAsync();
_ = await readStdout;
var stderrText = await readStderr;

if (process.ExitCode != 0)
{
    Console.Error.WriteLine($"[auto-formatter] エラー: {stderrText.Trim()}");
    Environment.Exit(1);
}
