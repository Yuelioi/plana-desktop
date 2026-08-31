using Plana.Core.Actions;

namespace Plana.Core.Tests;

public sealed class ActionEngineTests
{
    [Fact]
    public async Task DispatchesAnAuthorizedActionThroughItsHandler()
    {
        var handler = new RecordingHandler("example", new HashSet<string> { "example.use" });
        var engine = new ActionEngine([handler], new AllowPolicy());
        var action = new ActionDefinition("pack.action", "Example", "example", new Dictionary<string, string>(), new HashSet<string> { "example.use" });
        engine.ReplacePacks([new ActionPack("pack", "Pack", "1.0.0", "Test", [action])]);

        var result = await engine.ExecuteAsync("pack.action");

        Assert.True(result.Succeeded);
        Assert.Equal("pack.action", handler.LastActionId);
    }

    [Fact]
    public void RejectsAnActionThatDoesNotDeclareItsRequiredCapability()
    {
        var engine = new ActionEngine([new RecordingHandler("example", new HashSet<string> { "example.use" })], new AllowPolicy());
        var action = new ActionDefinition("pack.action", "Example", "example", new Dictionary<string, string>(), new HashSet<string>());

        var exception = Assert.Throws<ActionPackException>(() =>
            engine.ReplacePacks([new ActionPack("pack", "Pack", "1.0.0", "Test", [action])]));

        Assert.Contains("example.use", exception.Message);
    }

    [Fact]
    public async Task DoesNotInvokeHandlerWhenCapabilityIsDenied()
    {
        var handler = new RecordingHandler("example", new HashSet<string> { "example.use" });
        var engine = new ActionEngine([handler], new DenyPolicy());
        var action = new ActionDefinition("pack.action", "Example", "example", new Dictionary<string, string>(), new HashSet<string> { "example.use" });
        engine.ReplacePacks([new ActionPack("pack", "Pack", "1.0.0", "Test", [action])]);

        var result = await engine.ExecuteAsync("pack.action");

        Assert.False(result.Succeeded);
        Assert.Null(handler.LastActionId);
    }

    [Fact]
    public async Task UsesPackDirectoryAsDefaultWorkingDirectory()
    {
        var handler = new RecordingHandler("example", new HashSet<string>());
        var engine = new ActionEngine([handler], new AllowPolicy());
        var action = new ActionDefinition("pack.action", "Example", "example", new Dictionary<string, string>(), new HashSet<string>());
        engine.ReplacePacks([new ActionPack("pack", "Pack", "1.0.0", "Test", [action], SourceDirectory: "C:\\packs\\example")]);

        await engine.ExecuteAsync("pack.action");

        Assert.Equal("C:\\packs\\example", handler.LastWorkingDirectory);
    }

    [Fact]
    public async Task AuthorizesEveryCapabilityDeclaredByDynamicAction()
    {
        var policy = new RecordingPolicy();
        var engine = new ActionEngine([new RecordingHandler("dynamic", new HashSet<string>())], policy);
        var action = new ActionDefinition(
            "plugin.action", "Plugin Action", "dynamic", new Dictionary<string, string>(),
            new HashSet<string> { "folder.open", "url.open" });
        engine.ReplacePacks([new ActionPack("plugin", "Plugin", "1", "Tests", [action])]);

        await engine.ExecuteAsync(action.Id);

        Assert.Equal(action.Capabilities.Order(), policy.LastCapabilities!.Order());
    }

    private sealed class RecordingHandler(string kind, IReadOnlySet<string> capabilities) : IActionHandler
    {
        public string Kind => kind;
        public IReadOnlySet<string> RequiredCapabilities => capabilities;
        public string? LastActionId { get; private set; }
        public string? LastWorkingDirectory { get; private set; }
        public IReadOnlyList<string> Validate(ActionDefinition action) => [];
        public Task<ActionResult> ExecuteAsync(ActionDefinition action, ActionContext context, CancellationToken cancellationToken)
        {
            LastActionId = action.Id;
            LastWorkingDirectory = context.WorkingDirectory;
            return Task.FromResult(ActionResult.Success());
        }
    }

    private sealed class AllowPolicy : ICapabilityPolicy
    {
        public Task<bool> AuthorizeAsync(ActionPack pack, ActionDefinition action, IReadOnlySet<string> capabilities, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class DenyPolicy : ICapabilityPolicy
    {
        public Task<bool> AuthorizeAsync(ActionPack pack, ActionDefinition action, IReadOnlySet<string> capabilities, CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class RecordingPolicy : ICapabilityPolicy
    {
        public IReadOnlySet<string>? LastCapabilities { get; private set; }
        public Task<bool> AuthorizeAsync(ActionPack pack, ActionDefinition action, IReadOnlySet<string> capabilities, CancellationToken cancellationToken)
        {
            LastCapabilities = capabilities;
            return Task.FromResult(true);
        }
    }
}
