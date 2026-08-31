using System.Text.Json;

namespace Plana.Core.Plugins;

public sealed record PluginManifest(
    int SchemaVersion,
    string Id,
    string Version,
    string Publisher,
    string HostApi,
    string EntryPoint,
    string DefaultLocale,
    IReadOnlyDictionary<string, string> Locales,
    IReadOnlySet<string> Capabilities,
    string PackageDirectory);

public sealed record PluginEnvelope(
    int ProtocolVersion,
    string RequestId,
    string Type,
    JsonElement Payload);

public sealed record PluginDiagnostic(string ManifestPath, PluginManifest? Manifest, string? Error)
{
    public bool IsValid => Manifest is not null && Error is null;
}

public sealed class PluginManifestException(string message) : Exception(message);
