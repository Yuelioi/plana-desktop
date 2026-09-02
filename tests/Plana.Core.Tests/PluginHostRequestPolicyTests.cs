using Plana.Core.Actions;
using Plana.Core.Plugins;

namespace Plana.Core.Tests;

public sealed class PluginHostRequestPolicyTests
{
    [Fact]
    public void AllowsOnlyKindsCoveredByActionCapabilities()
    {
        var approved = new HashSet<string> { Capabilities.OpenFolder };

        Assert.True(PluginHostRequestPolicy.IsAllowed(ActionKinds.OpenFolder, approved));
        Assert.False(PluginHostRequestPolicy.IsAllowed(ActionKinds.RunScript, approved));
        Assert.False(PluginHostRequestPolicy.IsAllowed(ActionKinds.PluginInvoke, approved));
    }

    [Theory]
    [InlineData("character.activate", "character.select")]
    [InlineData("companion.content.showImage", "companion.content")]
    [InlineData("companion.content.preloadImage", "companion.content")]
    [InlineData("companion.content.showFile", "companion.content")]
    [InlineData("companion.content.restore", "companion.content")]
    public void MapsPluginV2HostCapabilities(string kind, string capability)
    {
        Assert.Equal(capability, PluginHostRequestPolicy.RequiredCapability(kind));
        Assert.True(PluginHostRequestPolicy.IsAllowed(kind, new HashSet<string> { capability }));
    }
}
