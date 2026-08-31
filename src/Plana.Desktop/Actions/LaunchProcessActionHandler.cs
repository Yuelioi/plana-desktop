using System.Diagnostics;
using Plana.Core.Actions;

namespace Plana.Desktop.Actions;

internal sealed class LaunchProcessActionHandler : IActionHandler
{
    public string Kind => ActionKinds.LaunchProcess;
    public IReadOnlySet<string> RequiredCapabilities { get; } = new HashSet<string> { Capabilities.LaunchProcess };

    public IReadOnlyList<string> Validate(ActionDefinition action) =>
        action.Parameters.TryGetValue("executable", out var executable) && !string.IsNullOrWhiteSpace(executable)
            ? []
            : ["Parameter 'executable' is required."];

    public Task<ActionResult> ExecuteAsync(ActionDefinition action, ActionContext context, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(ParameterExpander.Expand(action.Parameters["executable"], context))
        {
            UseShellExecute = true,
            WorkingDirectory = context.WorkingDirectory ?? string.Empty,
        };
        foreach (var argument in ParameterExpander.ReadArguments(action, context))
        {
            startInfo.ArgumentList.Add(argument);
        }
        Process.Start(startInfo);
        return Task.FromResult(ActionResult.Success("Started the application."));
    }
}
