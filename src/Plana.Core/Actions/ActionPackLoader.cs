using System.Text.Json;

namespace Plana.Core.Actions;

public sealed class ActionPackLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public async Task<ActionPackLoadResult> LoadDirectoryAsync(
        string directory,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directory))
        {
            return new ActionPackLoadResult([]);
        }

        var discoveries = new List<ActionPackDiscovery>();
        foreach (var manifestPath in Directory.EnumerateFiles(directory, "manifest.json", SearchOption.AllDirectories))
        {
            try
            {
                await using var stream = File.OpenRead(manifestPath);
                var manifest = await JsonSerializer.DeserializeAsync<ActionPackManifest>(stream, JsonOptions, cancellationToken)
                    ?? throw new ActionPackException("The manifest is empty.");
                if (manifest.SchemaVersion != 1)
                {
                    throw new ActionPackException($"Unsupported schema version {manifest.SchemaVersion}.");
                }
                discoveries.Add(new ActionPackDiscovery(manifestPath, manifest.ToActionPack(Path.GetDirectoryName(manifestPath)!), null));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ActionPackException)
            {
                discoveries.Add(new ActionPackDiscovery(manifestPath, null, exception.Message));
            }
        }
        return new ActionPackLoadResult(discoveries);
    }

    private sealed record ActionPackManifest(
        int SchemaVersion,
        string Id,
        string Name,
        string Version,
        string Publisher,
        List<ActionManifest>? Actions)
    {
        public ActionPack ToActionPack(string sourceDirectory) => new(
            Id,
            Name,
            Version,
            Publisher,
            Actions?.Select(action => action.ToAction()).ToArray() ?? [],
            SourceDirectory: sourceDirectory);
    }

    private sealed record ActionManifest(
        string Id,
        string Label,
        string Kind,
        Dictionary<string, string>? Parameters,
        HashSet<string>? Capabilities,
        bool RequiresConfirmation)
    {
        public ActionDefinition ToAction() => new(
            Id,
            Label,
            Kind,
            Parameters ?? new Dictionary<string, string>(),
            Capabilities ?? [],
            RequiresConfirmation);
    }
}
