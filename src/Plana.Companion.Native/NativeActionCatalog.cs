using Plana.Core.Actions;
using Plana.Core.Settings;

namespace Plana.Companion.Native;

internal static class NativeActionCatalog
{
    public static IReadOnlyList<NativeActionEntry> Load(
        DesktopSettings settings,
        string dataDirectory,
        IReadOnlyList<ActionPack> pluginPacks)
    {
        var chinese = settings.UiCulture.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        var result = new List<NativeActionEntry>();
        result.AddRange(settings.UserActions.Select(action => new NativeActionEntry(
            $"user.action.{action.Id}", action.Name,
            new ActionDefinition($"user.action.{action.Id}", action.Name, action.Kind, action.Parameters, Capability(action.Kind)), null,
            action.Description, chinese ? "我的动作" : "My actions")));
        result.AddRange(settings.ProjectLaunchers.Select(project =>
        {
            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["executable"] = project.Executable };
            for (var index = 0; index < project.Arguments.Count; index++)
                parameters[$"arg.{index}"] = project.Arguments[index].Replace("{folder}", project.Folder, StringComparison.OrdinalIgnoreCase);
            return new NativeActionEntry($"user.launcher.{project.Id}", project.Name,
                new ActionDefinition($"user.launcher.{project.Id}", project.Name, ActionKinds.LaunchProcess, parameters, Capability(ActionKinds.LaunchProcess)), project.Folder,
                project.Folder, chinese ? "项目" : "Projects");
        }));

        var loader = new ActionPackLoader();
        foreach (var directory in new[] { Path.Combine(AppContext.BaseDirectory, "StarterPacks"), Path.Combine(dataDirectory, "packs") })
        {
            var packs = loader.LoadDirectoryAsync(directory).GetAwaiter().GetResult();
            foreach (var pack in packs.ValidPacks.Where(pack => !settings.DisabledActionPacks.Contains(pack.Id)))
                result.AddRange(pack.Actions.Select(action => new NativeActionEntry(
                    action.Id, action.Label, action, pack.SourceDirectory, action.Description, pack.Name)));
        }
        foreach (var pack in pluginPacks)
            result.AddRange(pack.Actions.Select(action => new NativeActionEntry(
                action.Id, action.Label, action, pack.SourceDirectory, action.Description, pack.Name)));
        return result;
    }

    private static HashSet<string> Capability(string kind) => [kind switch
    {
        ActionKinds.OpenUrl => Capabilities.OpenUrl,
        ActionKinds.OpenFile => Capabilities.OpenFile,
        ActionKinds.OpenFolder => Capabilities.OpenFolder,
        ActionKinds.RunCommand => Capabilities.RunCommand,
        ActionKinds.RunScript => Capabilities.RunScript,
        _ => Capabilities.LaunchProcess,
    }];
}
