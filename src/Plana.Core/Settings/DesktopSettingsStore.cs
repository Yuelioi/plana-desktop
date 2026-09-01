using System.Text.Json;

namespace Plana.Core.Settings;

public sealed class DesktopSettingsStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<DesktopSettings> LoadAsync()
    {
        if (!File.Exists(path)) return new DesktopSettings();
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        var settings = await JsonSerializer.DeserializeAsync<DesktopSettings>(stream).ConfigureAwait(false) ?? new DesktopSettings();
        if (string.IsNullOrWhiteSpace(settings.SelectedCharacterPackId)) settings.SelectedCharacterPackId = "builtin.plana";
        NormalizeActionIds(settings);
        return settings;
    }

    public async Task SaveAsync(DesktopSettings settings)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $"settings-{Guid.NewGuid():N}.tmp");
        await using (var stream = File.Create(temporary))
        {
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
        }
        try
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    File.Move(temporary, path, true);
                    break;
                }
                catch (Exception exception) when (attempt < 7 && exception is IOException or UnauthorizedAccessException)
                {
                    await Task.Delay(50 * (attempt + 1)).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static void NormalizeActionIds(DesktopSettings settings)
    {
        var userActions = settings.UserActions.ToDictionary(action => action.Id, action => $"user.action.{action.Id}", StringComparer.OrdinalIgnoreCase);
        var launchers = settings.ProjectLaunchers.ToDictionary(launcher => launcher.Id, launcher => $"user.launcher.{launcher.Id}", StringComparer.OrdinalIgnoreCase);

        string Normalize(string id) => userActions.GetValueOrDefault(id) ?? launchers.GetValueOrDefault(id) ?? id;
        foreach (var group in settings.ToolGroups)
            group.ActionIds = group.ActionIds.Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        settings.PinnedCompanionActionIds = settings.PinnedCompanionActionIds.Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToList();
        if (settings.SelectedToolGroupId is null && settings.ToolGroups.Count > 0)
            settings.SelectedToolGroupId = settings.ToolGroups[0].Id;
        else if (settings.SelectedToolGroupId is not null && settings.ToolGroups.All(group => !group.Id.Equals(settings.SelectedToolGroupId, StringComparison.OrdinalIgnoreCase)))
            settings.SelectedToolGroupId = settings.ToolGroups.FirstOrDefault()?.Id;
        foreach (var interaction in settings.InteractionBindings.Keys.ToArray())
            settings.InteractionBindings[interaction] = Normalize(settings.InteractionBindings[interaction]);
    }
}
