$ErrorActionPreference = 'Stop'

$companionHost = Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'Plana.Desktop.exe' } | Select-Object -First 1
if (-not $companionHost) { throw 'Plana.Desktop.exe is not running.' }
$rendererInfo = Get-CimInstance Win32_Process | Where-Object ParentProcessId -eq $companionHost.ProcessId | Select-Object -First 1
if (-not $rendererInfo) { throw 'Godot Renderer is not running.' }
$renderer = Get-Process -Id $rendererInfo.ProcessId

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class FullPassProbe {
  [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
  [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out Rect r);
  [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(Point p);
}
'@

$hwnd = $renderer.MainWindowHandle
$before = [FullPassProbe+Rect]::new()
[FullPassProbe]::GetWindowRect($hwnd, [ref]$before) | Out-Null

function Send-PassThrough([bool]$enabled) {
    $pipe = [System.IO.Pipes.NamedPipeClientStream]::new('.', 'PlanaDesktop.Renderer', [System.IO.Pipes.PipeDirection]::InOut, [System.IO.Pipes.PipeOptions]::Asynchronous)
    try {
        $pipe.Connect(3000)
        $writer = [System.IO.StreamWriter]::new($pipe, [System.Text.Encoding]::UTF8, 1024, $true)
        $writer.AutoFlush = $true
        $reader = [System.IO.StreamReader]::new($pipe, [System.Text.Encoding]::UTF8, $false, 1024, $true)
        $writer.WriteLine((@{ Type='pass-through'; Enabled=$enabled } | ConvertTo-Json -Compress))
        return $reader.ReadLine()
    }
    finally { $pipe.Dispose() }
}

try {
    $enableResponse = Send-PassThrough $true
    Start-Sleep -Seconds 1

    $after = [FullPassProbe+Rect]::new()
    [FullPassProbe]::GetWindowRect($hwnd, [ref]$after) | Out-Null
    $point = [FullPassProbe+Point]::new()
    $point.X = $after.Left + [int](($after.Right - $after.Left) * 0.5)
    $point.Y = $after.Top + [int](($after.Bottom - $after.Top) * 0.78)
    $pointWindow = [FullPassProbe]::WindowFromPoint($point)
    $disableResponse = Send-PassThrough $false
    Start-Sleep -Milliseconds 300
    $restoredWindow = [FullPassProbe]::WindowFromPoint($point)

    $checks = [ordered]@{
        RendererAcknowledgesMode = $enableResponse -match '"ok":true'
        CharacterPointPassesThrough = $pointWindow -ne $hwnd
        InteractiveModeRestored = $disableResponse -match '"ok":true' -and $restoredWindow -eq $hwnd
        WindowPositionStable = $after.Left -eq $before.Left -and $after.Top -eq $before.Top
        WindowSizeStable = ($after.Right - $after.Left) -eq ($before.Right - $before.Left) -and ($after.Bottom - $after.Top) -eq ($before.Bottom - $before.Top)
    }
    $checks.GetEnumerator() | ForEach-Object { [pscustomobject]@{ Check=$_.Key; Passed=$_.Value } }
    Write-Output ("Before=({0},{1},{2},{3}) After=({4},{5},{6},{7}) PassWindow=0x{8:X} RestoredWindow=0x{9:X} Renderer=0x{10:X}" -f $before.Left,$before.Top,$before.Right,$before.Bottom,$after.Left,$after.Top,$after.Right,$after.Bottom,$pointWindow.ToInt64(),$restoredWindow.ToInt64(),$hwnd.ToInt64())
    if ($checks.Values -contains $false) { exit 1 }
}
finally {
    Send-PassThrough $false | Out-Null
}
