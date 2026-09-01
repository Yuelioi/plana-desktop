$ErrorActionPreference = 'Stop'
$settingsPath = Join-Path $env:LOCALAPPDATA 'PlanaDesktop\settings.json'
$hostProcess = Get-Process -Name Plana.Desktop -ErrorAction Stop |
    Where-Object Path -like '*artifacts\native-win-x64\Plana.Desktop.exe' |
    Select-Object -First 1
$original = [IO.File]::ReadAllText($settingsPath)
$originalJson = [Text.Json.Nodes.JsonNode]::Parse($original)
$originalId = $originalJson['SelectedCharacterPackId']?.GetValue[string]() ?? 'builtin.plana'

function Write-Selection([string]$id) {
    $json = [Text.Json.Nodes.JsonNode]::Parse([IO.File]::ReadAllText($settingsPath))
    $json['SelectedCharacterPackId'] = $id
    $temporary = Join-Path (Split-Path $settingsPath) ('settings-character-probe-' + [guid]::NewGuid().ToString('N') + '.tmp')
    [IO.File]::WriteAllText($temporary, $json.ToJsonString([Text.Json.JsonSerializerOptions]@{ WriteIndented = $true }))
    [IO.File]::Move($temporary, $settingsPath, $true)
}

function Wait-Renderer([int]$previousPid, [string]$pathFragment) {
    $deadline = [DateTime]::UtcNow.AddSeconds(12)
    do {
        Start-Sleep -Milliseconds 100
        $process = Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'Godot.exe' -and $_.ParentProcessId -eq $hostProcess.Id -and $_.ProcessId -ne $previousPid -and $_.CommandLine -like "*$pathFragment*" } | Select-Object -First 1
    } while (-not $process -and [DateTime]::UtcNow -lt $deadline)
    if (-not $process) { throw "Renderer did not switch to $pathFragment" }
    Start-Sleep -Milliseconds 1500
    if (-not (Get-Process -Id $hostProcess.Id -ErrorAction SilentlyContinue)) { throw 'Host exited during Character Pack switch.' }
    $children = @(Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'Godot.exe' -and $_.ParentProcessId -eq $hostProcess.Id })
    if ($children.Count -ne 1) { throw "Expected one Renderer child, found $($children.Count)." }
    return $process
}

$initialRenderer = Get-Process -Name Godot -ErrorAction Stop | Select-Object -First 1
try {
    Write-Selection 'builtin.plana'
    $plana = Wait-Renderer $initialRenderer.Id 'CharacterPacks\plana\NP0035_spr.skel'
    Write-Selection 'community.arona'
    $arona = Wait-Renderer $plana.ProcessId 'characters\community.arona\arona_spr.skel'
    $passed = [bool](Get-Process -Id $hostProcess.Id -ErrorAction SilentlyContinue)
}
finally {
    $restore = Join-Path (Split-Path $settingsPath) ('settings-character-restore-' + [guid]::NewGuid().ToString('N') + '.tmp')
    [IO.File]::WriteAllText($restore, $original)
    [IO.File]::Move($restore, $settingsPath, $true)
    Start-Sleep -Seconds 2
}

[pscustomobject]@{
    Check = 'CharacterPackHotSwitch'
    Passed = $passed
    PlanaRendererPid = $plana.ProcessId
    AronaRendererPid = $arona.ProcessId
    RestoredSelection = $originalId
}
if (-not $passed) { exit 1 }
