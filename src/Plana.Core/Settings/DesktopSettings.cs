namespace Plana.Core.Settings;

public sealed class DesktopSettings
{
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double Width { get; set; } = 340;
    public double Height { get; set; } = 520;
    public bool AlwaysOnTop { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public string UiCulture { get; set; } = "en";
    public double Scale { get; set; } = 1;
    public Dictionary<string, string> InteractionBindings { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["click"] = "builtin.companion.interact",
    };
    public Dictionary<string, HashSet<string>> CapabilityGrants { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> DisabledActionPacks { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ProjectLauncherSettings> ProjectLaunchers { get; set; } = [];
    public List<UserActionSettings> UserActions { get; set; } = [];
    public HashSet<string> DisabledPlugins { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ToolGroupSettings> ToolGroups { get; set; } = [];
    public string? SelectedToolGroupId { get; set; }
    public List<string> PinnedCompanionActionIds { get; set; } = [];
    public string AiProvider { get; set; } = "codex";
    public string AiModel { get; set; } = string.Empty;
    public string AiApiBaseUrl { get; set; } = "https://api.openai.com/v1";
    public string AiApiKeyEnvironmentVariable { get; set; } = "OPENAI_API_KEY";
}

public sealed class ProjectLauncherSettings
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Folder { get; set; } = string.Empty;
    public string Executable { get; set; } = "wt.exe";
    public List<string> Arguments { get; set; } = ["-d", "{folder}"];
}

public sealed class UserActionSettings
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool RequiresConfirmation { get; set; }
}

public sealed class ToolGroupSettings
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public List<string> ActionIds { get; set; } = [];
}
