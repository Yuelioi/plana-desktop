using Plana.Core.Actions;

namespace Plana.Core.Tests;

public sealed class InteractionPolicyTests
{
    [Fact]
    public void ReservesRightClickAndExcludesIdleFromInteractions()
    {
        Assert.Equal(["click", "double-click"], InteractionPolicy.ConfigurableInteractions);
        Assert.DoesNotContain("right-click", InteractionPolicy.ConfigurableInteractions);
        Assert.DoesNotContain("idle", InteractionPolicy.ConfigurableInteractions);
    }

    [Fact]
    public void AutomationAllowsOnlyCapabilityFreeCompanionAnimations()
    {
        var ambient = new ActionDefinition("ambient", "Ambient", ActionKinds.PetAnimation, new Dictionary<string, string>(), new HashSet<string>());
        var external = new ActionDefinition("external", "External", ActionKinds.OpenFolder, new Dictionary<string, string>(), new HashSet<string> { Capabilities.OpenFolder });

        Assert.True(InteractionPolicy.IsAutomationEligible(ambient));
        Assert.False(InteractionPolicy.IsAutomationEligible(external));
    }
}
