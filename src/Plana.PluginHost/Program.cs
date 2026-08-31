using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using Plana.Core.Plugins;

var options = ParseArguments(args);
if (!options.TryGetValue("pipe", out var pipeName) || !options.TryGetValue("manifest", out var manifestPath))
{
    Console.Error.WriteLine("Usage: Plana.PluginHost --pipe <name> --manifest <plugin.json>");
    return 2;
}

var diagnostic = await new PluginManifestLoader().LoadAsync(manifestPath);
if (!diagnostic.IsValid)
{
    Console.Error.WriteLine(diagnostic.Error);
    return 3;
}

var manifest = diagnostic.Manifest!;
var entryPoint = PluginManifestLoader.ResolveEntryPoint(manifest);
if (!File.Exists(entryPoint))
{
    Console.Error.WriteLine($"Plugin entry point does not exist: {entryPoint}");
    return 4;
}

await using var desktopPipe = new NamedPipeServerStream(
    pipeName,
    PipeDirection.InOut,
    1,
    PipeTransmissionMode.Byte,
    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
using var connectionTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
await desktopPipe.WaitForConnectionAsync(connectionTimeout.Token);

var startInfo = new ProcessStartInfo(entryPoint)
{
    UseShellExecute = false,
    RedirectStandardInput = true,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    CreateNoWindow = true,
    WorkingDirectory = manifest.PackageDirectory,
};
startInfo.ArgumentList.Add("--plana-protocol");
startInfo.ArgumentList.Add(PluginProtocol.Version.ToString());

using var plugin = Process.Start(startInfo) ?? throw new InvalidOperationException("The plugin process did not start.");
using var lifetime = new CancellationTokenSource();
var pipeReader = new StreamReader(desktopPipe, leaveOpen: true);
var pipeWriter = new StreamWriter(desktopPipe, leaveOpen: true) { AutoFlush = true };

var desktopToPlugin = RelayAsync(pipeReader, plugin.StandardInput, lifetime.Token);
var pluginToDesktop = RelayAsync(plugin.StandardOutput, pipeWriter, lifetime.Token);
var pluginExit = plugin.WaitForExitAsync(lifetime.Token);
await Task.WhenAny(desktopToPlugin, pluginToDesktop, pluginExit);
lifetime.Cancel();
if (!plugin.HasExited) plugin.Kill(entireProcessTree: true);
try { await Task.WhenAll(desktopToPlugin, pluginToDesktop, pluginExit); } catch (OperationCanceledException) { }
return plugin.HasExited ? plugin.ExitCode : 5;

static async Task RelayAsync(TextReader reader, TextWriter writer, CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        var line = await reader.ReadLineAsync(cancellationToken);
        if (line is null) return;
        if (line.Length > PluginProtocol.MaximumMessageCharacters) throw new InvalidDataException("Plugin protocol message is too large.");
        var envelope = JsonSerializer.Deserialize<PluginEnvelope>(line, PluginProtocol.JsonOptions)
            ?? throw new InvalidDataException("Plugin protocol message is empty.");
        if (envelope.ProtocolVersion != PluginProtocol.Version || string.IsNullOrWhiteSpace(envelope.RequestId) || string.IsNullOrWhiteSpace(envelope.Type))
        {
            throw new InvalidDataException("Plugin protocol message has an invalid version, requestId, or type.");
        }
        await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        await writer.FlushAsync(cancellationToken);
    }
}

static Dictionary<string, string> ParseArguments(string[] arguments)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var index = 0; index + 1 < arguments.Length; index += 2)
    {
        if (!arguments[index].StartsWith("--", StringComparison.Ordinal)) continue;
        result[arguments[index][2..]] = arguments[index + 1];
    }
    return result;
}
