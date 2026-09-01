using System.Text.Json;

namespace Plana.Core.Tests;

public sealed class CharacterPackageCatalogTests
{
    [Fact]
    public void CatalogPackagesHaveUniqueIdsAndVerifiedHttpsAssets()
    {
        var directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "release", "characters"));
        var files = Directory.GetFiles(directory, "*.planacharacter", SearchOption.AllDirectories);
        Assert.Equal(932, files.Length);
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var categories = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var root = document.RootElement;
            Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
            var category = root.TryGetProperty("category", out var categoryElement) ? categoryElement.GetString()! : "pure";
            categories[category] = categories.GetValueOrDefault(category) + 1;
            var manifest = root.GetProperty("manifest");
            Assert.True(ids.Add(manifest.GetProperty("id").GetString()!));
            var assets = root.GetProperty("assets").EnumerateArray().ToArray();
            Assert.InRange(assets.Length, 1, 12);
            foreach (var asset in assets)
            {
                Assert.Equal(Uri.UriSchemeHttps, new Uri(asset.GetProperty("url").GetString()!).Scheme);
                Assert.True(asset.TryGetProperty("sha256", out _) || asset.TryGetProperty("gitBlobSha1", out _));
            }
        }
        Assert.Equal(1, categories["pure"]);
        Assert.Equal(273, categories["scene"]);
        Assert.Equal(658, categories["static"]);
    }
}
