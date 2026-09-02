param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$installDirectory = Join-Path $env:LOCALAPPDATA 'Programs\Plana Desktop'
$companionSource = Join-Path $PSScriptRoot 'Companion'
$controlCenterInstaller = Join-Path $PSScriptRoot 'ControlCenter\Install.ps1'

if (-not (Test-Path -LiteralPath (Join-Path $companionSource 'Plana.Desktop.exe'))) {
    throw 'The Companion files are missing from this installer package.'
}
if (-not (Test-Path -LiteralPath $controlCenterInstaller)) {
    throw 'The Control Center installer is missing from this installer package.'
}

New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
Copy-Item -Path (Join-Path $companionSource '*') -Destination $installDirectory -Recurse -Force

& $controlCenterInstaller -SkipLoggingTelemetry -Force:$Force

$shell = New-Object -ComObject WScript.Shell
$shortcutPath = Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs\Plana Desktop.lnk'
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $installDirectory 'Plana.Desktop.exe'
$shortcut.WorkingDirectory = $installDirectory
$shortcut.Save()

Start-Process -FilePath (Join-Path $installDirectory 'Plana.Desktop.exe')
Write-Host "Plana Desktop was installed to $installDirectory"
