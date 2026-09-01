using System.Text.Json;
using System.Text.RegularExpressions;
using Plana.Core.Companion;

namespace Plana.Core.Characters;

public sealed class CharacterPackManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Skeleton { get; set; } = string.Empty;
    public string Atlas { get; set; } = string.Empty;
    public CharacterLayoutManifest Layout { get; set; } = new();
    public CharacterPerformanceManifest Performance { get; set; } = new();
}

public sealed class CharacterLayoutManifest
{
    public double X { get; set; } = 320;
    public double Y { get; set; } = 835;
    public double Scale { get; set; } = 0.36;
    public List<CharacterPointManifest> HitPolygon { get; set; } = [];
}

public sealed class CharacterPointManifest
{
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class CharacterPerformanceManifest
{
    public string Idle { get; set; } = "Idle_01";
    public string? Speaking { get; set; }
    public Dictionary<string, string> Emotions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Gestures { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record CharacterPack(
    CharacterPackManifest Manifest,
    string SourceDirectory,
    string SkeletonPath,
    string AtlasPath,
    IReadOnlyList<string> TexturePaths,
    bool BuiltIn);

public sealed record CharacterPackDiscovery(string ManifestPath, CharacterPack? Pack, string? Error)
{
    public bool IsValid => Pack is not null && Error is null;
}

public sealed record CharacterPackCatalog(IReadOnlyList<CharacterPackDiscovery> Discoveries)
{
    public IReadOnlyList<CharacterPack> ValidPacks { get; } = Discoveries
        .Where(item => item.IsValid)
        .Select(item => item.Pack!)
        .ToArray();

    public CharacterPack SelectOrFallback(string? id) =>
        ValidPacks.FirstOrDefault(pack => pack.Manifest.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
        ?? ValidPacks.FirstOrDefault(pack => pack.Manifest.Id.Equals(CharacterPackLoader.BundledPlanaId, StringComparison.OrdinalIgnoreCase))
        ?? ValidPacks.First(pack => pack.BuiltIn);
}

public sealed class CharacterPackLoader
{
    public const string ManifestFileName = "character.json";
    public const string BundledPlanaId = "builtin.plana";
    private static readonly Regex IdPattern = new("^[a-z0-9][a-z0-9._-]{2,63}$", RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<CharacterPackCatalog> LoadCatalogAsync(string bundledRoot, string installedRoot)
    {
        var discoveries = new List<CharacterPackDiscovery>();
        await DiscoverAsync(bundledRoot, builtIn: true, discoveries);
        await DiscoverAsync(installedRoot, builtIn: false, discoveries);
        foreach (var group in discoveries.Where(item => item.Pack is not null)
                     .GroupBy(item => item.Pack!.Manifest.Id, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            var builtIns = group.Where(item => item.Pack!.BuiltIn).ToArray();
            var preferred = builtIns.Length == 1 ? builtIns[0] : null;
            discoveries = discoveries.Select(item => item.Pack is not null &&
                    item.Pack.Manifest.Id.Equals(group.Key, StringComparison.OrdinalIgnoreCase) && !ReferenceEquals(item, preferred)
                ? item with { Pack = null, Error = $"Duplicate Character Pack ID: {item.Pack.Manifest.Id}" }
                : item).ToList();
        }
        return new CharacterPackCatalog(discoveries);
    }

    public async Task<CharacterPackDiscovery> LoadAsync(string manifestPath, bool builtIn = false)
    {
        try
        {
            var fullManifest = Path.GetFullPath(manifestPath);
            var root = Path.GetDirectoryName(fullManifest)!;
            await using var stream = File.OpenRead(fullManifest);
            var manifest = await JsonSerializer.DeserializeAsync<CharacterPackManifest>(stream, JsonOptions)
                ?? throw new InvalidDataException("Character Pack manifest is empty.");
            ValidateManifest(manifest);
            var skeleton = ResolveContained(root, manifest.Skeleton);
            var atlas = ResolveContained(root, manifest.Atlas);
            if (!File.Exists(skeleton)) throw new FileNotFoundException("Character skeleton was not found.", skeleton);
            if (!File.Exists(atlas)) throw new FileNotFoundException("Character atlas was not found.", atlas);
            if (!skeleton.EndsWith(".skel", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Character skeleton must be a .skel file.");
            if (!atlas.EndsWith(".atlas", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Character atlas must be an .atlas file.");
            var textures = ReadAtlasTextures(root, atlas);
            return new CharacterPackDiscovery(fullManifest,
                new CharacterPack(manifest, root, skeleton, atlas, textures, builtIn), null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException)
        {
            return new CharacterPackDiscovery(Path.GetFullPath(manifestPath), null, exception.Message);
        }
    }

    private async Task DiscoverAsync(string root, bool builtIn, List<CharacterPackDiscovery> discoveries)
    {
        if (!Directory.Exists(root)) return;
        foreach (var manifest in Directory.EnumerateFiles(root, ManifestFileName, SearchOption.AllDirectories).Order())
            discoveries.Add(await LoadAsync(manifest, builtIn));
    }

    private static void ValidateManifest(CharacterPackManifest manifest)
    {
        if (manifest.SchemaVersion != 1) throw new InvalidDataException($"Unsupported Character Pack schemaVersion: {manifest.SchemaVersion}");
        if (!IdPattern.IsMatch(manifest.Id)) throw new InvalidDataException("Character Pack ID must use lowercase letters, digits, dots, underscores, or hyphens.");
        if (string.IsNullOrWhiteSpace(manifest.Name)) throw new InvalidDataException("Character Pack name is required.");
        if (string.IsNullOrWhiteSpace(manifest.Version)) throw new InvalidDataException("Character Pack version is required.");
        if (string.IsNullOrWhiteSpace(manifest.Skeleton) || string.IsNullOrWhiteSpace(manifest.Atlas)) throw new InvalidDataException("Character skeleton and atlas are required.");
        if (string.IsNullOrWhiteSpace(manifest.Performance.Idle)) throw new InvalidDataException("Character idle animation is required.");
        if (manifest.Layout.Scale is < 0.05 or > 5) throw new InvalidDataException("Character layout scale must be between 0.05 and 5.");
        foreach (var key in manifest.Performance.Emotions.Keys)
            if (!Enum.TryParse<CharacterEmotion>(key, true, out _)) throw new InvalidDataException($"Unknown Character Emotion: {key}");
        foreach (var key in manifest.Performance.Gestures.Keys)
            if (!Enum.TryParse<CharacterGesture>(key, true, out _)) throw new InvalidDataException($"Unknown Character Gesture: {key}");
        foreach (var point in manifest.Layout.HitPolygon)
            if (point.X is < 0 or > 1 || point.Y is < 0 or > 1) throw new InvalidDataException("Character hit polygon points must be normalized from 0 to 1.");
    }

    private static IReadOnlyList<string> ReadAtlasTextures(string root, string atlasPath)
    {
        var lines = File.ReadAllLines(atlasPath);
        var textures = new List<string>();
        for (var index = 0; index + 1 < lines.Length; index++)
        {
            var name = lines[index].Trim();
            if (name.Length == 0 || char.IsWhiteSpace(lines[index], 0) || !lines[index + 1].TrimStart().StartsWith("size:", StringComparison.OrdinalIgnoreCase)) continue;
            var texture = ResolveContained(root, name);
            if (!File.Exists(texture)) throw new FileNotFoundException("Character atlas texture was not found.", texture);
            textures.Add(texture);
        }
        if (textures.Count == 0) throw new InvalidDataException("Character atlas did not declare any texture pages.");
        return textures;
    }

    private static string ResolveContained(string root, string relativePath)
    {
        if (Path.IsPathFullyQualified(relativePath)) throw new InvalidDataException("Character Pack paths must be relative.");
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(relativePath, root);
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Character Pack path escapes its directory.");
        return fullPath;
    }
}

public sealed class CharacterPerformancePlanner(CharacterPack pack)
{
    public CharacterPerformancePlan Plan(CharacterPerformanceIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        var performance = pack.Manifest.Performance;
        var cues = new List<SpineAnimationCue>();
        if (intent.Gesture != CharacterGesture.None && performance.Gestures.TryGetValue(intent.Gesture.ToString(), out var gesture) && !string.IsNullOrWhiteSpace(gesture))
            cues.Add(new SpineAnimationCue(gesture));
        var expression = intent.IsSpeaking && !string.IsNullOrWhiteSpace(performance.Speaking)
            ? performance.Speaking
            : performance.Emotions.GetValueOrDefault(intent.Emotion.ToString())
              ?? performance.Emotions.GetValueOrDefault(CharacterEmotion.Neutral.ToString());
        if (!string.IsNullOrWhiteSpace(expression)) cues.Add(new SpineAnimationCue(expression));
        cues.Add(new SpineAnimationCue(performance.Idle, Loop: true));
        return new CharacterPerformancePlan(cues);
    }
}
