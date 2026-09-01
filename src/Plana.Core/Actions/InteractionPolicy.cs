namespace Plana.Core.Actions;

public static class InteractionPolicy
{
    public static IReadOnlyList<string> ConfigurableInteractions { get; } = ["click", "double-click"];

    public static bool TryGetBinding(IReadOnlyDictionary<string, string> bindings, string interaction, out string actionId)
    {
        if (bindings.TryGetValue(interaction, out actionId!)) return true;
        var legacy = interaction.Equals("double-click", StringComparison.OrdinalIgnoreCase) ? "doubleClick" : null;
        return legacy is not null && bindings.TryGetValue(legacy, out actionId!);
    }

    public static bool IsAutomationEligible(ActionDefinition action) =>
        action.Kind == ActionKinds.PetAnimation && action.Capabilities.Count == 0;
}
