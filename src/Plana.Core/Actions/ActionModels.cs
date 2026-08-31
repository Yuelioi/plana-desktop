using System.Collections.ObjectModel;

namespace Plana.Core.Actions;

public static class ActionKinds
{
    public const string PetAnimation = "pet.animation";
    public const string OpenUrl = "url.open";
    public const string LaunchProcess = "process.launch";
    public const string RunCommand = "command.run";
}

public static class Capabilities
{
    public const string OpenUrl = "url.open";
    public const string LaunchProcess = "process.launch";
    public const string RunCommand = "command.run";
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
    bool BuiltIn = false);

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
