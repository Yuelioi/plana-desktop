using Plana.Core.Actions;
using Plana.Core.Plugins;

namespace Plana.Companion.Native;

internal static class NativePluginBroker
{
    public static Task<PluginHostResponsePayload> ExecuteAsync(
        PluginHostRequestPayload request,
        string? workingDirectory,
        CancellationToken cancellationToken)
    {
        if (PluginHostRequestPolicy.RequiredCapability(request.Kind) is not { } capability)
        {
            return Task.FromResult(new PluginHostResponsePayload(false, $"Host request kind '{request.Kind}' is not brokered."));
        }
        var definition = new ActionDefinition(
            "plugin.host-request",
            "Plugin host request",
            request.Kind,
            request.Parameters ?? new Dictionary<string, string>(),
            new HashSet<string> { capability });
        var result = NativeActionExecutor.Execute(new NativeActionEntry(definition.Id, definition.Label, definition, workingDirectory));
        return Task.FromResult(new PluginHostResponsePayload(result.Succeeded, result.Message));
    }
}
