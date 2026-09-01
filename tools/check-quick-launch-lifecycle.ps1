param([string]$CapturePath, [string]$Text)

$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

public static class QuickLaunchLifecycleProbe {
    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr state);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    public sealed record Sample(long Milliseconds, bool Visible, bool Foreground);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr state);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out Rect bounds);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extraInfo);

    public static IntPtr FindLauncher(uint processId) {
        IntPtr result = IntPtr.Zero;
        EnumWindows((hwnd, _) => {
            GetWindowThreadProcessId(hwnd, out var owner);
            if (owner != processId || !IsWindowVisible(hwnd) || !GetWindowRect(hwnd, out var bounds)) return true;
            var width = bounds.Right - bounds.Left;
            var height = bounds.Bottom - bounds.Top;
            if (width == 680 && height >= 132 && height <= 488) { result = hwnd; return false; }
            return true;
        }, IntPtr.Zero);
        return result;
    }

    public static void PressHotkey() {
        const byte Control = 0x11, Alt = 0x12, Space = 0x20;
        const uint KeyUp = 0x0002;
        keybd_event(Control, 0, 0, UIntPtr.Zero);
        keybd_event(Alt, 0, 0, UIntPtr.Zero);
        keybd_event(Space, 0, 0, UIntPtr.Zero);
        keybd_event(Space, 0, KeyUp, UIntPtr.Zero);
        keybd_event(Alt, 0, KeyUp, UIntPtr.Zero);
        keybd_event(Control, 0, KeyUp, UIntPtr.Zero);
    }

    public static Sample[] Run(uint processId) {
        PressHotkey();
        var samples = new List<Sample>();
        var started = Environment.TickCount64;
        while (Environment.TickCount64 - started < 1000) {
            var launcher = FindLauncher(processId);
            samples.Add(new Sample(Environment.TickCount64 - started, launcher != IntPtr.Zero, launcher != IntPtr.Zero && launcher == GetForegroundWindow()));
            Thread.Sleep(10);
        }
        return samples.ToArray();
    }
}
'@

$process = Get-Process -Name 'Plana.Desktop' -ErrorAction Stop |
    Where-Object Path -like '*artifacts\native-win-x64\Plana.Desktop.exe' |
    Select-Object -First 1
if (-not $process) { throw 'Published native Plana.Desktop.exe is not running.' }
$samples = [QuickLaunchLifecycleProbe]::Run([uint32]$process.Id)
$visibleSamples = @($samples | Where-Object Visible)
$foregroundSamples = @($samples | Where-Object Foreground)
$firstVisible = $visibleSamples | Select-Object -First 1
$lastVisible = $visibleSamples | Select-Object -Last 1
$visibleDuration = if ($firstVisible -and $lastVisible) { $lastVisible.Milliseconds - $firstVisible.Milliseconds } else { 0 }
$passed = $visibleDuration -ge 500 -and $foregroundSamples.Count -gt 0

if ($CapturePath) {
    Add-Type -AssemblyName System.Drawing
    $window = [QuickLaunchLifecycleProbe]::FindLauncher([uint32]$process.Id)
    if ($window -eq [IntPtr]::Zero) {
        [QuickLaunchLifecycleProbe]::PressHotkey()
        $deadline = [DateTime]::UtcNow.AddSeconds(1)
        do {
            Start-Sleep -Milliseconds 20
            $window = [QuickLaunchLifecycleProbe]::FindLauncher([uint32]$process.Id)
        } while ($window -eq [IntPtr]::Zero -and [DateTime]::UtcNow -lt $deadline)
    }
    if ($window -eq [IntPtr]::Zero) { throw 'Quick Launch did not become visible for capture.' }
    if ($Text) {
        (New-Object -ComObject WScript.Shell).SendKeys($Text)
        Start-Sleep -Milliseconds 800
        $window = [QuickLaunchLifecycleProbe]::FindLauncher([uint32]$process.Id)
        if ($window -eq [IntPtr]::Zero) { throw 'Quick Launch disappeared while receiving keyboard input.' }
    }
    $bounds = [QuickLaunchLifecycleProbe+Rect]::new()
    [QuickLaunchLifecycleProbe]::GetWindowRect($window, [ref]$bounds) | Out-Null
    $width = $bounds.Right - $bounds.Left
    $height = $bounds.Bottom - $bounds.Top
    $bitmap = [System.Drawing.Bitmap]::new($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($bounds.Left, $bounds.Top, 0, 0, [System.Drawing.Size]::new($width, $height))
        $bitmap.Save([System.IO.Path]::GetFullPath($CapturePath), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $graphics.Dispose(); $bitmap.Dispose() }

    $notepad = Start-Process notepad.exe -PassThru
    try {
        Start-Sleep -Milliseconds 250
        (New-Object -ComObject WScript.Shell).AppActivate($notepad.Id) | Out-Null
        Start-Sleep -Milliseconds 250
        $dismissedOutside = [QuickLaunchLifecycleProbe]::FindLauncher([uint32]$process.Id) -eq [IntPtr]::Zero
    }
    finally { Stop-Process -Id $notepad.Id -Force -ErrorAction SilentlyContinue }
}
else { $dismissedOutside = $true }

[pscustomobject]@{
    Check = 'QuickLaunchRemainsVisibleAfterHotkey'
    Passed = $passed
    VisibleDurationMs = $visibleDuration
    ForegroundSamples = $foregroundSamples.Count
    DismissedAfterOutsideActivation = $dismissedOutside
}
if (-not $passed -or -not $dismissedOutside) { exit 1 }
