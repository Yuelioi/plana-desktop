using System.Text.Json;

namespace Plana.Core.Plugins;

public static class PluginProtocol
{
    public const int Version = 1;
    public const int MaximumMessageCharacters = 1024 * 1024;
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    public static PluginEnvelope Envelope(string requestId, string type, object payload) => new(
        Version,
        requestId,
        type,
        JsonSerializer.SerializeToElement(payload, JsonOptions));
}

public sealed record PluginHelloPayload(string PluginId, string HostApi);
public sealed record PluginInitializePayload(string Culture, IReadOnlyList<string> ApprovedCapabilities);
public sealed record PluginActionContribution(
    string Id,
    string Label,
    string[] Capabilities,
    bool RequiresConfirmation = false);
public sealed record PluginContributionsPayload(PluginActionContribution[] Actions);
public sealed record PluginInvokePayload(string ActionId);
public sealed record PluginInvokeResultPayload(bool Succeeded, string Message);
public sealed record PluginHostRequestPayload(string Kind, Dictionary<string, string> Parameters);
public sealed record PluginHostResponsePayload(bool Succeeded, string Message);

public sealed class PluginProtocolException(string message) : Exception(message);
