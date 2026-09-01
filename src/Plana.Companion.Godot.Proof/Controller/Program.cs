using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using Plana.Core.Companion;

var arguments = args.Select((value, index) => (value, index)).ToDictionary(item => item.value, item => item.index);
if (!arguments.TryGetValue("--godot", out var godotIndex) || godotIndex + 1 >= args.Length ||
    !arguments.TryGetValue("--project", out var projectIndex) || projectIndex + 1 >= args.Length)
{
    Console.Error.WriteLine("Usage: controller --godot <godot.exe> --project <project-dir> [--smoke]");
    return 2;
}

var godotPath = Path.GetFullPath(args[godotIndex + 1]);
var projectPath = Path.GetFullPath(args[projectIndex + 1]);
var smoke = args.Contains("--smoke", StringComparer.OrdinalIgnoreCase);
using var supervisor = new ProofSupervisor(godotPath, projectPath);

if (smoke)
{
    supervisor.Start();
    supervisor.Render("Started");
    supervisor.SetPassThrough(true);
    supervisor.Render("PassThrough enabled");
    if (!supervisor.IsPassThrough) throw new InvalidOperationException("WS_EX_TRANSPARENT was not applied.");
    supervisor.SetPassThrough(false);
    supervisor.Render("Interactive restored");
    if (supervisor.IsPassThrough) throw new InvalidOperationException("WS_EX_TRANSPARENT was not removed.");
    var originalBounds = supervisor.Bounds;
    supervisor.MoveBy(30, 20);
    supervisor.Render("Window moved +30,+20");
    if (supervisor.Bounds.Left != originalBounds.Left + 30 || supervisor.Bounds.Top != originalBounds.Top + 20)
        throw new InvalidOperationException("Renderer window did not move by the requested offset.");
    supervisor.MoveTo(originalBounds.Left, originalBounds.Top);
    if (supervisor.Bounds != originalBounds) throw new InvalidOperationException("Renderer window bounds were not restored.");
    supervisor.Perform(new CharacterPerformanceIntent(CharacterEmotion.Affectionate));
    supervisor.Render("Affectionate performance acknowledged");
    supervisor.Perform(new CharacterPerformanceIntent(CharacterEmotion.Happy, CharacterGesture.HeadPat));
    supervisor.Render("Happy head-pat performance acknowledged");
    var idle = supervisor.MeasureIdle(TimeSpan.FromSeconds(1));
    supervisor.Render($"Idle measured: CPU {idle.CpuMilliseconds:F1} ms, WS {idle.WorkingSetBytes / 1024 / 1024} MiB");
    var firstProcessId = supervisor.ProcessId;
    supervisor.Restart();
    supervisor.Render("Renderer restarted");
    if (supervisor.ProcessId == firstProcessId) throw new InvalidOperationException("Renderer process was not replaced.");
    supervisor.Stop();
    supervisor.Render("Stopped");
    Console.WriteLine("PROOF_SUPERVISOR_SMOKE_OK");
    return 0;
}

supervisor.Start();
while (true)
{
    supervisor.Render("Interactive controller");
    var key = Console.ReadKey(intercept: true).Key;
    if (key == ConsoleKey.Q) break;
    if (key == ConsoleKey.T) supervisor.SetPassThrough(!supervisor.IsPassThrough);
    if (key == ConsoleKey.R) supervisor.Restart();
    if (key == ConsoleKey.H) supervisor.Perform(new CharacterPerformanceIntent(CharacterEmotion.Happy));
    if (key == ConsoleKey.P) supervisor.Perform(new CharacterPerformanceIntent(CharacterEmotion.Happy, CharacterGesture.HeadPat));
    if (key == ConsoleKey.L) supervisor.Perform(new CharacterPerformanceIntent(CharacterEmotion.Affectionate));
    if (key == ConsoleKey.LeftArrow) supervisor.MoveBy(-10, 0);
    if (key == ConsoleKey.RightArrow) supervisor.MoveBy(10, 0);
    if (key == ConsoleKey.UpArrow) supervisor.MoveBy(0, -10);
    if (key == ConsoleKey.DownArrow) supervisor.MoveBy(0, 10);
}

