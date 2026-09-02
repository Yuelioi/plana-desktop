using System.IO.Pipes;
using System.Text.Json;
using Plana.Core.Actions;

namespace Plana_ControlCenter.Services;

internal static class HostControlClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<IReadOnlyList<HostActionItem>> GetPluginActionsAsync()
    {
        var response = await SendAsync(new { type = "catalog" });
        if (!response.GetProperty("ok").GetBoolean()) return [];
        return response.GetProperty("actions").EnumerateArray()
            .Where(item => item.GetProperty("kind").GetString() == ActionKinds.PluginInvoke)
            .Select(item => new HostActionItem(
                item.GetProperty("id").GetString()!, item.GetProperty("name").GetString()!,
                item.GetProperty("description").GetString() ?? string.Empty, item.GetProperty("source").GetString() ?? "Plugin"))
            .ToArray();
    }

    public static async Task<ActionResult> ExecuteAsync(string actionId)
    {
        var response = await SendAsync(new { type = "execute-action", actionId });
        var message = response.TryGetProperty("message", out var value) ? value.GetString() ?? string.Empty : string.Empty;
        return response.GetProperty("ok").GetBoolean() ? ActionResult.Success(message) : ActionResult.Failure(message);
    }

    private static async Task<JsonElement> SendAsync(object request)
    {
        await using var pipe = new NamedPipeClientStream(".", "PlanaDesktop.Renderer", PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(1000);
        await using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(pipe, leaveOpen: true);
        await writer.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions));
        var line = await reader.ReadLineAsync() ?? throw new IOException("Companion Host closed the control connection.");
        using var document = JsonDocument.Parse(line);
        return document.RootElement.Clone();
    }
}

internal sealed record HostActionItem(string Id, string Name, string Description, string Source);
