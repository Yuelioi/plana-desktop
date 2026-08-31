using Plana.Core.Plugins;

namespace Plana.Core.Tests;

public sealed class PluginContributionPolicyTests
{
    [Fact]
    public void RejectsCapabilitiesAbsentFromManifest()
    {
        var manifest = Manifest(new HashSet<string> { "folder.open" });
        var contribution = new PluginActionContribution("run", "Run", ["script.run"]);

        Assert.Throws<PluginProtocolException>(() => PluginContributionPolicy.Validate(manifest, [contribution]));
    }

    [Fact]
    public void RejectsDuplicateActionIds()
    {
        var manifest = Manifest(new HashSet<string>());
        var contribution = new PluginActionContribution("same", "Same", []);

        Assert.Throws<PluginProtocolException>(() => PluginContributionPolicy.Validate(manifest, [contribution, contribution]));
    }

    private static PluginManifest Manifest(IReadOnlySet<string> capabilities) => new(
        1, "example.plugin", "1.0.0", "Tests", "1", "plugin.exe", "en",
        new Dictionary<string, string>(), capabilities, "C:\\plugins\\example");
}
