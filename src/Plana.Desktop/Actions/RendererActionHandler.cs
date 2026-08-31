using Plana.Core.Actions;

namespace Plana.Desktop.Actions;

internal interface IRendererBridge
{
    Task PlayAnimationAsync(string animation, CancellationToken cancellationToken);
}

internal sealed class RendererActionHandler(IRendererBridge renderer) : IActionHandler
{
    public string Kind => ActionKinds.PetAnimation;
    public IReadOnlySet<string> RequiredCapabilities { get; } = new HashSet<string>();

    public IReadOnlyList<string> Validate(ActionDefinition action) =>
        action.Parameters.TryGetValue("animation", out var animation) && !string.IsNullOrWhiteSpace(animation)
            ? []
            : ["Parameter 'animation' is required."];

    public async Task<ActionResult> ExecuteAsync(ActionDefinition action, ActionContext context, CancellationToken cancellationToken)
    {
        await renderer.PlayAnimationAsync(action.Parameters["animation"], cancellationToken);
        return ActionResult.Success("Played the animation.");
    }
}
