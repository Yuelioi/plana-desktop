param(
    [string]$ReleaseDirectory = (Join-Path $PSScriptRoot '..\release-output')
)

$ErrorActionPreference = 'Stop'
$releaseDirectory = [IO.Path]::GetFullPath($ReleaseDirectory)
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $releaseDirectory.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Release output must stay inside the repository.'
}

New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
Remove-Item -LiteralPath (Join-Path $releaseDirectory 'Plana-Desktop-x64-Installer.zip') -Force -ErrorAction SilentlyContinue
$testPackage = Get-ChildItem (Join-Path $repositoryRoot 'artifacts\control-center') -Directory -Recurse |
    Where-Object Name -like '*_Test' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $testPackage) { throw 'Control Center test package was not produced.' }

$setupPath = Join-Path $releaseDirectory 'Plana-Desktop-x64-Setup.exe'
$makensis = Get-Command 'makensis.exe' -ErrorAction SilentlyContinue
if (-not $makensis) { throw 'NSIS (makensis.exe) is required to build the Windows installer.' }
& $makensis.Source `
    "/DREPOSITORY_ROOT=$repositoryRoot" `
    "/DCONTROL_CENTER_PACKAGE=$($testPackage.FullName)" `
    "/DOUTPUT_FILE=$setupPath" `
    (Join-Path $repositoryRoot 'release\Plana-Desktop.nsi')
if ($LASTEXITCODE -ne 0) { throw "NSIS failed with exit code $LASTEXITCODE." }
Compress-Archive -Path (Join-Path $repositoryRoot 'artifacts\plugin-plana-random-images-win-x64\*') -DestinationPath (Join-Path $releaseDirectory 'plugin-plana-random-images-win-x64.zip') -Force
Compress-Archive -Path (Join-Path $repositoryRoot 'release\characters\*') -DestinationPath (Join-Path $releaseDirectory 'Character-Packs.zip') -Force

& (Join-Path $PSScriptRoot 'check-release-layout.ps1') $releaseDirectory
