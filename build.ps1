param(
    [switch]$Publish
)

$ErrorActionPreference = 'Stop'
Remove-Item Env:TargetPath -ErrorAction SilentlyContinue

& "$PSScriptRoot\tools\check-control-center-ui.ps1"
if (-not $?) { exit 1 }

dotnet test "$PSScriptRoot\tests\Plana.Core.Tests\Plana.Core.Tests.csproj" -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet test "$PSScriptRoot\tests\Plana.TransientUI.Tests\Plana.TransientUI.Tests.csproj" -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($Publish) {
    dotnet publish "$PSScriptRoot\src\Plana.Companion.Native\Plana.Companion.Native.csproj" `
        -c Release -r win-x64 --self-contained false `
        -o "$PSScriptRoot\artifacts\native-win-x64"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $godotToolRoot = Join-Path $PSScriptRoot 'artifacts\proof-toolchain'
    $godotExecutable = Join-Path $godotToolRoot 'godot-4.6.1\Godot_v4.6.1-stable_win64.exe'
    $godotConsoleExecutable = Join-Path $godotToolRoot 'godot-4.6.1\Godot_v4.6.1-stable_win64_console.exe'
    $godotExtension = Join-Path $godotToolRoot 'spine-godot-4.2-4.6.1\bin'
    if (-not (Test-Path -LiteralPath $godotExecutable) -or -not (Test-Path -LiteralPath $godotExtension)) {
        throw 'Godot 4.6.1 + spine-godot 4.2 proof toolchain is required under artifacts/proof-toolchain for publishing.'
    }
    $godotOutput = Join-Path $PSScriptRoot 'artifacts\native-win-x64\Godot'
    $rendererOutput = Join-Path $PSScriptRoot 'artifacts\native-win-x64\GodotRenderer'
    New-Item -ItemType Directory -Path $godotOutput,$rendererOutput,(Join-Path $rendererOutput 'bin'),(Join-Path $rendererOutput 'runtime-assets') -Force | Out-Null
    Remove-Item -LiteralPath (Join-Path $rendererOutput 'plana-proof.gd'),(Join-Path $rendererOutput 'plana-proof.gd.uid') -Force -ErrorAction SilentlyContinue
    Copy-Item -LiteralPath $godotExecutable -Destination (Join-Path $godotOutput 'Godot.exe') -Force
    Copy-Item -LiteralPath $godotConsoleExecutable -Destination (Join-Path $godotOutput 'Godot.console.exe') -Force
    Copy-Item -LiteralPath (Join-Path $godotExtension 'spine_godot_extension.gdextension') -Destination (Join-Path $rendererOutput 'bin') -Force
    Copy-Item -LiteralPath (Join-Path $godotExtension 'windows') -Destination (Join-Path $rendererOutput 'bin') -Recurse -Force
    $rendererSource = Join-Path $PSScriptRoot 'src\Plana.Companion.Godot.Renderer'
    Copy-Item -LiteralPath (Join-Path $rendererSource 'project.godot'),(Join-Path $rendererSource 'main.tscn'),(Join-Path $rendererSource 'plana-data.tres'),(Join-Path $rendererSource 'renderer.gd'),(Join-Path $rendererSource 'renderer.gd.uid') -Destination $rendererOutput -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src\Plana.Brand\AppIcon.png') -Destination $rendererOutput -Force
    $modelSource = Join-Path $PSScriptRoot 'src\Plana.Companion.Godot.Renderer\character-packs\plana'
    Copy-Item -LiteralPath (Join-Path $modelSource 'NP0035_spr.skel'),(Join-Path $modelSource 'NP0035_spr.atlas'),(Join-Path $modelSource 'NP0035_spr.png') -Destination (Join-Path $rendererOutput 'runtime-assets') -Force
    & (Join-Path $godotOutput 'Godot.console.exe') --headless --editor --path $rendererOutput --quit-after 30
    if ($LASTEXITCODE -ne 0) { throw "Godot renderer import failed with exit code $LASTEXITCODE." }

    dotnet publish "$PSScriptRoot\src\Plana.ControlCenter\Plana.ControlCenter.csproj" `
        -c Release -p:Platform=x64 -p:GenerateAppxPackageOnBuild=true `
        -p:AppxPackageDir="$PSScriptRoot\artifacts\control-center\"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet publish "$PSScriptRoot\src\Plana.PluginHost\Plana.PluginHost.csproj" `
        -c Release -r win-x64 --self-contained false `
        -o "$PSScriptRoot\artifacts\native-win-x64\PluginHost"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet publish "$PSScriptRoot\examples\Plana.ExamplePlugin\Plana.ExamplePlugin.csproj" `
        -c Release -r win-x64 --self-contained false `
        -o "$PSScriptRoot\artifacts\native-win-x64\SamplePlugins\hello"
    exit $LASTEXITCODE
}

dotnet build "$PSScriptRoot\Plana.Desktop.sln" -c Release
exit $LASTEXITCODE
