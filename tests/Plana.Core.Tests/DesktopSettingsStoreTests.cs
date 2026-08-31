using Plana.Core.Settings;

#pragma warning disable xUnit1031 // This regression intentionally exercises a synchronous UI caller.

namespace Plana.Core.Tests;

public sealed class DesktopSettingsStoreTests
{
    [Fact]
    public async Task LoadAsync_NormalizesLegacyUserActionIdsInToolGroups()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"plana-settings-ids-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "settings.json");
        await File.WriteAllTextAsync(path, """
            {
              "UserActions": [{ "Id": "action-1", "Name": "Test", "Kind": "folder.open", "Parameters": {} }],
              "ToolGroups": [{ "Id": "group-1", "Name": "Test", "ActionIds": ["action-1"] }],
              "InteractionBindings": { "click": "action-1" }
            }
            """);
        try
        {
            var settings = await new DesktopSettingsStore(path).LoadAsync();
            Assert.Equal(["user.action.action-1"], settings.ToolGroups.Single().ActionIds);
            Assert.Equal("user.action.action-1", settings.InteractionBindings["click"]);
            Assert.Equal("group-1", settings.SelectedToolGroupId);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_DoesNotCaptureUiSynchronizationContext()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"plana-settings-context-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "settings.json");
        await File.WriteAllTextAsync(path, "{\"Scale\":1.25}");

        try
        {
            var load = Task.Run(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new NonPumpingSynchronizationContext());
                return new DesktopSettingsStore(path).LoadAsync().GetAwaiter().GetResult();
            });
            var completed = await Task.WhenAny(load, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.Same(load, completed);
            Assert.Equal(1.25, load.Result.Scale, precision: 3);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_RetriesWhileExistingSettingsFileIsTemporarilyLocked()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"plana-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "settings.json");
        await File.WriteAllTextAsync(path, "{}");

        try
        {
            await using var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var save = new DesktopSettingsStore(path).SaveAsync(new DesktopSettings { Scale = 1.4 });
            await Task.Delay(120);
            await locked.DisposeAsync();

            await save;
            var loaded = await new DesktopSettingsStore(path).LoadAsync();
            Assert.Equal(1.4, loaded.Scale, precision: 3);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class NonPumpingSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state)
        {
            // Deliberately do not run continuations posted back to this context.
        }
    }
}

#pragma warning restore xUnit1031
