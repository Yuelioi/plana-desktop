using Plana.Core.Actions;
using Plana.Desktop.Localization;

namespace Plana.Desktop.Actions;

internal static class BuiltInActionPack
{
    public static ActionPack Create() => new(
        "builtin", LocalizationCatalog.Text("AppName"), "1.0.0", LocalizationCatalog.Text("AppName"),
        [new ActionDefinition(
            "builtin.companion.interact", LocalizationCatalog.Text("BuiltInInteractAction"), ActionKinds.PetAnimation,
            new Dictionary<string, string> { ["animation"] = "random" }, new HashSet<string>())],
        BuiltIn: true);
}
