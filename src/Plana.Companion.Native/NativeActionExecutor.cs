using System.Diagnostics;
using Plana.Core.Actions;

namespace Plana.Companion.Native;

internal sealed record NativeActionEntry(
    string Id,
    string Name,
    ActionDefinition Definition,
    string? WorkingDirectory,
    string Description = "",
    string Source = "");

internal static class NativeActionExecutor
{
    public static ActionResult Execute(NativeActionEntry entry)
    {
        try
        {
            var action = entry.Definition;
            return action.Kind switch
            {
                ActionKinds.OpenUrl => OpenShell(action.Parameters["url"]),
                ActionKinds.OpenFile or ActionKinds.OpenFolder => OpenShell(Resolve(action.Parameters["path"], entry.WorkingDirectory)),
                ActionKinds.LaunchProcess => Launch(action, entry.WorkingDirectory),
                ActionKinds.RunCommand => RunCommand(action, entry.WorkingDirectory),
                ActionKinds.RunScript => RunScript(action, entry.WorkingDirectory),
                _ => ActionResult.Failure($"Unsupported Action kind: {action.Kind}"),
            };
        }
        catch (Exception exception)
        {
            return ActionResult.Failure(exception.Message);
        }
    }

    private static ActionResult OpenShell(string target)
    {
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        return ActionResult.Success("Opened.");
    }

    private static ActionResult Launch(ActionDefinition action, string? workingDirectory)
    {
        var info = ActionProcessStartInfoFactory.CreateForLaunch(action.Parameters["executable"], action, workingDirectory);
        Process.Start(info);
        return ActionResult.Success("Launched.");
    }

    private static ActionResult RunCommand(ActionDefinition action, string? workingDirectory)
    {
        var command = action.Parameters.TryGetValue("command", out var value) ? value : action.Parameters["executable"];
        var info = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory };
        info.ArgumentList.Add("/d"); info.ArgumentList.Add("/s"); info.ArgumentList.Add("/c"); info.ArgumentList.Add(command);
        Process.Start(info);
        return ActionResult.Success("Command started.");
    }

    private static ActionResult RunScript(ActionDefinition action, string? workingDirectory)
    {
        var info = ActionProcessStartInfoFactory.Create(action.Parameters["interpreter"], action, workingDirectory);
        info.ArgumentList.Insert(0, Resolve(action.Parameters["script"], workingDirectory));
        Process.Start(info);
        return ActionResult.Success("Script started.");
    }

    private static string Resolve(string path, string? workingDirectory) =>
        Path.IsPathFullyQualified(path) || workingDirectory is null ? path : Path.GetFullPath(path, workingDirectory);

}
