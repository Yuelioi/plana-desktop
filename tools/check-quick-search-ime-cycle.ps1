$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class QuickImeCycleProbe {
    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr state);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out Rect bounds);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr state);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extraInfo);
    private static void Press(byte key) { keybd_event(key, 0, 0, UIntPtr.Zero); keybd_event(key, 0, 2, UIntPtr.Zero); }
    public static void PressHotkey() {
        keybd_event(0x11, 0, 0, UIntPtr.Zero); keybd_event(0x12, 0, 0, UIntPtr.Zero); Press(0x20);
        keybd_event(0x12, 0, 2, UIntPtr.Zero); keybd_event(0x11, 0, 2, UIntPtr.Zero);
    }
    public static void PressT() => Press(0x54);
    public static void PressBackspace() => Press(0x08);
}
'@

$hostProcess = Get-Process -Name Plana.Desktop -ErrorAction Stop |
    Where-Object Path -like '*artifacts\native-win-x64\Plana.Desktop.exe' |
    Select-Object -First 1

function Get-QuickHeight {
    $script:quickHeight = 0
    [QuickImeCycleProbe]::EnumWindows({
        param($window, $state)
        [uint32]$owner = 0
        [QuickImeCycleProbe]::GetWindowThreadProcessId($window, [ref]$owner) | Out-Null
        $bounds = [QuickImeCycleProbe+Rect]::new()
        if ($owner -eq [uint32]$hostProcess.Id -and [QuickImeCycleProbe]::IsWindowVisible($window) -and
            [QuickImeCycleProbe]::GetWindowRect($window, [ref]$bounds) -and ($bounds.Right - $bounds.Left) -eq 680) {
            $script:quickHeight = $bounds.Bottom - $bounds.Top
            return $false
        }
        return $true
    }, [IntPtr]::Zero) | Out-Null
    return $quickHeight
}

[QuickImeCycleProbe]::PressHotkey()
Start-Sleep -Milliseconds 150
[QuickImeCycleProbe]::PressT()
Start-Sleep -Milliseconds 180
$firstHeight = Get-QuickHeight
[QuickImeCycleProbe]::PressBackspace()
Start-Sleep -Milliseconds 180
$emptyHeight = Get-QuickHeight
[QuickImeCycleProbe]::PressT()
Start-Sleep -Milliseconds 180
$secondHeight = Get-QuickHeight
(New-Object -ComObject WScript.Shell).SendKeys('{ESC}')

$passed = $firstHeight -gt 132 -and $emptyHeight -eq 132 -and $secondHeight -gt 132
[pscustomobject]@{
    Check = 'QuickSearchImeTypeDeleteType'
    Passed = $passed
    FirstTypeHeight = $firstHeight
    EmptyHeight = $emptyHeight
    SecondTypeHeight = $secondHeight
}
if (-not $passed) { exit 1 }