return 0;

sealed class ProofSupervisor(string godotPath, string projectPath) : IDisposable
{
    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x20;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;

    private Process? renderer;
    private nint window;
    private long originalStyle;
    private ManualResetEventSlim rendererReady = new(false);
    private readonly PlanaPerformancePlanner planner = new();
    private TcpListener? listener;
    private TcpClient? controllerClient;
    private StreamWriter? controllerWriter;
    private ManualResetEventSlim commandAcknowledged = new(false);
    private long startupMilliseconds;

    public int ProcessId => renderer is { HasExited: false } ? renderer.Id : 0;
    public bool IsPassThrough => window != 0 && (GetWindowLongPtr(window, GwlExStyle).ToInt64() & WsExTransparent) != 0;
    public uint Dpi => window == 0 ? 0 : GetDpiForWindow(window);
    public WindowBounds Bounds
    {
        get
        {
            if (window == 0 || !GetWindowRect(window, out var rect)) return default;
            return new WindowBounds(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }
    }

    public void Start()
    {
        if (renderer is { HasExited: false }) return;
        rendererReady.Dispose();
        rendererReady = new ManualResetEventSlim(false);
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var controllerPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        var startup = Stopwatch.StartNew();
        renderer = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = godotPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                ArgumentList = { "--path", projectPath, "--", $"controller_port={controllerPort}" }
            },
            EnableRaisingEvents = true
        };
        renderer.OutputDataReceived += (_, eventArgs) => ObserveRendererOutput(eventArgs.Data);
        renderer.ErrorDataReceived += (_, eventArgs) => ObserveRendererOutput(eventArgs.Data);
        if (!renderer.Start()) throw new InvalidOperationException("Godot did not start.");
        renderer.BeginOutputReadLine();
        renderer.BeginErrorReadLine();

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (renderer.HasExited) throw new InvalidOperationException($"Godot exited with code {renderer.ExitCode}.");
            renderer.Refresh();
            if (renderer.MainWindowHandle != 0) break;
            Thread.Sleep(50);
        }

        if (!rendererReady.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Godot did not emit PROOF_READY.");
        controllerClient = listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        controllerWriter = new StreamWriter(controllerClient.GetStream()) { AutoFlush = true };
        _ = ReadControllerResponsesAsync(controllerClient, commandAcknowledged);
        startup.Stop();
        startupMilliseconds = startup.ElapsedMilliseconds;
        renderer.Refresh();
        window = renderer.MainWindowHandle;
        if (window == 0) throw new TimeoutException("Godot renderer HWND was not created.");
        originalStyle = GetWindowLongPtr(window, GwlExStyle).ToInt64();
    }

    private static async Task ReadControllerResponsesAsync(TcpClient client, ManualResetEventSlim acknowledged)
    {
        using var reader = new StreamReader(client.GetStream());
        while (await reader.ReadLineAsync() is { } line)
        {
            Console.WriteLine($"[controller] {line}");
            if (line.Contains("\"type\":\"performed\"", StringComparison.Ordinal)) acknowledged.Set();
        }
    }

    private void ObserveRendererOutput(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        Console.WriteLine($"[renderer] {line}");
        if (line.Contains("PROOF_READY", StringComparison.Ordinal)) rendererReady.Set();
    }

    public void SetPassThrough(bool enabled)
    {
        if (window == 0) throw new InvalidOperationException("Renderer is not running.");
        var style = GetWindowLongPtr(window, GwlExStyle).ToInt64();
        style = enabled ? style | WsExTransparent : style & ~WsExTransparent;
        SetLastError(0);
        var previous = SetWindowLongPtr(window, GwlExStyle, new nint(style));
        if (previous == 0 && Marshal.GetLastWin32Error() != 0)
            throw new InvalidOperationException($"SetWindowLongPtr failed: {Marshal.GetLastWin32Error()}.");
        SetWindowPos(window, 0, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
        Thread.Sleep(100);
    }

    public void Restart()
    {
        Stop();
        Start();
    }

    public void MoveBy(int deltaX, int deltaY)
    {
        var bounds = Bounds;
        MoveTo(bounds.Left + deltaX, bounds.Top + deltaY);
    }

    public void MoveTo(int x, int y)
    {
        if (window == 0) throw new InvalidOperationException("Renderer is not running.");
        SetWindowPos(window, 0, x, y, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);
    }

    public void Perform(CharacterPerformanceIntent intent)
    {
        if (controllerWriter is null) throw new InvalidOperationException("Renderer command channel is not connected.");
        var plan = planner.Plan(intent);
        commandAcknowledged.Reset();
        controllerWriter.WriteLine(JsonSerializer.Serialize(new { type = "perform", cues = plan.Cues }));
        if (!commandAcknowledged.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Renderer did not acknowledge performance command.");
    }

    public IdleMeasurement MeasureIdle(TimeSpan interval)
    {
        if (renderer is null || renderer.HasExited) throw new InvalidOperationException("Renderer is not running.");
        renderer.Refresh();
        var before = renderer.TotalProcessorTime;
        Thread.Sleep(interval);
        renderer.Refresh();
        return new IdleMeasurement((renderer.TotalProcessorTime - before).TotalMilliseconds, renderer.WorkingSet64);
    }

    public void Stop()
    {
        if (renderer is null) return;
        controllerWriter?.Dispose();
        controllerWriter = null;
        controllerClient?.Dispose();
        controllerClient = null;
        listener?.Stop();
        listener = null;
        if (!renderer.HasExited)
        {
            if (window != 0) SetWindowLongPtr(window, GwlExStyle, new nint(originalStyle));
            renderer.Kill(entireProcessTree: true);
            renderer.WaitForExit(5000);
        }
        renderer.Dispose();
        renderer = null;
        window = 0;
    }

    public void Render(string action)
    {
        var running = renderer is { HasExited: false };
        var style = window == 0 ? 0 : GetWindowLongPtr(window, GwlExStyle).ToInt64();
        if (!Console.IsOutputRedirected) Console.Clear();
        Console.WriteLine("PROTOTYPE — Godot renderer supervisor");
        Console.WriteLine($"Action:       {action}");
        Console.WriteLine($"State:        {(running ? (IsPassThrough ? "PassThrough" : "Interactive") : "Stopped")}");
        Console.WriteLine($"Process:      {(running ? renderer!.Id : 0)}");
        Console.WriteLine($"HWND:         0x{window:X}");
        Console.WriteLine($"ExtendedStyle: 0x{style:X}");
        Console.WriteLine($"Transparent:  {(style & WsExTransparent) != 0}");
        Console.WriteLine($"DPI:          {Dpi}");
        Console.WriteLine($"Bounds:       {Bounds}");
        Console.WriteLine($"Startup:      {startupMilliseconds} ms");
        Console.WriteLine($"Working set:  {(running ? renderer!.WorkingSet64 / 1024 / 1024 : 0)} MiB");
        Console.WriteLine();
        Console.WriteLine("[H] happy  [P] head pat  [L] love  [T] pass-through  [R] restart  [Arrows] move  [Q] quit");
    }

    public void Dispose() => Stop();

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint window, int index, nint value);

    [DllImport("kernel32.dll")]
    private static extern void SetLastError(int errorCode);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out NativeRect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }
}

readonly record struct WindowBounds(int Left, int Top, int Width, int Height);
readonly record struct IdleMeasurement(double CpuMilliseconds, long WorkingSetBytes);
