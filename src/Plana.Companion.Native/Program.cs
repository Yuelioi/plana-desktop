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
        var actions = NativeActionCatalog.Load(settings, dataDirectory, pluginRuntime.SnapshotActionPacks());
        var bundledCharacters = Path.Combine(AppContext.BaseDirectory, "CharacterPacks");
        var installedCharacters = Path.Combine(dataDirectory, "characters");
        var characterCatalog = new CharacterPackLoader().LoadCatalogAsync(bundledCharacters, installedCharacters).GetAwaiter().GetResult();
        var character = characterCatalog.SelectOrFallback(settings.SelectedCharacterPackId);
        settings.SelectedCharacterPackId = character.Manifest.Id;
        using var companion = CreateCompanion(pluginRuntime, settings, actions, character, bundledCharacters, installedCharacters);
        companion.Apply(state);
        companion.WatchSettings(settingsPath);
        using var controlServer = new CompanionControlServer(companion);
        using var tray = new NativeTrayIcon(companion, settings);
        companion.Show();
        companion.RunMessageLoop();
        var finalState = companion.Snapshot();
        var finalSettings = new DesktopSettingsStore(settingsPath).LoadAsync().GetAwaiter().GetResult();
        finalSettings.Left = finalState.Left;
        finalSettings.Top = finalState.Top;
        new DesktopSettingsStore(settingsPath).SaveAsync(finalSettings).GetAwaiter().GetResult();
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
            if (Environment.GetEnvironmentVariable("PLANA_CHAT_PROBE_CONFIGURATION") == "1")
            {
                var settings = new DesktopSettings { UiCulture = "zh-CN" };
                watch.Restart();
                var raw = client.SendAsync(model, AiConfigurationEditor.BuildPrompt(settings,
                    "创建一个名为‘打开哔哩哔哩’的动作，网址是 https://www.bilibili.com/，并放入‘娱乐’动作组。"), CancellationToken.None).GetAwaiter().GetResult();
                File.WriteAllText(outputPath + ".raw", raw);
                File.WriteAllText(outputPath, System.Text.Json.JsonSerializer.Serialize(new { succeeded = false, raw }));
                var result = AiConfigurationEditor.Apply(settings, raw);
                File.WriteAllText(outputPath, System.Text.Json.JsonSerializer.Serialize(new
                {
                    succeeded = result.Changed && settings.UserActions.Count == 1 && settings.ToolGroups.Count == 1,
                    warmUpMilliseconds,
                    configurationMilliseconds = watch.ElapsedMilliseconds,
                    result.Message,
                    result.Changes,
                    settings.UserActions,
                    settings.ToolGroups,
                    raw,
                }));
                return;
            }
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

    private static GodotCompanionWindow CreateCompanion(
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
        if (!File.Exists(godotPath))
            throw new FileNotFoundException("The production Godot executable is missing. Run build.ps1 -Publish and start the Host from artifacts/native-win-x64.", godotPath);
        if (!File.Exists(Path.Combine(projectPath, "project.godot")))
            throw new DirectoryNotFoundException($"The production Godot Renderer project is missing: {projectPath}");
        return new GodotCompanionWindow(godotPath, projectPath, pluginRuntime, settings, actions, character, bundledCharacters, installedCharacters);
    }

}
