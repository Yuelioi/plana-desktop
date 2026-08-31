using Plana.Core.Actions;

namespace Plana.Desktop.Actions;

internal static class BuiltInActionPack
{
    public static ActionPack Create() => new(
        "builtin", "Plana Desktop", "1.0.0", "Plana Desktop",
        [new ActionDefinition(
            "builtin.companion.interact", "Play a random animation", ActionKinds.PetAnimation,
            new Dictionary<string, string> { ["animation"] = "random" }, new HashSet<string>())],
        BuiltIn: true);
}
