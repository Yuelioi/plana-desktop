param(
    [string]$Output = "$PSScriptRoot\..\release\characters\scene",
    [string]$AuditReport = "$PSScriptRoot\..\artifacts\memorial-catalog-audit.json"
)

$ErrorActionPreference = 'Stop'
$repository = 'asdfdsa12/BA-Spine-Viewer-Asset'
$assetRoot = 'memorial/assets'
$headers = @{ 'User-Agent' = 'plana-desktop-catalog' }
$mappingUrl = 'https://gist.githubusercontent.com/Agent-0808/f1a52ffab7b7a8e50075b061463de60b/raw/2691b9320be9eda75a2be2d6508eafcb2cf56d8b/BA_SpineFilenames.md'

$mapping = @{}
foreach ($line in ((Invoke-WebRequest -UseBasicParsing $mappingUrl).Content -split "`n")) {
    if ($line -match '^\| `([^`]+_spr)` \|\s*([^|]+?)\s*\|') { $mapping[$Matches[1]] = $Matches[2].Trim() }
}
$commit = Invoke-RestMethod -Headers $headers "https://api.github.com/repos/$repository/commits/main"
$commitSha = $commit.sha
$treeSha = $commit.commit.tree.sha
$tree = Invoke-RestMethod -Headers $headers "https://api.github.com/repos/$repository/git/trees/${treeSha}?recursive=1"
if ($tree.truncated) { throw 'The upstream Git tree response was truncated.' }
$blobs = @{}
foreach ($item in $tree.tree | Where-Object { $_.type -eq 'blob' -and $_.path.StartsWith("$assetRoot/") }) { $blobs[$item.path] = $item }
$reports = Get-Content -Raw $AuditReport | ConvertFrom-Json
New-Item -ItemType Directory -Force $Output | Out-Null

function Pick-Animation($names, [string[]]$preferred, [string]$fallback) {
    foreach ($name in $preferred) { if ($name -in $names) { return $name } }
    return $fallback
}

foreach ($report in $reports | Sort-Object Base) {
    $base = $report.Base
    $stem = $base -replace '_home$',''
    $mappedKey = "${stem}_spr"
    $name = if ($mapping.ContainsKey($mappedKey)) { $mapping[$mappedKey] } else { (Get-Culture).TextInfo.ToTitleCase(($stem -replace '_',' ').ToLowerInvariant()) }
    $slug = ($stem -replace '[^A-Za-z0-9]+','-').Trim('-').ToLowerInvariant()
    $fileNames = @("$base.skel", "$base.atlas") + @($report.Pages)
    if ($fileNames.Count -gt 12) { throw "$base declares more than 12 assets." }
    $assets = foreach ($fileName in $fileNames) {
        $path = "$assetRoot/$fileName"
        $blob = $blobs[$path]
        if ($null -eq $blob) { throw "$base references a missing asset: $fileName" }
        [ordered]@{ path = $fileName; url = "https://raw.githubusercontent.com/$repository/$commitSha/$path"; gitBlobSha1 = $blob.sha }
    }
    $width = [double]$report.Bounds.width
    $height = [double]$report.Bounds.height
    $scale = [Math]::Round([Math]::Min(600.0 / $width, 820.0 / $height), 4)
    $x = [Math]::Round(320.0 - ([double]$report.Bounds.x + $width / 2.0) * $scale, 2)
    $y = [Math]::Round(450.0 + ([double]$report.Bounds.y + $height / 2.0) * $scale, 2)
    $names = @($report.Animations)
    $idle = Pick-Animation $names @('Idle_01','Idle','Start_Idle_01','Dummy') $names[0]
    $speaking = Pick-Animation $names @('Talk_01_M','Talk_01_A','Talk_01','Talk') $idle
    $gestures = [ordered]@{}
    $blink = Pick-Animation $names @('Eye_Close_01') ''
    $pat = Pick-Animation $names @('Pat_01_M','Pat_01_A','Pat_01') ''
    $look = Pick-Animation $names @('Look_01_M','Look_01_A','Look_01') ''
    if ($blink) { $gestures.Blink = $blink }; if ($pat) { $gestures.HeadPat = $pat }; if ($look) { $gestures.LookAtPointer = $look }
    $manifest = [ordered]@{
        schemaVersion = 1; id = "community.ba.memorial.$slug"; name = "$name (Memorial)"; version = '1.0.0'
        skeleton = "$base.skel"; atlas = "$base.atlas"
        layout = [ordered]@{ x = $x; y = $y; scale = $scale; hitPolygon = @(@{x=0.02;y=0.02},@{x=0.98;y=0.02},@{x=0.98;y=0.98},@{x=0.02;y=0.98}) }
        performance = [ordered]@{ idle = $idle; speaking = $speaking; emotions = [ordered]@{ Neutral = $idle; Happy = $idle; Worried = $idle }; gestures = $gestures }
    }
    $package = [ordered]@{
        schemaVersion = 1; category = 'scene'; sourceRevision = $commitSha; sourceTree = $treeSha
        motion = [ordered]@{ profile = 'full'; bones = $report.Bones; constraints = $report.Constraints }
        manifest = $manifest; assets = @($assets)
    }
    $package | ConvertTo-Json -Depth 12 | Set-Content -Encoding utf8 (Join-Path $Output "$slug.planacharacter")
}

[pscustomobject]@{ SourceRevision = $commitSha; SceneModels = $reports.Count; Output = [IO.Path]::GetFullPath($Output) }
