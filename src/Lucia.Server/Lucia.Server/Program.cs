using Lucia.Server.BackgroundServices;
using Lucia.Server.Components;
using Lucia.Server.Hubs;
using Lucia.Services;
using Lucia.Services.Power;
using Lucia.Services.Sessions;

using LuciaServer.Shared;

namespace Lucia.Server;

public class Program {
    public static void Main(string[] args) {
        var builder = WebApplication.CreateBuilder(args);

        builder.Host.UseWindowsService();

        // ロガー追加
        builder.Services.AddTransient(typeof(StatsLogger<>));

        // サービス追加
        builder.Services.AddSingleton<ISessionService, SessionService>();
        builder.Services.AddSingleton<IPowerService, PowerService>();

        // バックグラウンドサービス追加
        builder.Services.AddHostedService<SessionFetchWorker>();

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment()) {
            app.UseWebAssemblyDebugging();
        } else {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseHttpsRedirection();

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

        app.MapHub<SessionHub>("/sessionhub");
        app.MapHub<PowerHub>("/powerhub");

        app.Run();
    }
}
