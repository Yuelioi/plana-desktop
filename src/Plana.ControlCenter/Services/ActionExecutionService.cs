using System.Diagnostics;
using Plana.Core.Actions;

namespace Plana_ControlCenter.Services;

internal static class ActionExecutionService
{
    public static Task<ActionResult> ExecuteAsync(ActionDefinition action, string? workingDirectory = null)
    {
        try
        {
            return Task.FromResult(action.Kind switch
            {
                ActionKinds.OpenUrl => OpenShell(action, "url", workingDirectory),
                ActionKinds.OpenFile or ActionKinds.OpenFolder => OpenShell(action, "path", workingDirectory),
                ActionKinds.LaunchProcess => Launch(action, workingDirectory),
                ActionKinds.RunCommand => RunCommand(action, workingDirectory),
                ActionKinds.RunScript => RunScript(action, workingDirectory),
                _ => ActionResult.Failure($"Unsupported Action kind: {action.Kind}"),
            });
        }
        catch (Exception exception)
        {
            return Task.FromResult(ActionResult.Failure(exception.Message));
        }
    }

    private static ActionResult OpenShell(ActionDefinition action, string key, string? workingDirectory)
    {
        var target = Required(action, key);
        if (key == "path" && !Path.IsPathFullyQualified(target) && workingDirectory is not null)
        {
            target = Path.GetFullPath(target, workingDirectory);
        }
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        return ActionResult.Success(App.IsChinese ? "已打开。" : "Opened.");
    }

    private static ActionResult Launch(ActionDefinition action, string? workingDirectory)
    {
        var startInfo = ActionProcessStartInfoFactory.CreateForLaunch(Required(action, "executable"), action, workingDirectory);
        Process.Start(startInfo);
        return ActionResult.Success(App.IsChinese ? "已启动。" : "Launched.");
    }

    private static ActionResult RunCommand(ActionDefinition action, string? workingDirectory)
    {
        var command = action.Parameters.TryGetValue("command", out var configured)
            ? configured
            : Required(action, "executable");
        var startInfo = new ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
        };
        startInfo.ArgumentList.Add("/d");
        startInfo.ArgumentList.Add("/s");
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add(command);
        Process.Start(startInfo);
        return ActionResult.Success(App.IsChinese ? "命令已启动。" : "Command started.");
    }

    private static ActionResult RunScript(ActionDefinition action, string? workingDirectory)
    {
        var startInfo = ActionProcessStartInfoFactory.Create(Required(action, "interpreter"), action, workingDirectory);
        startInfo.ArgumentList.Insert(0, Required(action, "script"));
        Process.Start(startInfo);
        return ActionResult.Success(App.IsChinese ? "脚本已启动。" : "Script started.");
    }

    private static string Required(ActionDefinition action, string key) =>
        action.Parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Action parameter '{key}' is required.");
}
