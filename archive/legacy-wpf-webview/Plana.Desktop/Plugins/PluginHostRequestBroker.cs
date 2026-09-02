using Plana.Core.Actions;
using Plana.Core.Plugins;

namespace Plana.Desktop.Plugins;

internal sealed class PluginHostRequestBroker(IEnumerable<IActionHandler> handlers)
{
    private readonly Dictionary<string, IActionHandler> handlers = handlers
        .ToDictionary(handler => handler.Kind, StringComparer.OrdinalIgnoreCase);

    public async Task<PluginHostResponsePayload> ExecuteAsync(
        PluginHostRequestPayload request,
        ActionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            if (PluginHostRequestPolicy.RequiredCapability(request.Kind) is not { } capability
                || !handlers.TryGetValue(request.Kind, out var handler))
            {
                return new PluginHostResponsePayload(false, $"Host request kind '{request.Kind}' is not brokered.");
            }
            var definition = new ActionDefinition(
                "plugin.host-request",
                "Plugin host request",
                request.Kind,
                request.Parameters ?? new Dictionary<string, string>(),
                new HashSet<string> { capability });
            var errors = handler.Validate(definition);
            if (errors.Count > 0)
            {
                return new PluginHostResponsePayload(false, string.Join(" ", errors));
            }
            var result = await handler.ExecuteAsync(definition, context, cancellationToken);
            return new PluginHostResponsePayload(result.Succeeded, result.Message);
        }
        catch (Exception exception)
        {
            return new PluginHostResponsePayload(false, exception.Message);
        }
    }
}
