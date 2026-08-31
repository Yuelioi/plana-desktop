namespace Plana.Core.Actions;

public interface IActionHandler
{
    string Kind { get; }
    IReadOnlySet<string> RequiredCapabilities { get; }
    IReadOnlyList<string> Validate(ActionDefinition action);
    Task<ActionResult> ExecuteAsync(ActionDefinition action, ActionContext context, CancellationToken cancellationToken);
}

public interface ICapabilityPolicy
{
    Task<bool> AuthorizeAsync(
        ActionPack pack,
        ActionDefinition action,
        IReadOnlySet<string> capabilities,
        CancellationToken cancellationToken);
}
