using Plana.Core.Actions;

namespace Plana.Core.Tests;

public sealed class ActionPackLoaderTests
{
    [Fact]
    public async Task LoadsADeclarativeManifest()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"plana-pack-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "manifest.json"), """
                {
                  "schemaVersion": 1,
                  "id": "test.pack",
                  "name": "Test Pack",
                  "version": "1.0.0",
                  "publisher": "Tests",
                  "actions": [{
                    "id": "test.open",
                    "label": "Open",
                    "kind": "url.open",
                    "parameters": { "url": "https://example.com" },
                    "capabilities": ["url.open"]
                  }]
                }
                """);

            var result = await new ActionPackLoader().LoadDirectoryAsync(directory);

            var pack = Assert.Single(result.ValidPacks);
            Assert.Equal("test.pack", pack.Id);
            Assert.Equal("test.open", Assert.Single(pack.Actions).Id);
            Assert.Equal(directory, pack.SourceDirectory);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task ReportsMalformedManifestWithoutDiscardingValidPacks()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"plana-pack-{Guid.NewGuid():N}");
        var goodDirectory = Path.Combine(directory, "good");
        var badDirectory = Path.Combine(directory, "bad");
        Directory.CreateDirectory(goodDirectory);
        Directory.CreateDirectory(badDirectory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(goodDirectory, "manifest.json"), """
                { "schemaVersion": 1, "id": "good.pack", "name": "Good", "version": "1", "publisher": "Tests", "actions": [] }
                """);
            await File.WriteAllTextAsync(Path.Combine(badDirectory, "manifest.json"), "{ not-json");

            var result = await new ActionPackLoader().LoadDirectoryAsync(directory);

            Assert.Equal(2, result.Discoveries.Count);
            Assert.Single(result.ValidPacks);
            var invalid = Assert.Single(result.Discoveries, discovery => !discovery.IsValid);
            Assert.NotNull(invalid.Error);
            Assert.Contains("bad", invalid.ManifestPath);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
