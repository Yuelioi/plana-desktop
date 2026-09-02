using Plana.Core.Actions;
using Plana.Core.Plugins;
using Plana.Desktop.Plugins;

namespace Plana.Desktop.Actions;

internal sealed class PluginActionHandler(
    PluginRuntimeManager runtime,
    PluginHostRequestBroker broker) : IActionHandler
{
    public string Kind => ActionKinds.PluginInvoke;
    public IReadOnlySet<string> RequiredCapabilities { get; } = new HashSet<string>();

    public IReadOnlyList<string> Validate(ActionDefinition action)
    {
        var errors = new List<string>();
        if (!action.Parameters.TryGetValue("pluginId", out var pluginId) || string.IsNullOrWhiteSpace(pluginId))
        {
            errors.Add("Parameter 'pluginId' is required.");
        }
        if (!action.Parameters.TryGetValue("actionId", out var actionId) || string.IsNullOrWhiteSpace(actionId))
        {
            errors.Add("Parameter 'actionId' is required.");
        }
        return errors;
    }

    public Task<ActionResult> ExecuteAsync(
        ActionDefinition action,
        ActionContext context,
        CancellationToken cancellationToken) =>
        runtime.InvokeAsync(
            action.Parameters["pluginId"],
            action.Parameters["actionId"],
            action.Capabilities,
            (request, token) => broker.ExecuteAsync(request, context, token),
            cancellationToken);
}
