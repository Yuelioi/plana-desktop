using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using Plana.Core.Actions;
using Plana.Core.Companion;
using Plana.Core.Plugins;
using Plana.Core.Settings;
using Forms = System.Windows.Forms;

namespace Plana.Companion.Native;

internal sealed class GodotCompanionWindow : ICompanionController
{
    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x20;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const int SwHide = 0;
    private const int SwShowNoActivate = 4;
    private const uint JobObjectLimitKillOnJobClose = 0x00002000;

    private readonly string _godotPath;
    private readonly string _projectPath;
    private readonly PlanaPerformancePlanner _planner = new();
    private readonly PlanaInteractionPlanner _interactionPlanner = new();
    private readonly PluginRuntimeManager _pluginRuntime;
    private IReadOnlyDictionary<string, NativeActionEntry> _actions;
    private IReadOnlyDictionary<string, string> _interactionBindings;
    private readonly object _sync = new();
    private Process? _renderer;
    private TcpListener? _listener;
    private TcpClient? _client;
    private StreamWriter? _writer;
    private ManualResetEventSlim _ready = new(false);
    private ManualResetEventSlim _acknowledged = new(false);
    private Forms.Timer? _healthTimer;
    private System.Threading.Timer? _settingsTimer;
    private string? _settingsPath;
    private DateTime _settingsLastWriteUtc;
    private CompanionSurfaceState _state = new(null, null, 340, 520, 1, true);
    private bool _closing;
    private bool _visible = true;
    private bool _disposed;
    private readonly nint _rendererJob;

    public nint WindowHandle { get; private set; }
    public event EventHandler? ContextRequested;

    public GodotCompanionWindow(
        string godotPath,
        string projectPath,
        PluginRuntimeManager pluginRuntime,
        DesktopSettings settings,
        IReadOnlyList<NativeActionEntry> actions)
    {
        _godotPath = godotPath;
        _projectPath = projectPath;
        _pluginRuntime = pluginRuntime;
        _actions = actions.ToDictionary(action => action.Id, StringComparer.OrdinalIgnoreCase);
        _interactionBindings = new Dictionary<string, string>(settings.InteractionBindings, StringComparer.OrdinalIgnoreCase);
        _rendererJob = CreateKillOnCloseJob();
        StartRenderer();
    }

    public void Apply(CompanionSurfaceState state)
    {
        _state = state with { Scale = Math.Clamp(state.Scale, 0.5, 2.0) };
        if (WindowHandle == 0) return;
        var width = Math.Max(240, (int)Math.Round(state.Width * _state.Scale));
        var height = Math.Max(360, (int)Math.Round(state.Height * _state.Scale));
        var x = state.Left is null ? GetSystemMetrics(0) - width - 48 : (int)Math.Round(state.Left.Value);
        var y = state.Top is null ? GetSystemMetrics(1) - height - 72 : (int)Math.Round(state.Top.Value);
        var insertAfter = state.AlwaysOnTop ? new nint(-1) : new nint(-2);
        SetWindowPos(WindowHandle, insertAfter, x, y, width, height, SwpNoActivate);
    }

    public CompanionSurfaceState Snapshot()
    {
        if (WindowHandle == 0 || !GetWindowRect(WindowHandle, out var rect)) return _state;
        return _state with { Left = rect.Left, Top = rect.Top, Width = rect.Width / _state.Scale, Height = rect.Height / _state.Scale };
    }

    public void Show()
    {
        _visible = true;
        if (WindowHandle != 0) ShowWindow(WindowHandle, SwShowNoActivate);
    }

    public void Hide()
    {
        _visible = false;
        if (WindowHandle != 0) ShowWindow(WindowHandle, SwHide);
    }

    public void SetPassThrough(bool enabled)
    {
        if (WindowHandle == 0) return;
        var style = GetWindowLongPtr(WindowHandle, GwlExStyle).ToInt64();
        style = enabled ? style | WsExTransparent : style & ~WsExTransparent;
        SetWindowLongPtr(WindowHandle, GwlExStyle, new nint(style));
        SetWindowPos(WindowHandle, 0, 0, 0, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
    }

    public void Perform(CharacterPerformanceIntent intent)
    {
        lock (_sync)
        {
            if (_writer is null) return;
            _acknowledged.Reset();
            _writer.WriteLine(JsonSerializer.Serialize(new { type = "perform", cues = _planner.Plan(intent).Cues }));
        }
        _acknowledged.Wait(TimeSpan.FromSeconds(3));
    }

    public void WatchSettings(string settingsPath)
    {
        _settingsPath = settingsPath;
        _settingsLastWriteUtc = File.Exists(settingsPath) ? File.GetLastWriteTimeUtc(settingsPath) : DateTime.MinValue;
        _settingsTimer = new System.Threading.Timer(_ => CheckSettings(), null, 500, 500);
    }

    public void RunMessageLoop()
    {
        _healthTimer = new Forms.Timer { Interval = 1000 };
        _healthTimer.Tick += (_, _) =>
        {
            if (!_closing && _renderer is { HasExited: true }) RestartRenderer();
        };
        _healthTimer.Start();
        Forms.Application.Run();
    }

    public void Close()
    {
        _closing = true;
        Forms.Application.ExitThread();
    }

    private void StartRenderer()
    {
        _ready.Dispose();
        _ready = new ManualResetEventSlim(false);
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _renderer = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _godotPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                ArgumentList = { "--path", _projectPath, "--", $"controller_port={port}" }
            },
            EnableRaisingEvents = true
        };
        _renderer.OutputDataReceived += (_, args) => ObserveOutput(args.Data);
        _renderer.ErrorDataReceived += (_, args) => ObserveOutput(args.Data);
        if (!_renderer.Start()) throw new InvalidOperationException("Godot renderer did not start.");
        if (!AssignProcessToJobObject(_rendererJob, _renderer.Handle)) throw new Win32Exception(Marshal.GetLastWin32Error());
        _renderer.BeginOutputReadLine();
        _renderer.BeginErrorReadLine();
        if (!_ready.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Godot renderer did not become ready.");
        _client = _listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        _writer = new StreamWriter(_client.GetStream()) { AutoFlush = true };
        _ = ReadResponsesAsync(_client);
        _renderer.Refresh();
        WindowHandle = _renderer.MainWindowHandle;
        if (WindowHandle == 0) throw new Win32Exception("Godot renderer window was not created.");
        Apply(_state);
        if (!_visible) Hide();
    }

