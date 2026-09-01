using Plana.Core.Actions;
using Plana.Core.Plugins;
using Plana.Core.Characters;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;

namespace Plana_ControlCenter.Services;

internal sealed class ExtensionImportService(string dataDirectory)
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes(2) };
    public async Task<ExtensionImportResult> ImportPackAsync(string sourceDirectory)
    {
        var manifest = Path.Combine(sourceDirectory, "manifest.json");
        if (!File.Exists(manifest)) return ExtensionImportResult.Failure("manifest.json was not found in the selected folder.");
        var result = await new ActionPackLoader().LoadDirectoryAsync(sourceDirectory);
        var discovery = result.Discoveries.FirstOrDefault(item => Path.GetFullPath(item.ManifestPath).Equals(Path.GetFullPath(manifest), StringComparison.OrdinalIgnoreCase));
        if (discovery?.IsValid != true) return ExtensionImportResult.Failure(discovery?.Error ?? "The Action Pack manifest is invalid.");
        return CopyPackage(sourceDirectory, Path.Combine(dataDirectory, "packs"), discovery.Pack!.Id);
    }

    public async Task<ExtensionImportResult> ImportPluginAsync(string sourceDirectory)
    {
        var manifest = Path.Combine(sourceDirectory, "plugin.json");
        if (!File.Exists(manifest)) return ExtensionImportResult.Failure("plugin.json was not found in the selected folder.");
        var diagnostic = await new PluginManifestLoader().LoadAsync(manifest);
        if (!diagnostic.IsValid) return ExtensionImportResult.Failure(diagnostic.Error ?? "The Plugin manifest is invalid.");
        var entryPoint = PluginManifestLoader.ResolveEntryPoint(diagnostic.Manifest!);
        if (!File.Exists(entryPoint)) return ExtensionImportResult.Failure($"Plugin entry point was not found: {entryPoint}");
        return CopyPackage(sourceDirectory, Path.Combine(dataDirectory, "plugins"), diagnostic.Manifest!.Id);
    }

    public async Task<ExtensionImportResult> ImportCharacterPackAsync(string sourceDirectory)
    {
        var manifest = Path.Combine(sourceDirectory, CharacterPackLoader.ManifestFileName);
        if (!File.Exists(manifest)) return ExtensionImportResult.Failure("character.json was not found in the selected folder.");
        var discovery = await new CharacterPackLoader().LoadAsync(manifest);
        if (!discovery.IsValid) return ExtensionImportResult.Failure(discovery.Error ?? "The Character Pack manifest is invalid.");
        return CopyPackage(sourceDirectory, Path.Combine(dataDirectory, "characters"), discovery.Pack!.Manifest.Id);
    }

    public async Task<ExtensionImportResult> ImportCharacterPackageAsync(string packagePath)
    {
        var temporary = Path.Combine(Path.GetTempPath(), $"plana-character-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            await using var stream = File.OpenRead(packagePath);
            var package = await JsonSerializer.DeserializeAsync<CharacterPackageDocument>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (package is null || package.SchemaVersion != 1 || package.Manifest.ValueKind != JsonValueKind.Object)
                return ExtensionImportResult.Failure("The .planacharacter package is invalid.");
            if (package.Assets.Count is 0 or > 12) return ExtensionImportResult.Failure("A Character Package must declare between 1 and 12 assets.");
            await File.WriteAllTextAsync(Path.Combine(temporary, CharacterPackLoader.ManifestFileName), package.Manifest.GetRawText());
            foreach (var asset in package.Assets)
            {
                if (!Uri.TryCreate(asset.Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                    return ExtensionImportResult.Failure($"Character asset URL must use HTTPS: {asset.Url}");
                var target = ResolveContained(temporary, asset.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var response = await HttpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is > 25_000_000) return ExtensionImportResult.Failure($"Character asset is too large: {asset.Path}");
                await using (var output = File.Create(target)) await response.Content.CopyToAsync(output);
                if (!string.IsNullOrWhiteSpace(asset.Sha256))
                {
                    await using var input = File.OpenRead(target);
                    var hash = Convert.ToHexString(await SHA256.HashDataAsync(input));
                    if (!hash.Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase)) return ExtensionImportResult.Failure($"Character asset hash did not match: {asset.Path}");
                }
                else if (!string.IsNullOrWhiteSpace(asset.GitBlobSha1))
                {
                    var hash = await ComputeGitBlobSha1Async(target);
                    if (!hash.Equals(asset.GitBlobSha1, StringComparison.OrdinalIgnoreCase)) return ExtensionImportResult.Failure($"Character asset hash did not match: {asset.Path}");
                }
                else return ExtensionImportResult.Failure($"Character asset has no integrity hash: {asset.Path}");
            }
            return await ImportCharacterPackAsync(temporary);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or HttpRequestException or JsonException or InvalidDataException)
        {
            return ExtensionImportResult.Failure(exception.Message);
        }
        finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
    }

    private static async Task<string> ComputeGitBlobSha1Async(string path)
    {
        await using var input = File.OpenRead(path);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        hash.AppendData(Encoding.UTF8.GetBytes($"blob {input.Length}\0"));
        var buffer = new byte[81920];
        for (var read = await input.ReadAsync(buffer); read > 0; read = await input.ReadAsync(buffer)) hash.AppendData(buffer.AsSpan(0, read));
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string ResolveContained(string root, string relative)
    {
        if (Path.IsPathFullyQualified(relative)) throw new InvalidDataException("Character asset path must be relative.");
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(relative, root);
        if (!target.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Character asset path escapes the package.");
        return target;
    }

    private static ExtensionImportResult CopyPackage(string sourceDirectory, string libraryDirectory, string id)
    {
        if (id.Any(character => !(char.IsLetterOrDigit(character) || character is '.' or '-' or '_')))
            return ExtensionImportResult.Failure($"Package ID contains unsupported characters: {id}");
        var source = Path.GetFullPath(sourceDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var destination = Path.GetFullPath(Path.Combine(libraryDirectory, id)).TrimEnd(Path.DirectorySeparatorChar);
        if (source.Equals(destination, StringComparison.OrdinalIgnoreCase)) return ExtensionImportResult.Success("This package is already installed.");
        if (Directory.Exists(destination)) return ExtensionImportResult.Failure($"A package with ID '{id}' is already installed.");

        Directory.CreateDirectory(destination);
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(destination, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
            return ExtensionImportResult.Success($"Installed '{id}'.");
        }
        catch
        {
            if (Directory.Exists(destination)) Directory.Delete(destination, recursive: true);
            throw;
        }
    }
}

internal sealed class CharacterPackageDocument
{
    public int SchemaVersion { get; set; }
    public JsonElement Manifest { get; set; }
    public List<CharacterPackageAsset> Assets { get; set; } = [];
}

internal sealed class CharacterPackageAsset
{
    public string Path { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string GitBlobSha1 { get; set; } = string.Empty;
}

internal sealed record ExtensionImportResult(bool Succeeded, string Message)
{
    public static ExtensionImportResult Success(string message) => new(true, message);
    public static ExtensionImportResult Failure(string message) => new(false, message);
}
