param([string]$CapturePath = 'artifacts\companion-dock-wpf.png', [string]$Text)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class CompanionDockProbe {
    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr state);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr state);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out Rect bounds);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extraInfo);
}
'@

function Find-Window([uint32]$processId, [scriptblock]$predicate) {
    $script:foundWindow = [IntPtr]::Zero
    $script:foundBounds = [CompanionDockProbe+Rect]::new()
    [CompanionDockProbe]::EnumWindows({
        param($window, $state)
        [uint32]$owner = 0
        [CompanionDockProbe]::GetWindowThreadProcessId($window, [ref]$owner) | Out-Null
        $bounds = [CompanionDockProbe+Rect]::new()
        if ($owner -eq $processId -and [CompanionDockProbe]::IsWindowVisible($window) -and
            [CompanionDockProbe]::GetWindowRect($window, [ref]$bounds) -and (& $predicate $bounds)) {
            $script:foundWindow = $window
            $script:foundBounds = $bounds
            return $false
        }
        return $true
    }, [IntPtr]::Zero) | Out-Null
    return @($foundWindow, $foundBounds)
}

$renderer = Get-Process -Name Godot -ErrorAction Stop | Select-Object -First 1
$rendererResult = Find-Window ([uint32]$renderer.Id) { param($bounds) ($bounds.Right - $bounds.Left) -gt 200 -and ($bounds.Bottom - $bounds.Top) -gt 300 }
if ($rendererResult[0] -eq [IntPtr]::Zero) { throw 'Godot renderer window not found.' }
$rendererBounds = $rendererResult[1]
[CompanionDockProbe]::SetCursorPos($rendererBounds.Left + [int](($rendererBounds.Right - $rendererBounds.Left) / 2), $rendererBounds.Bottom - 40) | Out-Null
Start-Sleep -Milliseconds 350

$hostProcess = Get-Process -Name Plana.Desktop -ErrorAction Stop |
    Where-Object Path -like '*artifacts\native-win-x64\Plana.Desktop.exe' |
    Select-Object -First 1
$dockResult = Find-Window ([uint32]$hostProcess.Id) { param($bounds) $height = $bounds.Bottom - $bounds.Top; $height -in 52,82 }
if ($dockResult[0] -eq [IntPtr]::Zero) { throw 'WPF Companion Dock did not become visible on hover.' }
$bounds = $dockResult[1]
if ($Text) {
    [CompanionDockProbe]::SetCursorPos($bounds.Left + 100, $bounds.Bottom - 20) | Out-Null
    [CompanionDockProbe]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [CompanionDockProbe]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 100
    (New-Object -ComObject WScript.Shell).SendKeys($Text)
    Start-Sleep -Milliseconds 200
}
$width = $bounds.Right - $bounds.Left
$height = $bounds.Bottom - $bounds.Top
$bitmap = [System.Drawing.Bitmap]::new($width, $height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
try {
    $graphics.CopyFromScreen($bounds.Left, $bounds.Top, 0, 0, [System.Drawing.Size]::new($width, $height))
    $bitmap.Save([System.IO.Path]::GetFullPath($CapturePath), [System.Drawing.Imaging.ImageFormat]::Png)
}
finally { $graphics.Dispose(); $bitmap.Dispose() }

[pscustomobject]@{ Check = 'CompanionDockAppearsOnHover'; Passed = $true; Width = $width; Height = $height }
