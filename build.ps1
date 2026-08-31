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
        -o "$PSScriptRoot\artifacts\win-x64"
    exit $LASTEXITCODE
}

dotnet build "$PSScriptRoot\Plana.Desktop.sln" -c Release
exit $LASTEXITCODE
