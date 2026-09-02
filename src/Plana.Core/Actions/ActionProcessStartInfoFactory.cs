using System.Diagnostics;

namespace Plana.Core.Actions;

public static class ActionProcessStartInfoFactory
{
    public static ProcessStartInfo CreateForLaunch(string executable, ActionDefinition action, string? workingDirectory = null) =>
        Create(executable, action, workingDirectory, useExecutableDirectoryAsDefault: true);

    public static ProcessStartInfo Create(string executable, ActionDefinition action, string? workingDirectory = null) =>
        Create(executable, action, workingDirectory, useExecutableDirectoryAsDefault: false);

    private static ProcessStartInfo Create(string executable, ActionDefinition action, string? workingDirectory, bool useExecutableDirectoryAsDefault)
    {
        var resolvedWorkingDirectory = workingDirectory;
        if (useExecutableDirectoryAsDefault && string.IsNullOrWhiteSpace(resolvedWorkingDirectory) && Path.IsPathFullyQualified(executable))
        {
            resolvedWorkingDirectory = Path.GetDirectoryName(executable);
        }

        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            WorkingDirectory = string.IsNullOrWhiteSpace(resolvedWorkingDirectory)
                ? Environment.CurrentDirectory
                : resolvedWorkingDirectory,
        };
        foreach (var argument in action.Parameters
                     .Where(pair => pair.Key.StartsWith("arg.", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(pair => ParseArgumentIndex(pair.Key)))
        {
            startInfo.ArgumentList.Add(argument.Value);
        }
        return startInfo;
    }

    private static int ParseArgumentIndex(string key) =>
        int.TryParse(key.AsSpan(4), out var index) ? index : int.MaxValue;
}
