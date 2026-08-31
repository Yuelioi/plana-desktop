using System.Text.Json;

namespace Plana.Core.Plugins;

public sealed class PluginManifestLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public async Task<IReadOnlyList<PluginDiagnostic>> LoadDirectoryAsync(
        string directory,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directory)) return [];
        var diagnostics = new List<PluginDiagnostic>();
        foreach (var manifestPath in Directory.EnumerateFiles(directory, "plugin.json", SearchOption.AllDirectories))
        {
            diagnostics.Add(await LoadAsync(manifestPath, cancellationToken));
        }
        return diagnostics;
    }

    public async Task<PluginDiagnostic> LoadAsync(string manifestPath, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var stream = File.OpenRead(manifestPath);
            var data = await JsonSerializer.DeserializeAsync<ManifestData>(stream, JsonOptions, cancellationToken)
                ?? throw new PluginManifestException("The manifest is empty.");
            var packageDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
            Validate(data, packageDirectory);
            return new PluginDiagnostic(manifestPath, new PluginManifest(
                data.SchemaVersion,
                data.Id,
                data.Version,
                data.Publisher,
                data.HostApi,
                data.EntryPoint,
                string.IsNullOrWhiteSpace(data.DefaultLocale) ? "en" : data.DefaultLocale,
                data.Locales ?? new Dictionary<string, string>(),
                data.Capabilities ?? new HashSet<string>(),
                packageDirectory), null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or PluginManifestException)
        {
            return new PluginDiagnostic(manifestPath, null, exception.Message);
        }
    }

    public static string ResolveEntryPoint(PluginManifest manifest)
    {
        var entryPoint = Path.GetFullPath(manifest.EntryPoint, manifest.PackageDirectory);
        var relative = Path.GetRelativePath(manifest.PackageDirectory, entryPoint);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new PluginManifestException("The plugin entry point must stay inside its package directory.");
        }
        return entryPoint;
    }

    private static void Validate(ManifestData data, string packageDirectory)
    {
        if (data.SchemaVersion != 1) throw new PluginManifestException($"Unsupported schema version {data.SchemaVersion}.");
        if (!string.Equals(data.HostApi, "1", StringComparison.Ordinal)) throw new PluginManifestException($"Unsupported host API '{data.HostApi}'.");
        if (string.IsNullOrWhiteSpace(data.Id) || string.IsNullOrWhiteSpace(data.Version) || string.IsNullOrWhiteSpace(data.Publisher))
        {
            throw new PluginManifestException("Plugin id, version, and publisher are required.");
        }
        if (string.IsNullOrWhiteSpace(data.EntryPoint)) throw new PluginManifestException("Plugin entryPoint is required.");
        var candidate = new PluginManifest(data.SchemaVersion, data.Id, data.Version, data.Publisher, data.HostApi,
            data.EntryPoint, data.DefaultLocale, data.Locales ?? new Dictionary<string, string>(),
            data.Capabilities ?? new HashSet<string>(), packageDirectory);
        _ = ResolveEntryPoint(candidate);
    }

    private sealed record ManifestData(
        int SchemaVersion,
        string Id,
        string Version,
        string Publisher,
        string HostApi,
        string EntryPoint,
        string DefaultLocale,
        Dictionary<string, string>? Locales,
        HashSet<string>? Capabilities);
}
