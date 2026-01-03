using Lucia.Server.Client.HubClients;

using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace Lucia.Server.Client;

internal class Program {
    static async Task Main(string[] args) {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        builder.Services.AddHubClient<SessionHubClient>();

        await builder.Build().RunAsync();
    }
}
