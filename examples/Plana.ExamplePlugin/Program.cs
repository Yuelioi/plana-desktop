using System.Text.Json;
using Plana.Core.Plugins;

await using var imageCache = new RandomImageCache();

var requestedVersion = args.Length >= 2 && args[0] == "--plana-protocol" ? args[1] : null;
if (requestedVersion != PluginProtocol.Version.ToString()) return 2;

using var input = Console.In;
using var output = Console.Out;
await output.WriteLineAsync(JsonSerializer.Serialize(
    PluginProtocol.Envelope("hello", "hello", new PluginHelloPayload("plana.random-images", "2")),
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
        imageCache.Start();
        await output.WriteLineAsync(JsonSerializer.Serialize(
            PluginProtocol.Envelope("contribute", "contributeActions", new PluginContributionsPayload(
                [
                    new PluginActionContribution("open-package-folder", "Open Plugin package folder", ["folder.open"], Description: "Open the folder containing this Plugin package."),
                    new PluginActionContribution("activate-plana", "Switch to Plana", ["character.select"], Description: "Switch the current Companion to the bundled Plana Character Pack."),
                    new PluginActionContribution("random-plana-image", "Random Plana image", ["companion.content"], Description: "Display a random image from pln.yuelili.com on the Companion."),
                    new PluginActionContribution("restore-content", "Restore character content", ["companion.content"], Description: "Restore the animated character after Plugin content.")
                ],
                ContextCommands: [
                    new PluginContextContribution("random-image-context", "显示随机普拉娜图片", "random-plana-image"),
                    new PluginContextContribution("restore-context", "恢复动态角色", "restore-content")
                ],
                ContentProviders: [new PluginContentProviderContribution("plana-images", "普拉娜随机图片", "random-plana-image")])),
            PluginProtocol.JsonOptions));
        await output.FlushAsync();
    }
    else if (envelope?.Type == "invoke")
    {
        var invocation = envelope.Payload.Deserialize<PluginInvokePayload>(PluginProtocol.JsonOptions);
        PluginHostRequestPayload? hostRequest = invocation?.ActionId switch
        {
            "open-package-folder" => new PluginHostRequestPayload("folder.open", new Dictionary<string, string> { ["path"] = "." }),
            "activate-plana" => new PluginHostRequestPayload("character.activate", new Dictionary<string, string> { ["id"] = "builtin.plana" }),
            "restore-content" => new PluginHostRequestPayload("companion.content.restore", new Dictionary<string, string>()),
            _ => null,
        };
        if (invocation?.ActionId == "random-plana-image")
        {
            try
            {
                hostRequest = new PluginHostRequestPayload("companion.content.showFile", new Dictionary<string, string> { ["path"] = await imageCache.TakeAsync() });
            }
            catch (Exception exception) when (exception is IOException or TaskCanceledException)
            {
                await output.WriteLineAsync(JsonSerializer.Serialize(
                    PluginProtocol.Envelope(envelope.RequestId, "result", new PluginInvokeResultPayload(false, $"Image cache is not ready: {exception.Message}")),
                    PluginProtocol.JsonOptions));
                await output.FlushAsync();
                continue;
            }
        }
        if (hostRequest is null)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                PluginProtocol.Envelope(envelope.RequestId, "result", new PluginInvokeResultPayload(false, "The requested Plugin Action was not found.")),
                PluginProtocol.JsonOptions));
            await output.FlushAsync();
            continue;
        }
        pendingInvocationId = envelope.RequestId;
        await output.WriteLineAsync(JsonSerializer.Serialize(
            PluginProtocol.Envelope("host-request", "hostRequest", hostRequest),
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
