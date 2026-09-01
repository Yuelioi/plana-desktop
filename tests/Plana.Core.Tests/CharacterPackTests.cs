using Plana.Core.Characters;
using Plana.Core.Companion;

namespace Plana.Core.Tests;

public sealed class CharacterPackTests
{
    [Fact]
    public async Task LoadsContainedSpineAssetsAndPlansSemanticPerformance()
    {
        var root = CreatePack("test.arona", validTexture: true);
        try
        {
            var discovery = await new CharacterPackLoader().LoadAsync(Path.Combine(root, "character.json"));
            var pack = Assert.IsType<CharacterPack>(discovery.Pack);
            Assert.Single(pack.TexturePaths);
            var plan = new CharacterPerformancePlanner(pack).Plan(new CharacterPerformanceIntent(CharacterEmotion.Happy, CharacterGesture.HeadPat));
            Assert.Equal(["S_Pat_01_M_all", "16", "Idle_01"], plan.Cues.Select(cue => cue.Animation));
            Assert.True(plan.Cues[^1].Loop);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RejectsTraversalAndMissingAtlasTextures()
    {
        var traversal = CreatePack("test.traversal", validTexture: true, skeleton: "../outside.skel");
        var missingTexture = CreatePack("test.missing", validTexture: false);
        try
        {
            var first = await new CharacterPackLoader().LoadAsync(Path.Combine(traversal, "character.json"));
            var second = await new CharacterPackLoader().LoadAsync(Path.Combine(missingTexture, "character.json"));
            Assert.Contains("escapes", first.Error, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("texture", second.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(traversal, true); Directory.Delete(missingTexture, true); }
    }

    [Fact]
    public async Task CatalogRejectsDuplicateIdsAndFallsBackToBundledPlana()
    {
        var bundled = Path.Combine(Path.GetTempPath(), $"plana-characters-{Guid.NewGuid():N}");
        var installed = Path.Combine(Path.GetTempPath(), $"plana-characters-{Guid.NewGuid():N}");
        Directory.CreateDirectory(bundled);
        Directory.CreateDirectory(installed);
        var plana = CreatePack(CharacterPackLoader.BundledPlanaId, true, parent: bundled);
        var duplicateOne = CreatePack("duplicate.pack", true, parent: bundled);
        var duplicateTwo = CreatePack("duplicate.pack", true, parent: installed);
        try
        {
            var catalog = await new CharacterPackLoader().LoadCatalogAsync(bundled, installed);
            Assert.Single(catalog.Discoveries, item => item.Error?.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) == true);
            Assert.Contains(catalog.ValidPacks, pack => pack.Manifest.Id == "duplicate.pack" && pack.BuiltIn);
            Assert.Equal(CharacterPackLoader.BundledPlanaId, catalog.SelectOrFallback("missing").Manifest.Id);
        }
        finally { Directory.Delete(bundled, true); Directory.Delete(installed, true); }
    }

    private static string CreatePack(string id, bool validTexture, string skeleton = "model.skel", string? parent = null)
    {
        var root = Path.Combine(parent ?? Path.GetTempPath(), $"character-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "model.skel"), "fixture");
        File.WriteAllText(Path.Combine(root, "model.atlas"), "model.png\nsize: 16,16\nformat: RGBA8888\nfilter: Linear,Linear\nrepeat: none\n");
        if (validTexture) File.WriteAllBytes(Path.Combine(root, "model.png"), [0]);
        File.WriteAllText(Path.Combine(root, "character.json"), $$"""
            {
              "schemaVersion": 1,
              "id": "{{id}}",
              "name": "Test Character",
              "version": "1.0.0",
              "skeleton": "{{skeleton.Replace("\\", "\\\\")}}",
              "atlas": "model.atlas",
              "performance": {
                "idle": "Idle_01",
                "emotions": { "Neutral": "00", "Happy": "16" },
                "gestures": { "HeadPat": "S_Pat_01_M_all" }
              }
            }
            """);
        return root;
    }
}
