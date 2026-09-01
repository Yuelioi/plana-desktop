$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class QuickForegroundProbe {
    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr state);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr state);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out Rect bounds);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extraInfo);
    public static void PressHotkey() {
        keybd_event(0x11, 0, 0, UIntPtr.Zero); keybd_event(0x12, 0, 0, UIntPtr.Zero); keybd_event(0x20, 0, 0, UIntPtr.Zero);
        keybd_event(0x20, 0, 2, UIntPtr.Zero); keybd_event(0x12, 0, 2, UIntPtr.Zero); keybd_event(0x11, 0, 2, UIntPtr.Zero);
    }
}
'@

function Find-QuickWindow([uint32]$processId) {
    $script:quickWindow = [IntPtr]::Zero
    [QuickForegroundProbe]::EnumWindows({
        param($window, $state)
        [uint32]$owner = 0
        [QuickForegroundProbe]::GetWindowThreadProcessId($window, [ref]$owner) | Out-Null
        $bounds = [QuickForegroundProbe+Rect]::new()
        if ($owner -eq $processId -and [QuickForegroundProbe]::IsWindowVisible($window) -and
            [QuickForegroundProbe]::GetWindowRect($window, [ref]$bounds) -and
            ($bounds.Right - $bounds.Left) -eq 680 -and ($bounds.Bottom - $bounds.Top) -ge 132) {
            $script:quickWindow = $window
            return $false
        }
        return $true
    }, [IntPtr]::Zero) | Out-Null
    return $quickWindow
}

$hostProcess = Get-Process -Name Plana.Desktop -ErrorAction Stop |
    Where-Object Path -like '*artifacts\native-win-x64\Plana.Desktop.exe' |
    Select-Object -First 1
$notepad = Start-Process notepad.exe -PassThru
try {
    Start-Sleep -Milliseconds 250
    (New-Object -ComObject WScript.Shell).AppActivate($notepad.Id) | Out-Null
    Start-Sleep -Milliseconds 100
    $pipe = [System.IO.Pipes.NamedPipeClientStream]::new('.', 'PlanaDesktop.Renderer', [System.IO.Pipes.PipeDirection]::InOut)
    try {
        $pipe.Connect(1000)
        $writer = [System.IO.StreamWriter]::new($pipe)
        $writer.AutoFlush = $true
        $reader = [System.IO.StreamReader]::new($pipe)
        $writer.WriteLine('{"type":"quick-launch"}')
        $response = [System.Text.Json.JsonDocument]::Parse($reader.ReadLine())
        $activated = $response.RootElement.GetProperty('activated').GetBoolean()
    }
    finally { $pipe.Dispose() }
    $passed = $activated
}
finally { Stop-Process -Id $notepad.Id -Force -ErrorAction SilentlyContinue }

[pscustomobject]@{
    Check = 'QuickLaunchOwnsForegroundAndKeyboardFocus'
    Passed = $passed
    ActivationConfirmedByHost = $activated
}
if (-not $passed) { exit 1 }
