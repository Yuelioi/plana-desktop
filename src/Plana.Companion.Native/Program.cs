using Plana.Core.Companion;
using Plana.Core.Actions;
using Plana.Core.Settings;
using Plana.Core.Plugins;
using Plana.Core.Characters;

namespace Plana.Companion.Native;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (Environment.GetCommandLineArgs().Contains("--chat-probe", StringComparer.OrdinalIgnoreCase))
        {
            RunChatProbe();
            return;
        }
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
        var bundledCharacters = Path.Combine(AppContext.BaseDirectory, "CharacterPacks");
        var installedCharacters = Path.Combine(dataDirectory, "characters");
        var characterCatalog = new CharacterPackLoader().LoadCatalogAsync(bundledCharacters, installedCharacters).GetAwaiter().GetResult();
        var character = characterCatalog.SelectOrFallback(settings.SelectedCharacterPackId);
        settings.SelectedCharacterPackId = character.Manifest.Id;
        using var companion = CreateCompanion(pluginRuntime, settings, actions, character, bundledCharacters, installedCharacters);
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

    private static void RunChatProbe()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), "plana-chat-probe.json");
        try
        {
            using var client = new CodexAppServerClient(AiChatService.PersonaPrompt);
            var model = Environment.GetEnvironmentVariable("PLANA_CHAT_PROBE_MODEL") ?? "gpt-5.6-luna";
            var watch = System.Diagnostics.Stopwatch.StartNew();
            client.WarmUpAsync(model, CancellationToken.None).GetAwaiter().GetResult();
            var warmUpMilliseconds = watch.ElapsedMilliseconds;
            watch.Restart();
            var first = client.SendAsync(model, "只回复：老师好。", CancellationToken.None).GetAwaiter().GetResult();
            var firstMilliseconds = watch.ElapsedMilliseconds;
            watch.Restart();
            var second = client.SendAsync(model, "一加一等于几？只回复答案。", CancellationToken.None).GetAwaiter().GetResult();
            File.WriteAllText(outputPath, System.Text.Json.JsonSerializer.Serialize(new { succeeded = true, warmUpMilliseconds, firstMilliseconds, first, secondMilliseconds = watch.ElapsedMilliseconds, second }));
        }
        catch (Exception exception)
        {
            File.WriteAllText(outputPath, System.Text.Json.JsonSerializer.Serialize(new { succeeded = false, error = exception.ToString() }));
        }
    }

    private static ICompanionController CreateCompanion(
        PluginRuntimeManager pluginRuntime,
        DesktopSettings settings,
        IReadOnlyList<NativeActionEntry> actions,
        CharacterPack character,
        string bundledCharacters,
        string installedCharacters)
    {
        var godotPath = Environment.GetEnvironmentVariable("PLANA_GODOT_PATH")
            ?? Path.Combine(AppContext.BaseDirectory, "Godot", "Godot.exe");
        var projectPath = Environment.GetEnvironmentVariable("PLANA_GODOT_PROJECT")
            ?? Path.Combine(AppContext.BaseDirectory, "GodotRenderer");
        return File.Exists(godotPath) && File.Exists(Path.Combine(projectPath, "project.godot"))
            ? new GodotCompanionWindow(godotPath, projectPath, pluginRuntime, settings, actions, character, bundledCharacters, installedCharacters)
            : new NativeCompanionWindow(pluginRuntime);
    }

    private static IReadOnlyList<NativeActionEntry> LoadActions(DesktopSettings settings, string dataDirectory, IReadOnlyList<ActionPack> pluginPacks)
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
            for (var index = 0; index < project.Arguments.Count; index++) parameters[$"arg.{index}"] = project.Arguments[index].Replace("{folder}", project.Folder, StringComparison.OrdinalIgnoreCase);
            return new NativeActionEntry($"user.launcher.{project.Id}", project.Name,
                new ActionDefinition($"user.launcher.{project.Id}", project.Name, ActionKinds.LaunchProcess, parameters, Capability(ActionKinds.LaunchProcess)), project.Folder,
                project.Folder, chinese ? "项目" : "Projects");
        }));

        var loader = new ActionPackLoader();
        foreach (var directory in new[] { Path.Combine(AppContext.BaseDirectory, "StarterPacks"), Path.Combine(dataDirectory, "packs") })
        {
            var packs = loader.LoadDirectoryAsync(directory).GetAwaiter().GetResult();
            foreach (var pack in packs.ValidPacks.Where(pack => !settings.DisabledActionPacks.Contains(pack.Id)))
                result.AddRange(pack.Actions.Select(action => new NativeActionEntry(action.Id, action.Label, action, pack.SourceDirectory, "", pack.Name)));
        }
        foreach (var pack in pluginPacks)
            result.AddRange(pack.Actions.Select(action => new NativeActionEntry(action.Id, action.Label, action, pack.SourceDirectory, "", pack.Name)));
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
