using System.IO.Pipes;
using System.Text.Json;
using Plana.Core.Plugins;

namespace Plana.Core.Tests;

public sealed class PluginProtocolSessionTests
{
    [Fact]
    public async Task CompletesHelloInitializeReadyHandshake()
    {
        var pipeName = $"plana-test-{Guid.NewGuid():N}";
        var manifest = Manifest();
        var serverTask = Task.Run(async () =>
        {
            await using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            await server.WaitForConnectionAsync();
            using var reader = new StreamReader(server, leaveOpen: true);
            using var writer = new StreamWriter(server, leaveOpen: true) { AutoFlush = true };
            await writer.WriteLineAsync(JsonSerializer.Serialize(
                PluginProtocol.Envelope("hello-1", "hello", new PluginHelloPayload(manifest.Id, manifest.HostApi)),
                PluginProtocol.JsonOptions));
            var initialize = JsonSerializer.Deserialize<PluginEnvelope>((await reader.ReadLineAsync())!, PluginProtocol.JsonOptions)!;
            Assert.Equal("initialize", initialize.Type);
            await writer.WriteLineAsync(JsonSerializer.Serialize(
                PluginProtocol.Envelope(initialize.RequestId, "ready", new { }),
                PluginProtocol.JsonOptions));
            await writer.WriteLineAsync(JsonSerializer.Serialize(
                PluginProtocol.Envelope("contribute-1", "contributeActions", new PluginContributionsPayload(
                    [new PluginActionContribution("hello", "Say hello", ["folder.open"])])),
                PluginProtocol.JsonOptions));
            var accepted = JsonSerializer.Deserialize<PluginEnvelope>((await reader.ReadLineAsync())!, PluginProtocol.JsonOptions)!;
            Assert.Equal("actionsAccepted", accepted.Type);
            var invoke = JsonSerializer.Deserialize<PluginEnvelope>((await reader.ReadLineAsync())!, PluginProtocol.JsonOptions)!;
            var invokePayload = invoke.Payload.Deserialize<PluginInvokePayload>(PluginProtocol.JsonOptions)!;
            await writer.WriteLineAsync(JsonSerializer.Serialize(
                PluginProtocol.Envelope("host-1", "hostRequest", new PluginHostRequestPayload(
                    "folder.open", new Dictionary<string, string> { ["path"] = "C:\\Projects" })),
                PluginProtocol.JsonOptions));
            var hostResponse = JsonSerializer.Deserialize<PluginEnvelope>((await reader.ReadLineAsync())!, PluginProtocol.JsonOptions)!;
            Assert.Equal("hostResponse", hostResponse.Type);
            Assert.True(hostResponse.Payload.Deserialize<PluginHostResponsePayload>(PluginProtocol.JsonOptions)!.Succeeded);
            await writer.WriteLineAsync(JsonSerializer.Serialize(
                PluginProtocol.Envelope(invoke.RequestId, "result", new PluginInvokeResultPayload(true, $"Invoked {invokePayload.ActionId}")),
                PluginProtocol.JsonOptions));
        });

        await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(1000);
        await using var session = new PluginProtocolSession(client);
        var contributions = await session.InitializeAsync(manifest, "en", TimeSpan.FromSeconds(1));
        var result = await session.InvokeAsync(
            "hello",
            new HashSet<string> { "folder.open" },
            (request, _) => Task.FromResult(new PluginHostResponsePayload(true, $"Opened {request.Parameters["path"]}")),
            TimeSpan.FromSeconds(1));
        Assert.Equal("hello", Assert.Single(contributions.Actions).Id);
        Assert.True(result.Succeeded);
        await serverTask;
    }

    [Fact]
    public async Task RejectsHelloFromDifferentPlugin()
    {
        var pipeName = $"plana-test-{Guid.NewGuid():N}";
        var serverTask = Task.Run(async () =>
        {
            await using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            await server.WaitForConnectionAsync();
            using var writer = new StreamWriter(server) { AutoFlush = true };
            await writer.WriteLineAsync(JsonSerializer.Serialize(
                PluginProtocol.Envelope("hello-1", "hello", new PluginHelloPayload("wrong.plugin", "2")),
                PluginProtocol.JsonOptions));
        });

        await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(1000);
        await using var session = new PluginProtocolSession(client);
        await Assert.ThrowsAsync<PluginProtocolException>(() =>
            session.InitializeAsync(Manifest(), "en", TimeSpan.FromSeconds(1)));
        await serverTask;
    }

    private static PluginManifest Manifest() => new(
        1, "example.plugin", "1.0.0", "Tests", "2", "plugin.exe", "en",
        new Dictionary<string, string>(), new HashSet<string> { "folder.open" }, "C:\\plugins\\example");
}
