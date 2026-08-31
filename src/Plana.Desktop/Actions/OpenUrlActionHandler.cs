using System.Diagnostics;
using Plana.Core.Actions;

namespace Plana.Desktop.Actions;

internal sealed class OpenUrlActionHandler : IActionHandler
{
    public string Kind => ActionKinds.OpenUrl;
    public IReadOnlySet<string> RequiredCapabilities { get; } = new HashSet<string> { Capabilities.OpenUrl };

    public IReadOnlyList<string> Validate(ActionDefinition action)
    {
        return action.Parameters.TryGetValue("url", out var url)
            && Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? []
                : ["Parameter 'url' must be an absolute HTTP or HTTPS URL."];
    }

    public Task<ActionResult> ExecuteAsync(ActionDefinition action, ActionContext context, CancellationToken cancellationToken)
    {
        var url = ParameterExpander.Expand(action.Parameters["url"], context);
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        return Task.FromResult(ActionResult.Success("Opened the URL."));
    }
}
