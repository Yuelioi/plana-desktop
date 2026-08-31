using System.Diagnostics;
using System.IO;
using Plana.Core.Actions;
using Plana.Core.Plugins;

namespace Plana.Desktop.Extensions;

internal sealed class ExtensionLibrary(string appDataDirectory)
{
    public string ActionPacksDirectory { get; } = Path.Combine(appDataDirectory, "packs");
    public string PluginsDirectory { get; } = Path.Combine(appDataDirectory, "plugins");

    public void OpenActionPacksDirectory() => OpenDirectory(ActionPacksDirectory);
    public void OpenPluginsDirectory() => OpenDirectory(PluginsDirectory);
    public void OpenManifestLocation(string manifestPath) => OpenDirectory(Path.GetDirectoryName(Path.GetFullPath(manifestPath))!);

    public async Task<ExtensionImportResult> ImportActionPackAsync(string sourceDirectory)
    {
        var manifestPath = Path.Combine(sourceDirectory, "manifest.json");
        if (!File.Exists(manifestPath)) return ExtensionImportResult.Failure("ManifestMissing", manifestPath);
        var loaded = await new ActionPackLoader().LoadDirectoryAsync(sourceDirectory);
        var discovery = loaded.Discoveries.FirstOrDefault(item =>
            string.Equals(Path.GetFullPath(item.ManifestPath), Path.GetFullPath(manifestPath), StringComparison.OrdinalIgnoreCase));
        if (discovery?.IsValid != true) return ExtensionImportResult.Failure("ManifestInvalid", discovery?.Error);
        return ImportDirectory(sourceDirectory, ActionPacksDirectory, discovery.Pack!.Id);
    }

    public async Task<ExtensionImportResult> ImportPluginAsync(string sourceDirectory)
    {
        var manifestPath = Path.Combine(sourceDirectory, "plugin.json");
        if (!File.Exists(manifestPath)) return ExtensionImportResult.Failure("ManifestMissing", manifestPath);
        var diagnostic = await new PluginManifestLoader().LoadAsync(manifestPath);
        if (!diagnostic.IsValid) return ExtensionImportResult.Failure("ManifestInvalid", diagnostic.Error);
        var entryPoint = PluginManifestLoader.ResolveEntryPoint(diagnostic.Manifest!);
        if (!File.Exists(entryPoint)) return ExtensionImportResult.Failure("EntryPointMissing", entryPoint);
        return ImportDirectory(sourceDirectory, PluginsDirectory, diagnostic.Manifest!.Id);
    }

    private static ExtensionImportResult ImportDirectory(string sourceDirectory, string libraryDirectory, string id)
    {
        if (id.Any(character => !(char.IsLetterOrDigit(character) || character is '.' or '-' or '_')))
        {
            return ExtensionImportResult.Failure("IdInvalid", id);
        }
        Directory.CreateDirectory(libraryDirectory);
        var source = Path.GetFullPath(sourceDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var destination = Path.GetFullPath(Path.Combine(libraryDirectory, id)).TrimEnd(Path.DirectorySeparatorChar);
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
        {
            return ExtensionImportResult.Success("AlreadyInstalled", destination);
        }
        if (Directory.Exists(destination)) return ExtensionImportResult.Failure("AlreadyExists", destination);

        Directory.CreateDirectory(destination);
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
            }
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(destination, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
            return ExtensionImportResult.Success("Imported", destination);
        }
        catch
        {
            if (Directory.Exists(destination)) Directory.Delete(destination, recursive: true);
            throw;
        }
    }

    private static void OpenDirectory(string directory)
    {
        Directory.CreateDirectory(directory);
        var startInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
        startInfo.ArgumentList.Add(directory);
        Process.Start(startInfo);
    }
}

internal sealed record ExtensionImportResult(bool Succeeded, string Code, string? Detail)
{
    public static ExtensionImportResult Success(string code, string? detail = null) => new(true, code, detail);
    public static ExtensionImportResult Failure(string code, string? detail = null) => new(false, code, detail);
}
