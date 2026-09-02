param([switch]$Editor, [switch]$Capture, [switch]$PrepareOnly, [string]$Animation = 'Idle_01')

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$toolRoot = Join-Path $repoRoot 'artifacts\proof-toolchain'
$godotName = if ($Capture) { 'Godot_v4.6.1-stable_win64_console.exe' } else { 'Godot_v4.6.1-stable_win64.exe' }
$godot = Get-ChildItem -LiteralPath (Join-Path $toolRoot 'godot-4.6.1') -Filter $godotName | Select-Object -First 1
if (-not $godot) { throw 'Godot 4.6.1 proof toolchain is missing under artifacts/proof-toolchain.' }

$extensionRoot = Join-Path $toolRoot 'spine-godot-4.2-4.6.1\bin'
$proofBin = Join-Path $PSScriptRoot 'bin'
$assetDir = Join-Path $PSScriptRoot 'runtime-assets'
New-Item -ItemType Directory -Path $proofBin,$assetDir -Force | Out-Null
if (-not (Test-Path -LiteralPath (Join-Path $proofBin 'spine_godot_extension.gdextension'))) {
    Copy-Item -LiteralPath (Join-Path $extensionRoot 'spine_godot_extension.gdextension') -Destination $proofBin
}
if (-not (Test-Path -LiteralPath (Join-Path $proofBin 'windows\libspine_godot.windows.editor.x86_64.dll'))) {
    Copy-Item -LiteralPath (Join-Path $extensionRoot 'windows') -Destination $proofBin -Recurse
}

$modelRoot = Join-Path $repoRoot 'src\Plana.Desktop\Renderer\spine\plana'
Copy-Item -LiteralPath (Join-Path $modelRoot 'NP0035_spr.skel'),(Join-Path $modelRoot 'NP0035_spr.atlas'),(Join-Path $modelRoot 'NP0035_spr.png') -Destination $assetDir -Force

if ($PrepareOnly) { exit 0 }

$arguments = @('--path', $PSScriptRoot)
if ($Editor) { $arguments += '--editor' }
if ($Capture) {
    $safeAnimation = $Animation -replace '[^A-Za-z0-9_-]', '-'
    $capturePath = (Join-Path $repoRoot "artifacts\godot-proof-$safeAnimation.png").Replace('\', '/')
    $arguments += @('--', "capture=$capturePath", "animation=$Animation")
}
& $godot.FullName @arguments
exit $LASTEXITCODE
