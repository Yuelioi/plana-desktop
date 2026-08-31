using System.Collections.ObjectModel;

namespace Plana.Core.Actions;

public static class ActionKinds
{
    public const string PetAnimation = "pet.animation";
    public const string OpenUrl = "url.open";
    public const string OpenFile = "file.open";
    public const string OpenFolder = "folder.open";
    public const string LaunchProcess = "process.launch";
    public const string RunCommand = "command.run";
    public const string RunScript = "script.run";
    public const string PluginInvoke = "plugin.invoke";
}

public static class Capabilities
{
    public const string OpenUrl = "url.open";
    public const string OpenFile = "file.open";
    public const string OpenFolder = "folder.open";
    public const string LaunchProcess = "process.launch";
    public const string RunCommand = "command.run";
    public const string RunScript = "script.run";
}

public sealed record ActionDefinition(
    string Id,
    string Label,
    string Kind,
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlySet<string> Capabilities,
    bool RequiresConfirmation = false);

public sealed record ActionPack(
    string Id,
    string Name,
    string Version,
    string Publisher,
    IReadOnlyList<ActionDefinition> Actions,
    bool BuiltIn = false,
    string? SourceDirectory = null);

public sealed record ActionPackDiscovery(
    string ManifestPath,
    ActionPack? Pack,
    string? Error)
{
    public bool IsValid => Pack is not null && Error is null;
    public string DisplayName => Pack?.Name ?? Path.GetFileName(Path.GetDirectoryName(ManifestPath)) ?? "Unknown pack";
}

public sealed record ActionPackLoadResult(IReadOnlyList<ActionPackDiscovery> Discoveries)
{
    public IReadOnlyList<ActionPack> ValidPacks { get; } = Discoveries
        .Where(discovery => discovery.IsValid)
        .Select(discovery => discovery.Pack!)
        .ToArray();
}

public sealed record ActionContext(
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string>? Variables = null)
{
    public IReadOnlyDictionary<string, string> VariablesOrEmpty { get; } =
        Variables ?? ReadOnlyDictionary<string, string>.Empty;
}

public sealed record ActionResult(bool Succeeded, string Message)
{
    public static ActionResult Success(string message = "Action completed") => new(true, message);
    public static ActionResult Failure(string message) => new(false, message);
}

public sealed record ActionDescriptor(string PackId, string PackName, ActionDefinition Action);
