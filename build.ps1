param(
    [switch]$Publish
)

$ErrorActionPreference = 'Stop'
Remove-Item Env:TargetPath -ErrorAction SilentlyContinue

dotnet test "$PSScriptRoot\tests\Plana.Core.Tests\Plana.Core.Tests.csproj" -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($Publish) {
    dotnet publish "$PSScriptRoot\src\Plana.Desktop\Plana.Desktop.csproj" `
        -c Release -r win-x64 --self-contained false `
        -o "$PSScriptRoot\artifacts\legacy-win-x64"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet publish "$PSScriptRoot\src\Plana.Companion.Native\Plana.Companion.Native.csproj" `
        -c Release -r win-x64 --self-contained false `
        -o "$PSScriptRoot\artifacts\native-win-x64"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

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
