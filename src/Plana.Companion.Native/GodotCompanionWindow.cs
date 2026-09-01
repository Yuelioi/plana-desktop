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
    private const long WsExLayered = 0x00080000;
    private const long WsExTransparent = 0x20;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
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
    private ManualResetEventSlim _inputModeAcknowledged = new(false);
    private Forms.Timer? _healthTimer;
    private Forms.Timer? _chatPlacementTimer;
    private System.Threading.Timer? _settingsTimer;
    private string? _settingsPath;
    private DateTime _settingsLastWriteUtc;
    private CompanionSurfaceState _state = new(null, null, 340, 520, 1, true);
    private bool _closing;
    private bool _visible = true;
    private bool _disposed;
    private bool _fullPassThrough;
    private readonly nint _rendererJob;
    private readonly CompanionChatInput _chatInput;
    private readonly CompanionSpeechBubble _speechBubble;
    private readonly GlobalHotkey _quickLaunchHotkey;
    private DesktopSettings _currentSettings;
    private DateTime _lastHoverUtc = DateTime.MinValue;

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
        _currentSettings = settings;
        _actions = actions.ToDictionary(action => action.Id, StringComparer.OrdinalIgnoreCase);
        _interactionBindings = new Dictionary<string, string>(settings.InteractionBindings, StringComparer.OrdinalIgnoreCase);
        _chatInput = new CompanionChatInput(
            settings.UiCulture.StartsWith("zh", StringComparison.OrdinalIgnoreCase),
            SendChatAsync)
        {
            TopMost = settings.AlwaysOnTop,
        };
        _speechBubble = new CompanionSpeechBubble { TopMost = settings.AlwaysOnTop };
        ConfigurePinnedActions(settings);
        _ = _speechBubble.Handle;
        _ = _chatInput.Handle;
        _quickLaunchHotkey = new GlobalHotkey(_chatInput.Handle, OpenQuickLaunch);
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
        UpdateChatTopMost(state.AlwaysOnTop);
        UpdateBubbleTopMost(state.AlwaysOnTop);
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
        _chatInput.Hide();
        _speechBubble.Hide();
    }

    public void SetPassThrough(bool enabled)
    {
        if (WindowHandle == 0) return;
        if (!enabled) SetTransparentWindowStyle(false);
        lock (_sync)
        {
            if (_writer is null) return;
            _inputModeAcknowledged.Reset();
            _writer.WriteLine(JsonSerializer.Serialize(new { type = "set_input_mode", passThrough = enabled }));
        }
        if (!_inputModeAcknowledged.Wait(TimeSpan.FromSeconds(3)))
            throw new TimeoutException("Renderer did not acknowledge input mode change.");
        if (enabled) SetTransparentWindowStyle(true);
        _fullPassThrough = enabled;
        if (enabled) _chatInput.Hide();
    }

    private void SetTransparentWindowStyle(bool enabled)
    {
        var style = GetWindowLongPtr(WindowHandle, GwlExStyle).ToInt64();
        style = enabled ? style | WsExLayered | WsExTransparent : style & ~WsExTransparent & ~WsExLayered;
        SetWindowLongPtr(WindowHandle, GwlExStyle, new nint(style));
        SetWindowPos(WindowHandle, 0, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
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

    public void ShowBubble(string text, bool isError = false)
    {
        if (_speechBubble.InvokeRequired)
        {
            _speechBubble.BeginInvoke(() => ShowBubble(text, isError));
            return;
        }
        if (string.IsNullOrWhiteSpace(text)) _speechBubble.ClearMessage();
        else _speechBubble.ShowMessage(text.Length <= 900 ? text : string.Concat(text.AsSpan(0, 897), "…"), isError);
        PositionSpeechBubble();
    }

    public void ShowThinkingBubble()
    {
        if (_speechBubble.InvokeRequired)
        {
            _speechBubble.BeginInvoke(ShowThinkingBubble);
            return;
        }
        _speechBubble.ShowThinking(_currentSettings.UiCulture.StartsWith("zh", StringComparison.OrdinalIgnoreCase));
        PositionSpeechBubble();
    }

    private async Task SendChatAsync(string prompt)
    {
        ShowThinkingBubble();
        Perform(new CharacterPerformanceIntent(CharacterEmotion.Worried, CharacterGesture.LookAtPointer));
        try
        {
            var response = await AiChatService.SendAsync(_currentSettings, prompt, CancellationToken.None);
            ShowBubble(response);
            Perform(new CharacterPerformanceIntent(CharacterEmotion.Happy, CharacterGesture.Blink));
        }
        catch (Exception exception)
        {
            ShowBubble(exception.Message, isError: true);
            Perform(new CharacterPerformanceIntent(CharacterEmotion.Sad));
        }
    }

    private void PositionChatInput()
    {
        if (!_visible || WindowHandle == 0 || !GetWindowRect(WindowHandle, out var rect)) return;
        var workingArea = Forms.Screen.FromHandle(WindowHandle).WorkingArea;
        var width = Math.Max(280, rect.Width);
        var left = Math.Clamp(rect.Left, workingArea.Left, Math.Max(workingArea.Left, workingArea.Right - width));
        var top = rect.Bottom + 8;
        if (top + _chatInput.Height > workingArea.Bottom) top = Math.Max(workingArea.Top, rect.Top - _chatInput.Height - 8);
        _chatInput.SetBounds(left, top, width, _chatInput.Height);
    }

    private void UpdateHoverSurfaces()
    {
        if (!_visible || _fullPassThrough || WindowHandle == 0 || !GetWindowRect(WindowHandle, out var rect))
        {
            if (!_chatInput.ShouldRemainVisible) _chatInput.Hide();
            return;
        }

        var cursor = Forms.Cursor.Position;
        var modelZone = Rectangle.FromLTRB(rect.Left - 16, rect.Top - 16, rect.Right + 16, rect.Bottom + 16);
        var dockZone = new Rectangle(rect.Left - 20, rect.Bottom - 12, rect.Width + 40, _chatInput.Height + 32);
        var hovering = modelZone.Contains(cursor) || dockZone.Contains(cursor) || (_chatInput.Visible && _chatInput.Bounds.Contains(cursor));
        if (hovering)
        {
            _lastHoverUtc = DateTime.UtcNow;
            if (!_chatInput.Visible) _chatInput.Show();
            PositionChatInput();
            return;
        }

        if (!_chatInput.ShouldRemainVisible && DateTime.UtcNow - _lastHoverUtc > TimeSpan.FromMilliseconds(500))
            _chatInput.Hide();
    }

    private static void OpenQuickLaunch() =>
        Process.Start(new ProcessStartInfo("plana://commands") { UseShellExecute = true });

    private void ConfigurePinnedActions(DesktopSettings settings)
    {
        var entries = settings.PinnedCompanionActionIds
            .Select(id => _actions.GetValueOrDefault(id))
            .Where(entry => entry is not null)
            .Select(entry => (entry!.Name, (Action)(() => _ = Task.Run(() => ExecuteActionByIdAsync(entry.Id)))))
            .ToArray();
        _chatInput.ConfigureQuickActions(entries);
    }

    private void PositionSpeechBubble()
    {
        if (!_visible || !_speechBubble.Visible || WindowHandle == 0 || !GetWindowRect(WindowHandle, out var rect)) return;
        var workingArea = Forms.Screen.FromHandle(WindowHandle).WorkingArea;
        var width = Math.Max(260, rect.Width - 20);
        var left = Math.Clamp(rect.Left + 10, workingArea.Left, Math.Max(workingArea.Left, workingArea.Right - width));
        var top = Math.Clamp(rect.Top + (int)(rect.Height * 0.22), workingArea.Top, Math.Max(workingArea.Top, workingArea.Bottom - _speechBubble.Height));
        SetWindowPos(_speechBubble.Handle, new nint(-1), left, top, width, _speechBubble.Height, SwpNoActivate);
    }

    private void UpdateChatTopMost(bool topMost)
    {
        if (_chatInput.IsHandleCreated && _chatInput.InvokeRequired)
        {
            _chatInput.BeginInvoke(() => _chatInput.TopMost = topMost);
            return;
        }
        _chatInput.TopMost = topMost;
    }

    private void UpdateBubbleTopMost(bool topMost)
    {
        if (_speechBubble.IsHandleCreated && _speechBubble.InvokeRequired)
        {
            _speechBubble.BeginInvoke(() => _speechBubble.TopMost = topMost);
            return;
        }
        _speechBubble.TopMost = topMost;
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
        _chatPlacementTimer = new Forms.Timer { Interval = 100 };
        _chatPlacementTimer.Tick += (_, _) => { UpdateHoverSurfaces(); PositionSpeechBubble(); };
        _chatPlacementTimer.Start();
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
        if (_fullPassThrough) SetPassThrough(true);
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
                if (type == "input_mode") _inputModeAcknowledged.Set();
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
        await ExecuteActionByIdAsync(actionId);
    }

    private async Task ExecuteActionByIdAsync(string actionId)
    {
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
            _currentSettings = settings;
            _interactionBindings = new Dictionary<string, string>(settings.InteractionBindings, StringComparer.OrdinalIgnoreCase);
            ConfigurePinnedActions(settings);
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
        _chatPlacementTimer?.Dispose();
        _settingsTimer?.Dispose();
        _quickLaunchHotkey.Dispose();
        _chatInput.Dispose();
        _speechBubble.Dispose();
        StopRenderer();
        _ready.Dispose();
        _acknowledged.Dispose();
        _inputModeAcknowledged.Dispose();
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

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint window, int index, nint value);
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
