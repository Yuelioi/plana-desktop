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
}
