namespace Plana.Core.Actions;

public static class InteractionPolicy
{
    public static IReadOnlyList<string> ConfigurableInteractions { get; } = ["click", "double-click"];

    public static bool IsAutomationEligible(ActionDefinition action) =>
        action.Kind == ActionKinds.PetAnimation && action.Capabilities.Count == 0;
}
