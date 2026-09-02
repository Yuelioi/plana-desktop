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

    [Fact]
    public void PreservesActionDescription()
    {
        var contribution = new PluginActionContribution("describe", "Described", [], Description: "What this Plugin Action does.");

        var accepted = Assert.Single(PluginContributionPolicy.Validate(Manifest(new HashSet<string>()), [contribution]));

        Assert.Equal("What this Plugin Action does.", accepted.Description);
    }

    [Fact]
    public void AcceptsToolsContextsAndContentProvidersLinkedToActions()
    {
        var payload = new PluginContributionsPayload(
            [new PluginActionContribution("show", "Show", [])],
            [new PluginToolContribution("tool", "Show image", "show")],
            [new PluginContextContribution("context", "Show here", "show")],
            [new PluginContentProviderContribution("provider", "Images", "show")]);

        var accepted = PluginContributionPolicy.Validate(Manifest(new HashSet<string>()), payload);

        Assert.Single(accepted.Tools!);
        Assert.Single(accepted.ContextCommands!);
        Assert.Single(accepted.ContentProviders!);
    }

    [Fact]
    public void RejectsContributionLinkedToUnknownAction()
    {
        var payload = new PluginContributionsPayload([], [new PluginToolContribution("tool", "Broken", "missing")]);
        Assert.Throws<PluginProtocolException>(() => PluginContributionPolicy.Validate(Manifest(new HashSet<string>()), payload));
    }

    private static PluginManifest Manifest(IReadOnlySet<string> capabilities) => new(
        1, "example.plugin", "1.0.0", "Tests", "2", "plugin.exe", "en",
        new Dictionary<string, string>(), capabilities, "C:\\plugins\\example");
}
