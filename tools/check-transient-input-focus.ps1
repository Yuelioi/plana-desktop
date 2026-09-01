param([int]$Iterations = 12, [switch]$SingleKey, [switch]$HoldHotkeyModifiers, [switch]$SkipDock, [switch]$OutsideClose)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class TransientInputProbe {
    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr state);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr state);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out Rect bounds);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extraInfo);
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extraInfo);
    public static void PressHotkey() {
        keybd_event(0x11, 0, 0, UIntPtr.Zero); keybd_event(0x12, 0, 0, UIntPtr.Zero); keybd_event(0x20, 0, 0, UIntPtr.Zero);
        keybd_event(0x20, 0, 2, UIntPtr.Zero); keybd_event(0x12, 0, 2, UIntPtr.Zero); keybd_event(0x11, 0, 2, UIntPtr.Zero);
    }
    public static void PressHotkeyHoldingModifiers(int milliseconds) {
        keybd_event(0x11, 0, 0, UIntPtr.Zero); keybd_event(0x12, 0, 0, UIntPtr.Zero); keybd_event(0x20, 0, 0, UIntPtr.Zero);
        keybd_event(0x20, 0, 2, UIntPtr.Zero); System.Threading.Thread.Sleep(milliseconds);
        keybd_event(0x12, 0, 2, UIntPtr.Zero); keybd_event(0x11, 0, 2, UIntPtr.Zero);
    }
    public static void PressT() { keybd_event(0x54, 0, 0, UIntPtr.Zero); keybd_event(0x54, 0, 2, UIntPtr.Zero); }
}
'@

function Find-HostWindow([uint32]$processId, [scriptblock]$predicate) {
    $script:matched = [IntPtr]::Zero
    [TransientInputProbe]::EnumWindows({
        param($window, $state)
        [uint32]$owner = 0
        [TransientInputProbe]::GetWindowThreadProcessId($window, [ref]$owner) | Out-Null
        $bounds = [TransientInputProbe+Rect]::new()
        if ($owner -eq $processId -and [TransientInputProbe]::IsWindowVisible($window) -and
            [TransientInputProbe]::GetWindowRect($window, [ref]$bounds) -and (& $predicate $bounds)) {
            $script:matched = $window
            return $false
        }
        return $true
    }, [IntPtr]::Zero) | Out-Null
    return $matched
}

function Find-Edit([IntPtr]$window) {
    try {
        $root = [System.Windows.Automation.AutomationElement]::FromHandle($window)
        $condition = [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Edit)
        return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    }
    catch [System.Windows.Automation.ElementNotAvailableException] { return $null }
}

function Has-VisiblePlaceholder([IntPtr]$window) {
    try { $root = [System.Windows.Automation.AutomationElement]::FromHandle($window) }
    catch [System.Windows.Automation.ElementNotAvailableException] { return $false }
    $texts = $root.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Text))
    foreach ($text in $texts) {
        if ($text.Current.Name -in @('搜索动作','Search actions','和普拉娜说点什么…','Ask Plana…')) { return $true }
    }
    return $false
}

function Paste-And-Read($edit, [string]$text, [bool]$skipClick = $false) {
    $pattern = $edit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    if (-not ($SingleKey -and $skipClick)) { $pattern.SetValue('') }
    if (-not $skipClick) {
        $bounds = $edit.Current.BoundingRectangle
        [TransientInputProbe]::SetCursorPos([int]($bounds.Left + $bounds.Width / 2), [int]($bounds.Top + $bounds.Height / 2)) | Out-Null
        [TransientInputProbe]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
        [TransientInputProbe]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 40
    }
    if ($SingleKey) {
        [TransientInputProbe]::PressT()
    }
    else {
        [System.Windows.Forms.Clipboard]::SetText($text)
        (New-Object -ComObject WScript.Shell).SendKeys('^v')
    }
    Start-Sleep -Milliseconds 80
    return $pattern.Current.Value
}

$hostProcess = Get-Process -Name Plana.Desktop -ErrorAction Stop |
    Where-Object Path -like '*artifacts\native-win-x64\Plana.Desktop.exe' |
    Select-Object -First 1
