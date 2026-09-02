using System.Diagnostics;
using Plana.Core.Actions;

namespace Plana.Desktop.Actions;

internal sealed class RunCommandActionHandler : IActionHandler
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    public string Kind => ActionKinds.RunCommand;
    public IReadOnlySet<string> RequiredCapabilities { get; } = new HashSet<string> { Capabilities.RunCommand };

    public IReadOnlyList<string> Validate(ActionDefinition action) =>
        action.Parameters.TryGetValue("executable", out var executable) && !string.IsNullOrWhiteSpace(executable)
            ? []
            : ["Parameter 'executable' is required. Raw shell command strings are not supported."];

    public async Task<ActionResult> ExecuteAsync(ActionDefinition action, ActionContext context, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(ParameterExpander.Expand(action.Parameters["executable"], context))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = context.WorkingDirectory ?? string.Empty,
        };
        foreach (var argument in ParameterExpander.ReadArguments(action, context))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("The command did not start.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token);
        var output = (await outputTask).Trim();
        var error = (await errorTask).Trim();
        return process.ExitCode == 0
            ? ActionResult.Success(string.IsNullOrWhiteSpace(output) ? "Command completed." : output)
            : ActionResult.Failure(string.IsNullOrWhiteSpace(error) ? $"Command exited with code {process.ExitCode}." : error);
    }
}
