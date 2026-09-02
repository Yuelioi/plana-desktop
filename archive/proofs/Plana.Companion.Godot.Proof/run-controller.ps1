param([switch]$Smoke)

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'run-proof.ps1') -PrepareOnly
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$godot = Join-Path $repoRoot 'artifacts\proof-toolchain\godot-4.6.1\Godot_v4.6.1-stable_win64.exe'
$arguments = @('run', '--project', (Join-Path $PSScriptRoot 'Controller\Plana.GodotProof.Controller.csproj'), '--', '--godot', $godot, '--project', $PSScriptRoot)
if ($Smoke) { $arguments += '--smoke' }
dotnet @arguments
exit $LASTEXITCODE