if (-not $hostProcess) { throw 'Published Host is not running.' }
$clipboard = if ([System.Windows.Forms.Clipboard]::ContainsText()) { [System.Windows.Forms.Clipboard]::GetText() } else { $null }
$quickFailures = 0
$dockFailures = 0
$placeholderFailures = 0
$quickMissing = 0
$quickValueFailures = 0
$dockMissing = 0
$dockValueFailures = 0
$outsideCloseFailures = 0
$quickForegroundFailures = 0
$quickKeyboardFocusFailures = 0
$lastDockEdit = $null
$outsideProcess = if ($OutsideClose) { Start-Process notepad.exe -PassThru } else { $null }
try {
    for ($index = 0; $index -lt $Iterations; $index++) {
        if ($HoldHotkeyModifiers) { [TransientInputProbe]::PressHotkeyHoldingModifiers(150) } else { [TransientInputProbe]::PressHotkey() }
        Start-Sleep -Milliseconds 120
        $quick = [TransientInputProbe]::GetForegroundWindow()
        [uint32]$quickOwner = 0
        [TransientInputProbe]::GetWindowThreadProcessId($quick, [ref]$quickOwner) | Out-Null
        $quickBounds = [TransientInputProbe+Rect]::new()
        if ($quickOwner -ne [uint32]$hostProcess.Id -or -not [TransientInputProbe]::GetWindowRect($quick, [ref]$quickBounds) -or
            ($quickBounds.Right - $quickBounds.Left) -ne 680 -or ($quickBounds.Bottom - $quickBounds.Top) -lt 132) {
            $quick = [IntPtr]::Zero
        }
        if ($quick -eq [IntPtr]::Zero) { $quickFailures++; $quickMissing++; continue }
        if ($SingleKey -and (Has-VisiblePlaceholder $quick)) { $placeholderFailures++ }
        $edit = Find-Edit $quick
        if ([TransientInputProbe]::GetForegroundWindow() -ne $quick) { $quickForegroundFailures++ }
        if ($edit -and -not $edit.Current.HasKeyboardFocus) { $quickKeyboardFocusFailures++ }
        $expected = if ($SingleKey) { 't' } else { "quick-$index" }
        if (-not $edit -or (Paste-And-Read $edit $expected $SingleKey) -ne $expected) { $quickFailures++; $quickValueFailures++ }
        if ($OutsideClose) {
            $closeDeadline = [DateTime]::UtcNow.AddMilliseconds(500)
            do {
                (New-Object -ComObject WScript.Shell).AppActivate($outsideProcess.Id) | Out-Null
                Start-Sleep -Milliseconds 10
                $stillVisible = Find-HostWindow ([uint32]$hostProcess.Id) { param($bounds) ($bounds.Right - $bounds.Left) -eq 680 -and ($bounds.Bottom - $bounds.Top) -ge 132 }
            } while ($stillVisible -ne [IntPtr]::Zero -and [DateTime]::UtcNow -lt $closeDeadline)
            if ($stillVisible -ne [IntPtr]::Zero) { $outsideCloseFailures++ }
        }
        else {
            (New-Object -ComObject WScript.Shell).SendKeys('{ESC}')
        }
        Start-Sleep -Milliseconds 80
    }

    if (-not $SkipDock) {
        $renderer = Get-Process -Name Godot -ErrorAction Stop | Select-Object -First 1
        $rendererWindow = Find-HostWindow ([uint32]$renderer.Id) { param($bounds) ($bounds.Right - $bounds.Left) -gt 200 -and ($bounds.Bottom - $bounds.Top) -gt 300 }
        $rendererBounds = [TransientInputProbe+Rect]::new()
        [TransientInputProbe]::GetWindowRect($rendererWindow, [ref]$rendererBounds) | Out-Null
        for ($index = 0; $index -lt $Iterations; $index++) {
        [TransientInputProbe]::SetCursorPos($rendererBounds.Left + 100, $rendererBounds.Bottom - 40) | Out-Null
        Start-Sleep -Milliseconds 140
        $dock = Find-HostWindow ([uint32]$hostProcess.Id) { param($bounds) ($bounds.Bottom - $bounds.Top) -in 52,82 }
        if ($dock -eq [IntPtr]::Zero) { $dockFailures++; $dockMissing++; continue }
        $edit = Find-Edit $dock
        $lastDockEdit = $edit
        $expected = if ($SingleKey) { 't' } else { "dock-$index" }
        if (-not $edit) { $dockFailures++; $dockMissing++; continue }
        if ($SingleKey) {
            $bounds = $edit.Current.BoundingRectangle
            [TransientInputProbe]::SetCursorPos([int]($bounds.Left + $bounds.Width / 2), [int]($bounds.Top + $bounds.Height / 2)) | Out-Null
            [TransientInputProbe]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
            [TransientInputProbe]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
            Start-Sleep -Milliseconds 40
            if (Has-VisiblePlaceholder $dock) { $placeholderFailures++ }
            [TransientInputProbe]::PressT()
            Start-Sleep -Milliseconds 80
        }
        elseif ((Paste-And-Read $edit $expected) -ne $expected) { $dockFailures++; $dockValueFailures++ }
        }
    }
}
finally {
    if ($lastDockEdit) {
        $lastDockEdit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('')
    }
    if ($outsideProcess) { Stop-Process -Id $outsideProcess.Id -Force -ErrorAction SilentlyContinue }
    if ($null -eq $clipboard) { [System.Windows.Forms.Clipboard]::Clear() } else { [System.Windows.Forms.Clipboard]::SetText($clipboard) }
}

[pscustomobject]@{
    Check = 'TransientTextInputFocus'
    Passed = $quickFailures -eq 0 -and $dockFailures -eq 0 -and $placeholderFailures -eq 0 -and $outsideCloseFailures -eq 0
    Iterations = $Iterations
    QuickFailures = $quickFailures
    DockFailures = $dockFailures
    FocusedPlaceholderFailures = $placeholderFailures
    QuickMissing = $quickMissing
    QuickValueFailures = $quickValueFailures
    DockMissing = $dockMissing
    DockValueFailures = $dockValueFailures
    OutsideCloseFailures = $outsideCloseFailures
    QuickForegroundFailures = $quickForegroundFailures
    QuickKeyboardFocusFailures = $quickKeyboardFocusFailures
}
if ($quickFailures -gt 0 -or $dockFailures -gt 0 -or $placeholderFailures -gt 0 -or $outsideCloseFailures -gt 0) { exit 1 }
