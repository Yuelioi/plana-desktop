using System.Diagnostics;
using System.IO;
using Plana.Core.Actions;

namespace Plana.Desktop.Actions;

internal sealed class OpenPathActionHandler(bool folder) : IActionHandler
{
    public string Kind => folder ? ActionKinds.OpenFolder : ActionKinds.OpenFile;
    public IReadOnlySet<string> RequiredCapabilities { get; } = new HashSet<string>
    {
        folder ? Capabilities.OpenFolder : Capabilities.OpenFile,
    };

    public IReadOnlyList<string> Validate(ActionDefinition action) =>
        action.Parameters.TryGetValue("path", out var path) && !string.IsNullOrWhiteSpace(path)
            ? []
            : ["Parameter 'path' is required."];

    public Task<ActionResult> ExecuteAsync(ActionDefinition action, ActionContext context, CancellationToken cancellationToken)
    {
        var expanded = ParameterExpander.Expand(action.Parameters["path"], context);
        var path = Path.GetFullPath(expanded, context.WorkingDirectory ?? Environment.CurrentDirectory);
        if (!folder && !ActionFilePolicy.CanOpenWithFileCapability(path))
        {
            return Task.FromResult(ActionResult.Failure(
                "Executable and script files cannot use file.open. Use process.launch or script.run with the corresponding capability."));
        }
        var exists = folder ? Directory.Exists(path) : File.Exists(path);
        if (!exists)
        {
            return Task.FromResult(ActionResult.Failure($"The {(folder ? "folder" : "file")} does not exist: {path}"));
        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        return Task.FromResult(ActionResult.Success(folder ? "Opened the folder." : "Opened the file."));
    }
}
