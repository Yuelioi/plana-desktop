using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using Plana.Core.Plugins;
using Plana.Core.Actions;
using Plana.Core.Settings;

namespace Plana.Core.Plugins;

public sealed record PluginRuntimeInfo(string PluginId, string Status, string? Message = null);

public sealed class PluginRuntimeManager(string pluginHostPath) : IAsyncDisposable
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(5);
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, RunningPlugin> running = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PluginRuntimeInfo> statuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ActionPack> contributionPacks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PluginContributionsPayload> contributionSets = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler? StateChanged;

    public IReadOnlyDictionary<string, PluginRuntimeInfo> Snapshot()
    {
        lock (statuses) return new Dictionary<string, PluginRuntimeInfo>(statuses, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<ActionPack> SnapshotActionPacks()
    {
        lock (contributionPacks) return contributionPacks.Values.ToArray();
    }

    public IReadOnlyDictionary<string, PluginContributionsPayload> SnapshotContributions()
    {
        lock (contributionSets) return new Dictionary<string, PluginContributionsPayload>(contributionSets, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<ActionResult> InvokeAsync(
        string pluginId,
        string actionId,
        IReadOnlySet<string> approvedActionCapabilities,
        Func<PluginHostRequestPayload, CancellationToken, Task<PluginHostResponsePayload>> handleHostRequest,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        RunningPlugin? session = null;
        try
        {
            if (!running.TryGetValue(pluginId, out session))
            {
                return ActionResult.Failure("The Plugin is not running.");
            }
            var result = await session.Protocol.InvokeAsync(
                actionId,
                approvedActionCapabilities,
                handleHostRequest,
                TimeSpan.FromSeconds(45),
                cancellationToken);
            return result.Succeeded ? ActionResult.Success(result.Message) : ActionResult.Failure(result.Message);
        }
        catch (Exception exception)
        {
            if (session is not null) await FailAsync(pluginId, session, exception.Message);
            return ActionResult.Failure(exception.Message);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task FailAsync(string pluginId, RunningPlugin session, string message)
    {
        running.Remove(pluginId);
        lock (contributionPacks) contributionPacks.Remove(pluginId);
        lock (contributionSets) contributionSets.Remove(pluginId);
        await session.Protocol.DisposeAsync();
        session.Pipe.Dispose();
        if (!session.Host.HasExited) session.Host.Kill(entireProcessTree: true);
        session.Host.Dispose();
        SetStatus(new PluginRuntimeInfo(pluginId, "Failed", message));
    }

    public async Task ReconcileAsync(
        IReadOnlyList<PluginDiagnostic> plugins,
        DesktopSettings settings,
        string culture,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var desired = plugins
                .Where(plugin => plugin.IsValid
                    && plugin.Manifest is { } manifest
                    && !settings.DisabledPlugins.Contains(manifest.Id))
                .ToDictionary(plugin => plugin.Manifest!.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var pluginId in running.Keys.Where(id => !desired.ContainsKey(id)).ToArray())
            {
                await StopAsync(pluginId);
            }
            foreach (var plugin in desired.Values.Where(plugin => !running.ContainsKey(plugin.Manifest!.Id)))
            {
                await StartAsync(plugin, culture, cancellationToken);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        try
        {
            foreach (var pluginId in running.Keys.ToArray()) await StopAsync(pluginId);
        }
        finally
        {
            gate.Release();
            gate.Dispose();
        }
    }

    public void TerminateAll()
    {
        foreach (var session in running.Values.ToArray())
        {
            try
            {
                if (!session.Host.HasExited) session.Host.Kill(entireProcessTree: true);
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // The process already ended or cannot be queried during shutdown.
            }
        }
    }

    private async Task StartAsync(PluginDiagnostic plugin, string culture, CancellationToken cancellationToken)
    {
        var manifest = plugin.Manifest!;
        SetStatus(new PluginRuntimeInfo(manifest.Id, "Starting"));
        Process? host = null;
        NamedPipeClientStream? pipe = null;
        PluginProtocolSession? protocol = null;
        try
        {
            if (!File.Exists(pluginHostPath)) throw new FileNotFoundException("Plana.PluginHost was not found.", pluginHostPath);
            var pipeName = $"plana-plugin-{Environment.ProcessId}-{Guid.NewGuid():N}";
            var startInfo = new ProcessStartInfo(pluginHostPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add("--pipe");
            startInfo.ArgumentList.Add(pipeName);
            startInfo.ArgumentList.Add("--manifest");
            startInfo.ArgumentList.Add(plugin.ManifestPath);
            host = Process.Start(startInfo) ?? throw new InvalidOperationException("The Plugin Host did not start.");

            pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(StartupTimeout);
            await pipe.ConnectAsync(deadline.Token);
            protocol = new PluginProtocolSession(pipe);
            var contributions = await protocol.InitializeAsync(manifest, culture, StartupTimeout, deadline.Token);

            var session = new RunningPlugin(host, pipe, protocol);
            running.Add(manifest.Id, session);
            lock (contributionPacks) contributionPacks[manifest.Id] = CreateActionPack(manifest, contributions.Actions);
            lock (contributionSets) contributionSets[manifest.Id] = contributions;
            SetStatus(new PluginRuntimeInfo(manifest.Id, "Ready"));
            _ = MonitorExitAsync(manifest.Id, session);
        }
        catch (Exception exception)
        {
            if (host is { HasExited: false }) host.Kill(entireProcessTree: true);
            if (protocol is not null) await protocol.DisposeAsync();
            pipe?.Dispose();
            host?.Dispose();
            SetStatus(new PluginRuntimeInfo(manifest.Id, "Failed", exception.Message));
        }
    }

    private async Task StopAsync(string pluginId)
    {
        if (!running.Remove(pluginId, out var session)) return;
        lock (contributionPacks) contributionPacks.Remove(pluginId);
        lock (contributionSets) contributionSets.Remove(pluginId);
        await session.Protocol.DisposeAsync();
        session.Pipe.Dispose();
        if (!session.Host.HasExited)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try { await session.Host.WaitForExitAsync(deadline.Token); }
            catch (OperationCanceledException) { session.Host.Kill(entireProcessTree: true); }
        }
        session.Host.Dispose();
        SetStatus(new PluginRuntimeInfo(pluginId, "Stopped"));
    }

    private async Task MonitorExitAsync(string pluginId, RunningPlugin session)
    {
        await session.Host.WaitForExitAsync();
        await gate.WaitAsync();
        try
        {
            if (!running.TryGetValue(pluginId, out var current) || !ReferenceEquals(current, session)) return;
            running.Remove(pluginId);
            lock (contributionPacks) contributionPacks.Remove(pluginId);
            lock (contributionSets) contributionSets.Remove(pluginId);
            SetStatus(new PluginRuntimeInfo(pluginId, "Exited", $"Plugin Host exited with code {session.Host.ExitCode}."));
            await session.Protocol.DisposeAsync();
            session.Pipe.Dispose();
            session.Host.Dispose();
        }
        finally
        {
            gate.Release();
        }
    }

    private void SetStatus(PluginRuntimeInfo status)
    {
        lock (statuses) statuses[status.PluginId] = status;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static ActionPack CreateActionPack(
        PluginManifest manifest,
        IReadOnlyList<PluginActionContribution> contributions) => new(
        $"plugin.{manifest.Id}",
        manifest.Id,
        manifest.Version,
        manifest.Publisher,
        contributions.Select(action => new ActionDefinition(
            $"{manifest.Id}.{action.Id}",
            action.Label,
            ActionKinds.PluginInvoke,
            new Dictionary<string, string>
            {
                ["pluginId"] = manifest.Id,
                ["actionId"] = action.Id,
            },
            new HashSet<string>(action.Capabilities, StringComparer.OrdinalIgnoreCase),
            action.RequiresConfirmation,
            action.Description?.Trim() ?? string.Empty)).ToArray(),
        SourceDirectory: manifest.PackageDirectory);

    private sealed record RunningPlugin(Process Host, NamedPipeClientStream Pipe, PluginProtocolSession Protocol);
}
