using ConsoleAppFramework;
using Lucia.Installer.Commands;

var app = ConsoleApp.Create();
app.Add<InstallerCommands>();
app.Run(args);
