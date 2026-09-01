$ErrorActionPreference = 'Stop'
$settingsPath = Join-Path $env:LOCALAPPDATA 'PlanaDesktop\settings.json'
$hostProcess = Get-Process -Name Plana.Desktop -ErrorAction Stop |
    Where-Object Path -like '*artifacts\native-win-x64\Plana.Desktop.exe' |
    Select-Object -First 1
if (-not $hostProcess) { throw 'Published native Plana.Desktop.exe is not running.' }

$original = [System.IO.File]::ReadAllText($settingsPath)
$settings = [System.Text.Json.Nodes.JsonNode]::Parse($original)
$originalScale = $settings['Scale'].GetValue[double]()
$probeScale = if ($originalScale -ge 1.85) { 1.8 } else { [Math]::Min(2.0, $originalScale + 0.1) }

function Write-Settings([string]$json) {
    $temporary = Join-Path (Split-Path $settingsPath) ("settings-thread-probe-{0}.tmp" -f [Guid]::NewGuid().ToString('N'))
    [System.IO.File]::WriteAllText($temporary, $json)
    [System.IO.File]::Move($temporary, $settingsPath, $true)
}

try {
    $settings['Scale'] = $probeScale
    Write-Settings ($settings.ToJsonString([System.Text.Json.JsonSerializerOptions]@{ WriteIndented = $true }))
    Start-Sleep -Seconds 2
    $survivedProbe = [bool](Get-Process -Id $hostProcess.Id -ErrorAction SilentlyContinue)
}
finally {
    Write-Settings $original
    Start-Sleep -Seconds 2
}
$survivedRestore = [bool](Get-Process -Id $hostProcess.Id -ErrorAction SilentlyContinue)

[pscustomobject]@{
    Check = 'HostSurvivesScaleRefresh'
    Passed = $survivedProbe -and $survivedRestore
    OriginalScale = $originalScale
    ProbeScale = $probeScale
    HostProcessId = $hostProcess.Id
}
if (-not $survivedProbe -or -not $survivedRestore) { exit 1 }
