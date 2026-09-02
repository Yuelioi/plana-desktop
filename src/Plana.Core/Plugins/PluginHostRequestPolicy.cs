using Plana.Core.Actions;

namespace Plana.Core.Plugins;

public static class PluginHostRequestPolicy
{
    public static string? RequiredCapability(string kind) => kind switch
    {
        ActionKinds.OpenUrl => Capabilities.OpenUrl,
        ActionKinds.OpenFile => Capabilities.OpenFile,
        ActionKinds.OpenFolder => Capabilities.OpenFolder,
        ActionKinds.LaunchProcess => Capabilities.LaunchProcess,
        ActionKinds.RunCommand => Capabilities.RunCommand,
        ActionKinds.RunScript => Capabilities.RunScript,
        "character.activate" => Capabilities.CharacterSelect,
        "companion.content.showImage" or "companion.content.preloadImage" or "companion.content.showFile" or "companion.content.restore" => Capabilities.CompanionContent,
        _ => null,
    };

    public static bool IsAllowed(string kind, IReadOnlySet<string> approvedActionCapabilities) =>
        RequiredCapability(kind) is { } capability && approvedActionCapabilities.Contains(capability);
}
