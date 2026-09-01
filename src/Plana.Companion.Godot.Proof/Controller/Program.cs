using System.Diagnostics;
using System.Runtime.InteropServices;

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

    public int ProcessId => renderer is { HasExited: false } ? renderer.Id : 0;
    public bool IsPassThrough => window != 0 && (GetWindowLongPtr(window, GwlExStyle).ToInt64() & WsExTransparent) != 0;

    public void Start()
    {
        if (renderer is { HasExited: false }) return;
        renderer = Process.Start(new ProcessStartInfo
        {
            FileName = godotPath,
            UseShellExecute = false,
            ArgumentList = { "--path", projectPath }
        }) ?? throw new InvalidOperationException("Godot did not start.");

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (renderer.HasExited) throw new InvalidOperationException($"Godot exited with code {renderer.ExitCode}.");
            renderer.Refresh();
            if (renderer.MainWindowHandle != 0) break;
            Thread.Sleep(50);
        }

        window = renderer.MainWindowHandle;
        if (window == 0) throw new TimeoutException("Godot renderer HWND was not created.");
        // Godot creates the HWND before its display backend finishes applying final flags.
        // The production protocol will replace this bounded proof delay with renderer_ready.
        Thread.Sleep(1200);
        renderer.Refresh();
        window = renderer.MainWindowHandle;
        originalStyle = GetWindowLongPtr(window, GwlExStyle).ToInt64();
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

    public void Stop()
    {
        if (renderer is null) return;
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
        Console.WriteLine();
        Console.WriteLine("[T] toggle pass-through  [R] restart renderer  [Q] quit");
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
}
