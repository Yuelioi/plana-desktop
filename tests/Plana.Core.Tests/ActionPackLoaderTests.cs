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

            var packs = await new ActionPackLoader().LoadDirectoryAsync(directory);

            var pack = Assert.Single(packs);
            Assert.Equal("test.pack", pack.Id);
            Assert.Equal("test.open", Assert.Single(pack.Actions).Id);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
