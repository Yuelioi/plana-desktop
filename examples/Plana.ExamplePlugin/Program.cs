using System.Text.Json;
using Plana.Core.Plugins;

var requestedVersion = args.Length >= 2 && args[0] == "--plana-protocol" ? args[1] : null;
if (requestedVersion != PluginProtocol.Version.ToString()) return 2;

using var input = Console.In;
using var output = Console.Out;
await output.WriteLineAsync(JsonSerializer.Serialize(
    PluginProtocol.Envelope("hello", "hello", new PluginHelloPayload("plana.example.hello", "1")),
    PluginProtocol.JsonOptions));
await output.FlushAsync();
string? pendingInvocationId = null;

while (await input.ReadLineAsync() is { } line)
{
    var envelope = JsonSerializer.Deserialize<PluginEnvelope>(line, PluginProtocol.JsonOptions);
    if (envelope?.Type == "initialize")
    {
        await output.WriteLineAsync(JsonSerializer.Serialize(
            PluginProtocol.Envelope(envelope.RequestId, "ready", new { }),
            PluginProtocol.JsonOptions));
        await output.FlushAsync();
        await output.WriteLineAsync(JsonSerializer.Serialize(
            PluginProtocol.Envelope("contribute", "contributeActions", new PluginContributionsPayload(
                [new PluginActionContribution("open-package-folder", "Open Plugin package folder", ["folder.open"])])),
            PluginProtocol.JsonOptions));
        await output.FlushAsync();
    }
    else if (envelope?.Type == "invoke")
    {
        var invocation = envelope.Payload.Deserialize<PluginInvokePayload>(PluginProtocol.JsonOptions);
        if (invocation?.ActionId != "open-package-folder")
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                PluginProtocol.Envelope(envelope.RequestId, "result", new PluginInvokeResultPayload(false, "The requested Plugin Action was not found.")),
                PluginProtocol.JsonOptions));
            await output.FlushAsync();
            continue;
        }
        pendingInvocationId = envelope.RequestId;
        await output.WriteLineAsync(JsonSerializer.Serialize(
            PluginProtocol.Envelope("host-request", "hostRequest", new PluginHostRequestPayload(
                "folder.open", new Dictionary<string, string> { ["path"] = "." })),
            PluginProtocol.JsonOptions));
        await output.FlushAsync();
    }
    else if (envelope?.Type == "hostResponse" && pendingInvocationId is not null)
    {
        var hostResponse = envelope.Payload.Deserialize<PluginHostResponsePayload>(PluginProtocol.JsonOptions);
        await output.WriteLineAsync(JsonSerializer.Serialize(
            PluginProtocol.Envelope(pendingInvocationId, "result", new PluginInvokeResultPayload(
                hostResponse?.Succeeded == true,
                hostResponse?.Message ?? "The host request failed.")),
            PluginProtocol.JsonOptions));
        await output.FlushAsync();
        pendingInvocationId = null;
    }
    else if (envelope?.Type == "stop")
    {
        return 0;
    }
}
return 0;
