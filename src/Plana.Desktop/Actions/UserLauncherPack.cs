using Plana.Core.Actions;
using Plana.Desktop.Localization;
using Plana.Core.Settings;

namespace Plana.Desktop.Actions;

internal static class UserLauncherPack
{
    public const string PackId = "user.actions";

    public static ActionPack Create(
        IEnumerable<UserActionSettings> userActions,
        IEnumerable<ProjectLauncherSettings> launchers)
    {
        var launcherActions = launchers
            .Where(launcher => !string.IsNullOrWhiteSpace(launcher.Id)
                && !string.IsNullOrWhiteSpace(launcher.Name)
                && !string.IsNullOrWhiteSpace(launcher.Executable))
            .Select(launcher => new ActionDefinition(
                $"user.launcher.{launcher.Id}",
                launcher.Name,
                ActionKinds.LaunchProcess,
                BuildParameters(launcher),
                new HashSet<string> { Capabilities.LaunchProcess }))
            .ToArray();
        var customActions = userActions
            .Where(action => !string.IsNullOrWhiteSpace(action.Id)
                && !string.IsNullOrWhiteSpace(action.Name)
                && RequiredCapability(action.Kind) is not null)
            .Select(action => new ActionDefinition(
                $"user.action.{action.Id}",
                action.Name,
                action.Kind,
                action.Parameters,
                new HashSet<string> { RequiredCapability(action.Kind)! },
                action.RequiresConfirmation));
        return new ActionPack(
            PackId,
            LocalizationCatalog.Text("YourActionsPackName"),
            "1",
            LocalizationCatalog.Text("YouPublisher"),
            [.. launcherActions, .. customActions]);
    }

    private static string? RequiredCapability(string kind) => kind switch
    {
        ActionKinds.OpenUrl => Capabilities.OpenUrl,
        ActionKinds.OpenFile => Capabilities.OpenFile,
        ActionKinds.OpenFolder => Capabilities.OpenFolder,
        ActionKinds.LaunchProcess => Capabilities.LaunchProcess,
        ActionKinds.RunCommand => Capabilities.RunCommand,
        ActionKinds.RunScript => Capabilities.RunScript,
        _ => null,
    };

    private static IReadOnlyDictionary<string, string> BuildParameters(ProjectLauncherSettings launcher)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["executable"] = launcher.Executable,
        };
        for (var index = 0; index < launcher.Arguments.Count; index++)
        {
            parameters[$"arg.{index}"] = launcher.Arguments[index]
                .Replace("{folder}", launcher.Folder, StringComparison.OrdinalIgnoreCase);
        }
        return parameters;
    }
}
