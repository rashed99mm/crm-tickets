using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// A real Kestrel listener the outbound senders can actually POST to, so "calls the configured URL"
/// (CC-6) is asserted over real HTTP rather than against a mocked handler. Records the raw request
/// bodies it receives and answers 200 — a stand-in for the sandbox no WhatsApp Business account
/// exists to point at (spec A11).
/// </summary>
public sealed class StubGatewayServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly ConcurrentQueue<string> _receivedBodies;

    private StubGatewayServer(WebApplication app, string baseUrl, ConcurrentQueue<string> receivedBodies)
    {
        _app = app;
        BaseUrl = baseUrl;
        _receivedBodies = receivedBodies;
    }

    /// <summary>The reachable URL, port assigned by the OS at start.</summary>
    public string BaseUrl { get; }

    public IReadOnlyCollection<string> ReceivedBodies => _receivedBodies.ToArray();

    public static async Task<StubGatewayServer> StartAsync()
    {
        var receivedBodies = new ConcurrentQueue<string>();

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        var app = builder.Build();

        app.MapPost("/messages", async (HttpContext context) =>
        {
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();
            receivedBodies.Enqueue(body);
            return Results.Ok();
        });

        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.First();

        return new StubGatewayServer(app, address, receivedBodies);
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}