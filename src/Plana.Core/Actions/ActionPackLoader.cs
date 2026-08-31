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

    public async Task<IReadOnlyList<ActionPack>> LoadDirectoryAsync(
        string directory,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var packs = new List<ActionPack>();
        foreach (var manifestPath in Directory.EnumerateFiles(directory, "manifest.json", SearchOption.AllDirectories))
        {
            await using var stream = File.OpenRead(manifestPath);
            var manifest = await JsonSerializer.DeserializeAsync<ActionPackManifest>(stream, JsonOptions, cancellationToken)
                ?? throw new ActionPackException($"Manifest '{manifestPath}' is empty.");
            if (manifest.SchemaVersion != 1)
            {
                throw new ActionPackException($"Manifest '{manifestPath}' uses unsupported schema version {manifest.SchemaVersion}.");
            }
            packs.Add(manifest.ToActionPack());
        }
        return packs;
    }

    private sealed record ActionPackManifest(
        int SchemaVersion,
        string Id,
        string Name,
        string Version,
        string Publisher,
        List<ActionManifest>? Actions)
    {
        public ActionPack ToActionPack() => new(
            Id,
            Name,
            Version,
            Publisher,
            Actions?.Select(action => action.ToAction()).ToArray() ?? []);
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
