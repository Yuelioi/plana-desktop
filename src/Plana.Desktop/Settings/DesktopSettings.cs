namespace Plana.Desktop.Settings;

internal sealed class DesktopSettings
{
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double Width { get; set; } = 340;
    public double Height { get; set; } = 520;
    public bool AlwaysOnTop { get; set; } = true;
    public Dictionary<string, string> InteractionBindings { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["click"] = "builtin.companion.interact",
    };
    public Dictionary<string, HashSet<string>> CapabilityGrants { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
