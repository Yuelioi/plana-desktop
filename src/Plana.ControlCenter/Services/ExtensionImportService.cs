using Plana.Core.Actions;
using Plana.Core.Plugins;

namespace Plana_ControlCenter.Services;

internal sealed class ExtensionImportService(string dataDirectory)
{
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

internal sealed record ExtensionImportResult(bool Succeeded, string Message)
{
    public static ExtensionImportResult Success(string message) => new(true, message);
    public static ExtensionImportResult Failure(string message) => new(false, message);
}
