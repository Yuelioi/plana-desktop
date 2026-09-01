$ErrorActionPreference = 'Stop'

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$companionHost = Get-CimInstance Win32_Process | Where-Object {
    $_.Name -eq 'Plana.Desktop.exe'
} | Select-Object -First 1
if (-not $companionHost) { throw 'Published Plana.Desktop.exe is not running.' }
$renderer = Get-CimInstance Win32_Process | Where-Object ParentProcessId -eq $companionHost.ProcessId | Select-Object -First 1
if (-not $renderer) { throw 'Published Godot Renderer is not running.' }
$rendererProcess = Get-Process -Id $renderer.ProcessId
if ($rendererProcess.MainWindowHandle -eq 0) { throw 'Godot Renderer has no HWND.' }

Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class CompanionCapture {
  [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
}
'@

$rect = [CompanionCapture+Rect]::new()
[CompanionCapture]::GetWindowRect($rendererProcess.MainWindowHandle, [ref]$rect) | Out-Null
$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top
$bitmap = [System.Drawing.Bitmap]::new($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
try {
    $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, [System.Drawing.Size]::new($width, $height))

    function Get-OpaqueBlackRatio([int]$fromY, [int]$toY) {
        $black = 0
        $total = 0
        for ($y = $fromY; $y -lt $toY; $y += 2) {
            for ($x = 0; $x -lt $width; $x += 4) {
                $pixel = $bitmap.GetPixel($x, $y)
                if ($pixel.A -gt 240 -and $pixel.R -lt 8 -and $pixel.G -lt 8 -and $pixel.B -lt 8) { $black++ }
                $total++
            }
        }
        return $black / [Math]::Max(1, $total)
    }

    $topRatio = Get-OpaqueBlackRatio 0 ([Math]::Min(32, $height))
    $bottomRatio = Get-OpaqueBlackRatio ([Math]::Max(0, $height - 32)) $height
}
finally {
    $graphics.Dispose()
    $bitmap.Dispose()
}

$rendererSource = Get-Content -LiteralPath (Join-Path $repoRoot 'src\Plana.Companion.Godot.Renderer\renderer.gd') -Raw
$hostSource = Get-Content -LiteralPath (Join-Path $repoRoot 'src\Plana.Companion.Native\GodotCompanionWindow.cs') -Raw
$checks = [ordered]@{
    NoTopBlackBar = $topRatio -lt 0.25
    NoBottomBlackBar = $bottomRatio -lt 0.25
    DoubleClickDispatchedOnPress = $rendererSource -match 'event\.pressed[\s\S]{0,200}event\.double_click'
    ClickUsesConfiguredInteraction = $hostSource -match 'InteractionBindings' -and $hostSource -notmatch 'type == "interaction"[\s\S]{0,500}CharacterGesture\.HeadPat'
    ContextEventHandled = $rendererSource -match 'send_event\("context"' -and $hostSource -match 'type == "context"'
}

$checks.GetEnumerator() | ForEach-Object { [pscustomobject]@{ Check=$_.Key; Passed=$_.Value } }
Write-Output ("TopBlackRatio={0:N3} BottomBlackRatio={1:N3}" -f $topRatio,$bottomRatio)
if ($checks.Values -contains $false) { exit 1 }
