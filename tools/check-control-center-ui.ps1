$ErrorActionPreference = 'Stop'

$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$uiRoot = Join-Path $root 'src\Plana.ControlCenter'
$xamlFiles = Get-ChildItem -LiteralPath $uiRoot -Recurse -Filter '*.xaml' |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
$violations = [System.Collections.Generic.List[string]]::new()

foreach ($file in $xamlFiles) {
    $text = Get-Content -LiteralPath $file.FullName -Raw
    $relative = [System.IO.Path]::GetRelativePath($root, $file.FullName)
    if ($text -match '<FontIcon\b') { $violations.Add("$relative uses FontIcon; use SymbolIcon or a reviewed SVG PathIcon.") }
    if ($text -match '\bGlyph\s*=') { $violations.Add("$relative contains a raw glyph.") }
    if ($text -match '<SymbolIcon[^>]*\b(FontSize|Width|Height)\s*=') { $violations.Add("$relative sizes a SymbolIcon locally; use CommandIconStyle or the native SymbolIcon size.") }
    if ($text -match '<SymbolIcon[^>]*CommandIconStyle') { $violations.Add("$relative applies CommandIconStyle directly; wrap the SymbolIcon in a styled Viewbox.") }
}

foreach ($file in Get-ChildItem -LiteralPath $uiRoot -Recurse -Filter '*.cs' | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }) {
    $text = Get-Content -LiteralPath $file.FullName -Raw
    if ($text -match '\bnew\s+FontIcon\b') {
        $relative = [System.IO.Path]::GetRelativePath($root, $file.FullName)
        $violations.Add("$relative creates FontIcon dynamically; use SymbolIcon or a reviewed SVG PathIcon.")
    }
}

$resourcePath = Join-Path $uiRoot 'Themes\PlanaControls.xaml'
if (-not (Test-Path -LiteralPath $resourcePath)) { $violations.Add('Themes/PlanaControls.xaml is missing.') }

if ($violations.Count -gt 0) {
    $violations | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Output "Control Center UI contract passed ($($xamlFiles.Count) XAML files)."
