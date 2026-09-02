using System.Diagnostics;
using System.IO;
using Plana.Core.Actions;

namespace Plana.Desktop.Actions;

internal sealed class RunScriptActionHandler : IActionHandler
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public string Kind => ActionKinds.RunScript;
    public IReadOnlySet<string> RequiredCapabilities { get; } = new HashSet<string> { Capabilities.RunScript };

    public IReadOnlyList<string> Validate(ActionDefinition action)
    {
        var errors = new List<string>();
        if (!action.Parameters.TryGetValue("interpreter", out var interpreter) || string.IsNullOrWhiteSpace(interpreter))
        {
            errors.Add("Parameter 'interpreter' is required.");
        }
        if (!action.Parameters.TryGetValue("script", out var script) || string.IsNullOrWhiteSpace(script))
        {
            errors.Add("Parameter 'script' is required.");
        }
        return errors;
    }

    public async Task<ActionResult> ExecuteAsync(ActionDefinition action, ActionContext context, CancellationToken cancellationToken)
    {
        var interpreter = ParameterExpander.Expand(action.Parameters["interpreter"], context);
        var expandedScript = ParameterExpander.Expand(action.Parameters["script"], context);
        var script = Path.GetFullPath(expandedScript, context.WorkingDirectory ?? Environment.CurrentDirectory);
        if (!File.Exists(script)) return ActionResult.Failure($"The script does not exist: {script}");

        var startInfo = new ProcessStartInfo(interpreter)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = context.WorkingDirectory ?? Path.GetDirectoryName(script)!,
        };
        startInfo.ArgumentList.Add(script);
        foreach (var argument in ParameterExpander.ReadArguments(action, context)) startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("The script did not start.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token);
        var output = (await outputTask).Trim();
        var error = (await errorTask).Trim();
        return process.ExitCode == 0
            ? ActionResult.Success(string.IsNullOrWhiteSpace(output) ? "Script completed." : output)
            : ActionResult.Failure(string.IsNullOrWhiteSpace(error) ? $"Script exited with code {process.ExitCode}." : error);
    }
}
