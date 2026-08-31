namespace Plana.Core.Actions;

public sealed class ActionEngine
{
    private readonly Dictionary<string, IActionHandler> handlers;
    private readonly ICapabilityPolicy capabilityPolicy;
    private readonly Dictionary<string, (ActionPack Pack, ActionDefinition Action)> catalog = [];

    public ActionEngine(IEnumerable<IActionHandler> handlers, ICapabilityPolicy capabilityPolicy)
    {
        this.handlers = handlers.ToDictionary(handler => handler.Kind, StringComparer.OrdinalIgnoreCase);
        this.capabilityPolicy = capabilityPolicy;
    }

    public IReadOnlyList<ActionDescriptor> ListActions() => catalog.Values
        .Select(entry => new ActionDescriptor(entry.Pack.Id, entry.Pack.Name, entry.Action))
        .OrderBy(item => item.PackName, StringComparer.CurrentCultureIgnoreCase)
        .ThenBy(item => item.Action.Label, StringComparer.CurrentCultureIgnoreCase)
        .ToArray();

    public void ReplacePacks(IEnumerable<ActionPack> packs)
    {
        var next = new Dictionary<string, (ActionPack, ActionDefinition)>(StringComparer.OrdinalIgnoreCase);
        foreach (var pack in packs)
        {
            ValidatePack(pack);
            foreach (var action in pack.Actions)
            {
                if (!next.TryAdd(action.Id, (pack, action)))
                {
                    throw new ActionPackException($"Action id '{action.Id}' is declared more than once.");
                }
            }
        }

        catalog.Clear();
        foreach (var entry in next)
        {
            catalog.Add(entry.Key, entry.Value);
        }
    }

    public async Task<ActionResult> ExecuteAsync(
        string actionId,
        ActionContext? context = null,
        CancellationToken cancellationToken = default)
    {
        if (!catalog.TryGetValue(actionId, out var entry))
        {
            return ActionResult.Failure($"Action '{actionId}' was not found.");
        }

        var handler = handlers[entry.Action.Kind];
        var authorized = entry.Pack.BuiltIn || await capabilityPolicy.AuthorizeAsync(
            entry.Pack,
            entry.Action,
            entry.Action.Capabilities,
            cancellationToken);
        if (!authorized)
        {
            return ActionResult.Failure("The Action Pack is not authorized for this capability.");
        }

        try
        {
            var requestedContext = context ?? new ActionContext();
            var effectiveContext = new ActionContext(
                requestedContext.WorkingDirectory ?? entry.Pack.SourceDirectory,
                requestedContext.Variables);
            return await handler.ExecuteAsync(entry.Action, effectiveContext, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ActionResult.Failure("Action cancelled.");
        }
        catch (Exception exception)
        {
            return ActionResult.Failure(exception.Message);
        }
    }

    private void ValidatePack(ActionPack pack)
    {
        if (string.IsNullOrWhiteSpace(pack.Id) || string.IsNullOrWhiteSpace(pack.Name))
        {
            throw new ActionPackException("Action Pack id and name are required.");
        }

        foreach (var action in pack.Actions)
        {
            if (string.IsNullOrWhiteSpace(action.Id) || string.IsNullOrWhiteSpace(action.Label))
            {
                throw new ActionPackException($"Action Pack '{pack.Id}' contains an action without an id or label.");
            }
            if (!handlers.TryGetValue(action.Kind, out var handler))
            {
                throw new ActionPackException($"Action '{action.Id}' uses unsupported kind '{action.Kind}'.");
            }
            if (!handler.RequiredCapabilities.IsSubsetOf(action.Capabilities))
            {
                var missing = handler.RequiredCapabilities.Except(action.Capabilities);
                throw new ActionPackException($"Action '{action.Id}' does not declare capabilities: {string.Join(", ", missing)}.");
            }
            var errors = handler.Validate(action);
            if (errors.Count > 0)
            {
                throw new ActionPackException($"Action '{action.Id}' is invalid: {string.Join(" ", errors)}");
            }
        }
    }
}

public sealed class ActionPackException(string message) : Exception(message);
