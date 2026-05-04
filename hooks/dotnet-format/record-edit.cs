using System.Linq;
using System.Text.Json;
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

var filePath = root?["tool_input"]?["file_path"]?.GetValue<string>();
if (string.IsNullOrEmpty(filePath) || !filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
    return;

var normalized = filePath.Replace('\\', '/');
if (normalized.Contains("/obj/") || normalized.Contains("/bin/"))
    return;

var workingDir = Directory.GetCurrentDirectory();
var relativePath = Path.GetRelativePath(workingDir, filePath).Replace('\\', '/');
if (relativePath.StartsWith(".."))
    return;

var stateDir = Path.Combine(workingDir, "hooks", "state", "dotnet-format");
Directory.CreateDirectory(stateDir);
var stateFile = Path.Combine(stateDir, "pending.json");

JsonObject state;
if (File.Exists(stateFile))
{
    try { state = JsonNode.Parse(await File.ReadAllTextAsync(stateFile)) as JsonObject ?? new JsonObject(); }
    catch { state = new JsonObject(); }
}
else
{
    state = new JsonObject();
}

var files = state["files"] as JsonArray ?? new JsonArray();
if (!files.Any(f => (f as JsonValue)?.GetValue<string>() == relativePath))
{
    files.Add(relativePath);
    state["files"] = files;
}

var tempPath = stateFile + ".tmp";
await File.WriteAllTextAsync(tempPath, state.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
File.Move(tempPath, stateFile, overwrite: true);
