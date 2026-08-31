using System.Text.Json;
using System.IO;

namespace Plana.Desktop.Settings;

internal sealed class DesktopSettingsStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<DesktopSettings> LoadAsync()
    {
        if (!File.Exists(path)) return new DesktopSettings();
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<DesktopSettings>(stream) ?? new DesktopSettings();
    }

    public async Task SaveAsync(DesktopSettings settings)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $"settings-{Guid.NewGuid():N}.tmp");
        await using (var stream = File.Create(temporary))
        {
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
            await stream.FlushAsync();
        }
        File.Move(temporary, path, true);
    }
}
