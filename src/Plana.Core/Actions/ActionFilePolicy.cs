namespace Plana.Core.Actions;

public static class ActionFilePolicy
{
    private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".com", ".bat", ".cmd", ".ps1", ".js", ".jse", ".vbs", ".vbe",
        ".wsf", ".wsh", ".msi", ".msp", ".scr", ".cpl", ".reg", ".lnk",
    };

    public static bool CanOpenWithFileCapability(string path) =>
        !ExecutableExtensions.Contains(Path.GetExtension(path));
}
