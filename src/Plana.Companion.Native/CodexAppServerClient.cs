using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Plana.Companion.Native;

internal sealed class CodexAppServerClient : IDisposable
{
    private readonly string _baseInstructions;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Process? _process;
    private StreamWriter? _input;
    private StreamReader? _output;
    private string? _threadId;
    private string? _model;
    private int _requestId;
    private bool _disposed;

    public CodexAppServerClient(string baseInstructions)
    {
        _baseInstructions = baseInstructions;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Dispose();
    }

    public async Task<string> SendAsync(string? model, string prompt, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureStartedAsync(model, cancellationToken);
            var responseId = NextId();
            await SendAsync(new
            {
                jsonrpc = "2.0",
                id = responseId,
                method = "turn/start",
                @params = new
                {
                    threadId = _threadId,
                    effort = "none",
                    input = new[] { new { type = "text", text = prompt } },
                },
            }, cancellationToken);
            await ReadResponseAsync(responseId, cancellationToken);

            string? answer = null;
            while (await ReadMessageAsync(cancellationToken) is { } message)
            {
                if (!message.TryGetProperty("method", out var method)) continue;
                var methodName = method.GetString();
                if (methodName == "item/completed" &&
                    message.TryGetProperty("params", out var parameters) && parameters.TryGetProperty("item", out var item) &&
                    item.TryGetProperty("type", out var type) && type.GetString() == "agentMessage" &&
                    item.TryGetProperty("text", out var text))
                {
                    answer = text.GetString();
                }
                if (methodName == "turn/completed")
                    return string.IsNullOrWhiteSpace(answer) ? "Codex completed without a response." : answer.Trim();
            }
            throw new EndOfStreamException("Codex app-server closed before the turn completed.");
        }
        catch
        {
            Stop();
            throw;
        }
        finally { _gate.Release(); }
    }

    public async Task WarmUpAsync(string? model, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try { await EnsureStartedAsync(model, cancellationToken); }
        catch { Stop(); throw; }
        finally { _gate.Release(); }
    }

    private async Task EnsureStartedAsync(string? model, CancellationToken cancellationToken)
    {
        model = string.IsNullOrWhiteSpace(model) ? null : model.Trim();
        if (_process is { HasExited: false } && _threadId is not null && string.Equals(_model, model, StringComparison.OrdinalIgnoreCase)) return;
        Stop();

        var npmCodex = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "node_modules", "@openai", "codex", "bin", "codex.js");
        var info = new ProcessStartInfo(File.Exists(npmCodex) ? "node.exe" : "codex.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
        };
        if (File.Exists(npmCodex)) info.ArgumentList.Add(npmCodex);
        info.ArgumentList.Add("app-server");
        info.ArgumentList.Add("--stdio");
        _process = Process.Start(info) ?? throw new InvalidOperationException("Codex app-server could not be started.");
        _input = _process.StandardInput;
        _output = _process.StandardOutput;
        _ = _process.StandardError.ReadToEndAsync();

        var initializeId = NextId();
        await SendAsync(new { jsonrpc = "2.0", id = initializeId, method = "initialize", @params = new { clientInfo = new { name = "plana-desktop", version = "1.0" } } }, cancellationToken);
        await ReadResponseAsync(initializeId, cancellationToken);
        await SendAsync(new { jsonrpc = "2.0", method = "initialized", @params = new { } }, cancellationToken);

        var threadId = NextId();
        await SendAsync(new
        {
            jsonrpc = "2.0",
            id = threadId,
            method = "thread/start",
            @params = new
            {
                ephemeral = true,
                sandbox = "read-only",
                approvalPolicy = "never",
                baseInstructions = _baseInstructions,
                cwd = AppContext.BaseDirectory,
                model,
                config = new { model_reasoning_effort = "none" },
            },
        }, cancellationToken);
        var response = await ReadResponseAsync(threadId, cancellationToken);
        _threadId = response.GetProperty("result").GetProperty("thread").GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Codex app-server did not return a thread ID.");
        _model = model;
    }

    private int NextId() => Interlocked.Increment(ref _requestId);

    private async Task SendAsync<T>(T message, CancellationToken cancellationToken)
    {
        if (_input is null) throw new InvalidOperationException("Codex app-server is not running.");
        await _input.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(), cancellationToken);
        await _input.FlushAsync(cancellationToken);
    }

    private async Task<JsonElement> ReadResponseAsync(int id, CancellationToken cancellationToken)
    {
        while (await ReadMessageAsync(cancellationToken) is { } message)
        {
            if (!message.TryGetProperty("id", out var responseId) || responseId.ValueKind != JsonValueKind.Number || responseId.GetInt32() != id) continue;
            if (message.TryGetProperty("error", out var error)) throw new InvalidOperationException($"Codex app-server error: {error}");
            return message;
        }
        throw new EndOfStreamException("Codex app-server closed before responding.");
    }

    private async Task<JsonElement?> ReadMessageAsync(CancellationToken cancellationToken)
    {
        if (_output is null) return null;
        while (await _output.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using var document = JsonDocument.Parse(line);
                return document.RootElement.Clone();
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"Codex app-server returned an invalid frame: {line}", exception);
            }
        }
        return null;
    }

    private void Stop()
    {
        _threadId = null;
        _model = null;
        _input?.Dispose();
        _input = null;
        _output?.Dispose();
        _output = null;
        if (_process is not null)
        {
            try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            _process.Dispose();
            _process = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _gate.Dispose();
    }
}
