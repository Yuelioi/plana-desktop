param(
    [Parameter(Mandatory = $true)]
    [string]$ReleaseDirectory
)

$ErrorActionPreference = 'Stop'
$expected = @(
    'Character-Packs.zip',
    'Plana-Desktop-x64-Setup.exe',
    'plugin-plana-random-images-win-x64.zip'
)
$actual = Get-ChildItem -LiteralPath $ReleaseDirectory -File | Select-Object -ExpandProperty Name | Sort-Object
if (Compare-Object $expected $actual) {
    throw "Release must contain exactly: $($expected -join ', '). Actual: $($actual -join ', ')."
}

if ((Get-Item (Join-Path $ReleaseDirectory 'Plana-Desktop-x64-Setup.exe')).Length -lt 1MB) {
    throw 'The Windows installer is unexpectedly small.'
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
function Assert-ZipEntry([string]$zipPath, [string]$pattern) {
    $archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        if (-not ($archive.Entries.FullName -like $pattern)) {
            throw "$(Split-Path $zipPath -Leaf) is missing $pattern."
        }
    }
    finally { $archive.Dispose() }
}

function Assert-ZipEntryAbsent([string]$zipPath, [string]$pattern) {
    $archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        if ($archive.Entries.FullName -like $pattern) {
            throw "$(Split-Path $zipPath -Leaf) must not contain $pattern."
        }
    }
    finally { $archive.Dispose() }
}

Assert-ZipEntry (Join-Path $ReleaseDirectory 'plugin-plana-random-images-win-x64.zip') 'plugin.json'
Assert-ZipEntry (Join-Path $ReleaseDirectory 'plugin-plana-random-images-win-x64.zip') 'Plana.ExamplePlugin.exe'

Write-Host 'Release layout is valid: installer, Plugin, and Character Packs only.'
