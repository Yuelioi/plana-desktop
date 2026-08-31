using Plana.Core.Plugins;

namespace Plana.Core.Tests;

public sealed class PluginManifestLoaderTests
{
    [Fact]
    public async Task LoadsVersionedPluginManifest()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"plana-plugin-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(directory, "plugin"));
        try
        {
            var manifestPath = Path.Combine(directory, "plugin.json");
            await File.WriteAllTextAsync(manifestPath, """
                {
                  "schemaVersion": 1,
                  "id": "example.plugin",
                  "version": "1.0.0",
                  "publisher": "Tests",
                  "hostApi": "1",
                  "entryPoint": "plugin/example.exe",
                  "defaultLocale": "en",
                  "locales": { "en": "locales/en.json" },
                  "capabilities": ["folder.open"]
                }
                """);

            var diagnostic = await new PluginManifestLoader().LoadAsync(manifestPath);

            Assert.True(diagnostic.IsValid, diagnostic.Error);
            Assert.Equal("example.plugin", diagnostic.Manifest!.Id);
            Assert.Equal(Path.Combine(directory, "plugin", "example.exe"), PluginManifestLoader.ResolveEntryPoint(diagnostic.Manifest));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task RejectsEntryPointOutsidePackage()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"plana-plugin-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var manifestPath = Path.Combine(directory, "plugin.json");
            await File.WriteAllTextAsync(manifestPath, """
                {
                  "schemaVersion": 1,
                  "id": "example.plugin",
                  "version": "1.0.0",
                  "publisher": "Tests",
                  "hostApi": "1",
                  "entryPoint": "../escape.exe",
                  "defaultLocale": "en",
                  "locales": {},
                  "capabilities": []
                }
                """);

            var diagnostic = await new PluginManifestLoader().LoadAsync(manifestPath);

            Assert.False(diagnostic.IsValid);
            Assert.Contains("inside", diagnostic.Error);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task DiscoversPluginManifestsRecursively()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"plana-plugin-{Guid.NewGuid():N}");
        var pluginDirectory = Path.Combine(directory, "example");
        Directory.CreateDirectory(pluginDirectory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(pluginDirectory, "plugin.json"), """
                {
                  "schemaVersion": 1,
                  "id": "example.plugin",
                  "version": "1.0.0",
                  "publisher": "Tests",
                  "hostApi": "1",
                  "entryPoint": "plugin.exe",
                  "defaultLocale": "en",
                  "locales": {},
                  "capabilities": []
                }
                """);

            var diagnostics = await new PluginManifestLoader().LoadDirectoryAsync(directory);

            Assert.Single(diagnostics);
            Assert.True(diagnostics[0].IsValid, diagnostics[0].Error);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
