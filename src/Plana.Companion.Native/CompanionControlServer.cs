using System.IO.Pipes;
using System.Text.Json;
using Plana.Core.Companion;

namespace Plana.Companion.Native;

internal sealed class CompanionControlServer : IDisposable
{
    private readonly GodotCompanionWindow _companion;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;

    public CompanionControlServer(GodotCompanionWindow companion)
    {
        _companion = companion;
        _loop = Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                "PlanaDesktop.Renderer", PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            try
            {
                await pipe.WaitForConnectionAsync(_stop.Token);
                using var reader = new StreamReader(pipe, leaveOpen: true);
                await using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
                var line = await reader.ReadLineAsync(_stop.Token);
                var request = line is null ? null : JsonSerializer.Deserialize<PerformanceRequest>(line);
                if (request is null || !Enum.TryParse<CharacterEmotion>(request.Emotion, true, out var emotion) ||
                    !Enum.TryParse<CharacterGesture>(request.Gesture, true, out var gesture))
                {
                    await writer.WriteLineAsync("{\"ok\":false}");
                    continue;
                }
                _companion.Perform(new CharacterPerformanceIntent(emotion, gesture, request.IsSpeaking));
                await writer.WriteLineAsync("{\"ok\":true}");
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
            catch (IOException) when (!_stop.IsCancellationRequested) { }
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        try { _loop.Wait(TimeSpan.FromSeconds(2)); } catch (AggregateException) { }
        _stop.Dispose();
    }

    private sealed record PerformanceRequest(string Emotion, string Gesture, bool IsSpeaking);
}
