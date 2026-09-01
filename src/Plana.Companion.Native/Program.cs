using Plana.Core.Companion;
using Plana.Core.Actions;
using Plana.Core.Settings;
using Plana.Core.Plugins;

namespace Plana.Companion.Native;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var singleInstance = new Mutex(true, @"Local\PlanaDesktop.Native", out var ownsInstance);
        if (!ownsInstance) return;

        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PlanaDesktop");
        var settingsPath = Path.Combine(dataDirectory, "settings.json");
        var settings = new DesktopSettingsStore(settingsPath)
            .LoadAsync().GetAwaiter().GetResult();
        var state = new CompanionSurfaceState(
            settings.Left,
            settings.Top,
            settings.Width,
            settings.Height,
            settings.Scale,
            settings.AlwaysOnTop);

        var pluginRuntime = new PluginRuntimeManager(Path.Combine(AppContext.BaseDirectory, "PluginHost", "Plana.PluginHost.exe"));
        var pluginDiagnostics = new PluginManifestLoader().LoadDirectoryAsync(Path.Combine(dataDirectory, "plugins")).GetAwaiter().GetResult();
        pluginRuntime.ReconcileAsync(pluginDiagnostics, settings, settings.UiCulture).GetAwaiter().GetResult();
        var actions = LoadActions(settings, dataDirectory, pluginRuntime.SnapshotActionPacks());
        using var companion = CreateCompanion(pluginRuntime, settings, actions);
        companion.Apply(state);
        if (companion is NativeCompanionWindow nativeCompanion)
        {
            try
            {
                nativeCompanion.InitializeRenderer(Path.Combine(AppContext.BaseDirectory, "Renderer"), settings, actions);
            }
            catch (Exception exception)
            {
                File.WriteAllText(Path.Combine(dataDirectory, "renderer-error.log"), exception.ToString());
            }
        }
        if (companion is NativeCompanionWindow currentNative) currentNative.WatchSettings(settingsPath);
        if (companion is GodotCompanionWindow godotCompanion) godotCompanion.WatchSettings(settingsPath);
        using var controlServer = companion is GodotCompanionWindow controlledGodot ? new CompanionControlServer(controlledGodot) : null;
        using var tray = new NativeTrayIcon(companion, settings);
        companion.Show();
        companion.RunMessageLoop();
        var finalState = companion.Snapshot();
        settings.Left = finalState.Left;
        settings.Top = finalState.Top;
        new DesktopSettingsStore(settingsPath).SaveAsync(settings).GetAwaiter().GetResult();
        pluginRuntime.TerminateAll();
        Environment.Exit(0);
    }

    private static ICompanionController CreateCompanion(PluginRuntimeManager pluginRuntime, DesktopSettings settings, IReadOnlyList<NativeActionEntry> actions)
    {
        var godotPath = Environment.GetEnvironmentVariable("PLANA_GODOT_PATH")
            ?? Path.Combine(AppContext.BaseDirectory, "Godot", "Godot.exe");
        var projectPath = Environment.GetEnvironmentVariable("PLANA_GODOT_PROJECT")
            ?? Path.Combine(AppContext.BaseDirectory, "GodotRenderer");
        return File.Exists(godotPath) && File.Exists(Path.Combine(projectPath, "project.godot"))
            ? new GodotCompanionWindow(godotPath, projectPath, pluginRuntime, settings, actions)
            : new NativeCompanionWindow(pluginRuntime);
    }

    private static IReadOnlyList<NativeActionEntry> LoadActions(DesktopSettings settings, string dataDirectory, IReadOnlyList<ActionPack> pluginPacks)
    {
        var result = new List<NativeActionEntry>();
        result.AddRange(settings.UserActions.Select(action => new NativeActionEntry(
            $"user.action.{action.Id}", action.Name,
            new ActionDefinition($"user.action.{action.Id}", action.Name, action.Kind, action.Parameters, Capability(action.Kind)), null)));
        result.AddRange(settings.ProjectLaunchers.Select(project =>
        {
            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["executable"] = project.Executable };
            for (var index = 0; index < project.Arguments.Count; index++) parameters[$"arg.{index}"] = project.Arguments[index].Replace("{folder}", project.Folder, StringComparison.OrdinalIgnoreCase);
            return new NativeActionEntry($"user.launcher.{project.Id}", project.Name,
                new ActionDefinition($"user.launcher.{project.Id}", project.Name, ActionKinds.LaunchProcess, parameters, Capability(ActionKinds.LaunchProcess)), project.Folder);
        }));

        var loader = new ActionPackLoader();
        foreach (var directory in new[] { Path.Combine(AppContext.BaseDirectory, "StarterPacks"), Path.Combine(dataDirectory, "packs") })
        {
            var packs = loader.LoadDirectoryAsync(directory).GetAwaiter().GetResult();
            foreach (var pack in packs.ValidPacks.Where(pack => !settings.DisabledActionPacks.Contains(pack.Id)))
                result.AddRange(pack.Actions.Select(action => new NativeActionEntry(action.Id, action.Label, action, pack.SourceDirectory)));
        }
        foreach (var pack in pluginPacks)
            result.AddRange(pack.Actions.Select(action => new NativeActionEntry(action.Id, action.Label, action, pack.SourceDirectory)));
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
