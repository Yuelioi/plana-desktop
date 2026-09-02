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
$testPackage = Get-ChildItem (Join-Path $repositoryRoot 'artifacts\control-center') -Directory -Recurse |
    Where-Object Name -like '*_Test' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $testPackage) { throw 'Control Center test package was not produced.' }

$installer = Join-Path $releaseDirectory 'installer'
New-Item -ItemType Directory -Path (Join-Path $installer 'Companion'),(Join-Path $installer 'ControlCenter') -Force | Out-Null
Copy-Item (Join-Path $repositoryRoot 'artifacts\native-win-x64\*') (Join-Path $installer 'Companion') -Recurse -Force
Copy-Item (Join-Path $testPackage.FullName '*') (Join-Path $installer 'ControlCenter') -Recurse -Force
Copy-Item (Join-Path $repositoryRoot 'release\Install.ps1') $installer -Force
Compress-Archive -Path (Join-Path $installer '*') -DestinationPath (Join-Path $releaseDirectory 'Plana-Desktop-x64-Installer.zip') -Force
Compress-Archive -Path (Join-Path $repositoryRoot 'artifacts\plugin-plana-random-images-win-x64\*') -DestinationPath (Join-Path $releaseDirectory 'plugin-plana-random-images-win-x64.zip') -Force
Compress-Archive -Path (Join-Path $repositoryRoot 'release\characters\*') -DestinationPath (Join-Path $releaseDirectory 'Character-Packs.zip') -Force

& (Join-Path $PSScriptRoot 'check-release-layout.ps1') $releaseDirectory
