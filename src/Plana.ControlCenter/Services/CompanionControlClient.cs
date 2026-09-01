using System.IO.Pipes;
using System.Text.Json;
using Plana.Core.Companion;

namespace Plana_ControlCenter.Services;

internal static class CompanionControlClient
{
    public static async Task PerformAsync(CharacterPerformanceIntent intent, CancellationToken cancellationToken = default)
    {
        await using var pipe = new NamedPipeClientStream(".", "PlanaDesktop.Renderer", PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(1500, cancellationToken);
        await using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(pipe, leaveOpen: true);
        await writer.WriteLineAsync(JsonSerializer.Serialize(new
        {
            Emotion = intent.Emotion.ToString(),
            Gesture = intent.Gesture.ToString(),
            intent.IsSpeaking,
        }));
        var response = await reader.ReadLineAsync(cancellationToken);
        if (response?.Contains("\"ok\":true", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("The Companion rejected the performance request.");
    }
}