    private void ObserveOutput(string? line)
    {
        if (line?.Contains("RENDERER_READY", StringComparison.Ordinal) == true) _ready.Set();
    }

    private async Task ReadResponsesAsync(TcpClient client)
    {
        try
        {
            using var reader = new StreamReader(client.GetStream());
            while (await reader.ReadLineAsync() is { } line)
            {
                using var message = JsonDocument.Parse(line);
                var type = message.RootElement.GetProperty("type").GetString();
                if (type == "performed") _acknowledged.Set();
                if (type == "interaction")
                {
                    var interaction = message.RootElement.GetProperty("interaction").GetString();
                    if (interaction is not null) _ = Task.Run(() => ExecuteInteractionAsync(interaction));
                }
                if (type == "context") ContextRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException && (_closing || client != _client)) { }
    }

    private async Task ExecuteInteractionAsync(string interaction)
    {
        if (!InteractionPolicy.TryGetBinding(_interactionBindings, interaction, out var actionId)) return;
        if (actionId.Equals("builtin.companion.interact", StringComparison.OrdinalIgnoreCase))
        {
            Perform(_interactionPlanner.PlanRandomInteraction());
            return;
        }
        if (!_actions.TryGetValue(actionId, out var action)) return;
        if (action.Definition.Kind != ActionKinds.PluginInvoke)
        {
            NativeActionExecutor.Execute(action);
            return;
        }
        await _pluginRuntime.InvokeAsync(
            action.Definition.Parameters["pluginId"],
            action.Definition.Parameters["actionId"],
            action.Definition.Capabilities,
            (request, token) => NativePluginBroker.ExecuteAsync(request, action.WorkingDirectory, token));
    }

    private void RestartRenderer()
    {
        StopRenderer();
        if (!_closing) StartRenderer();
    }

    private void StopRenderer()
    {
        lock (_sync)
        {
            _writer?.Dispose();
            _writer = null;
            _client?.Dispose();
            _client = null;
            _listener?.Stop();
            _listener = null;
        }
        if (_renderer is not null)
        {
            if (!_renderer.HasExited)
            {
                _renderer.Kill(entireProcessTree: true);
                _renderer.WaitForExit(5000);
            }
            _renderer.Dispose();
            _renderer = null;
        }
        WindowHandle = 0;
    }

    private void CheckSettings()
    {
        if (_settingsPath is null || !File.Exists(_settingsPath)) return;
        var write = File.GetLastWriteTimeUtc(_settingsPath);
        if (write == _settingsLastWriteUtc) return;
        _settingsLastWriteUtc = write;
        try
        {
            var settings = new DesktopSettingsStore(_settingsPath).LoadAsync().GetAwaiter().GetResult();
            _interactionBindings = new Dictionary<string, string>(settings.InteractionBindings, StringComparer.OrdinalIgnoreCase);
            Apply(new CompanionSurfaceState(settings.Left, settings.Top, settings.Width, settings.Height, settings.Scale, settings.AlwaysOnTop));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _closing = true;
        _healthTimer?.Dispose();
        _settingsTimer?.Dispose();
        StopRenderer();
        _ready.Dispose();
        _acknowledged.Dispose();
        CloseHandle(_rendererJob);
    }

    private static nint CreateKillOnCloseJob()
    {
        var job = CreateJobObject(0, null);
        if (job == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        var information = new JobObjectExtendedLimitInformation
        {
            BasicLimitInformation = new JobObjectBasicLimitInformation { LimitFlags = JobObjectLimitKillOnJobClose }
        };
        var size = Marshal.SizeOf<JobObjectExtendedLimitInformation>();
        var pointer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(information, pointer, false);
            if (!SetInformationJobObject(job, 9, pointer, (uint)size)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        catch
        {
            CloseHandle(job);
            throw;
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
        return job;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint window, int index, nint value);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateJobObject(nint securityAttributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(nint job, int informationClass, nint information, uint informationLength);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(nint job, nint process);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(nint handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left; public int Top; public int Right; public int Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }
}
