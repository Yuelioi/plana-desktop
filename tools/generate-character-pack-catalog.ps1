param(
    [string]$Output = "$PSScriptRoot\..\release\characters\static",
    [string]$AuditReport = "$PSScriptRoot\..\artifacts\character-catalog-audit.json"
)

$ErrorActionPreference = 'Stop'
$repository = 'asdfdsa12/BA-Spine-Viewer-Asset'
$assetRoot = 'new/assets'
$headers = @{ 'User-Agent' = 'plana-desktop-catalog' }
$mappingUrl = 'https://gist.githubusercontent.com/Agent-0808/f1a52ffab7b7a8e50075b061463de60b/raw/2691b9320be9eda75a2be2d6508eafcb2cf56d8b/BA_SpineFilenames.md'

$mapping = @{}
foreach ($line in ((Invoke-WebRequest -UseBasicParsing $mappingUrl).Content -split "`n")) {
    if ($line -match '^\| `([^`]+_spr)` \|\s*([^|]+?)\s*\|') { $mapping[$Matches[1]] = $Matches[2].Trim() }
}

$commit = Invoke-RestMethod -Headers $headers "https://api.github.com/repos/$repository/commits/main"
$commitSha = $commit.sha
$treeSha = $commit.commit.tree.sha
$rawRoot = "https://raw.githubusercontent.com/$repository/$commitSha/$assetRoot"
$tree = Invoke-RestMethod -Headers $headers "https://api.github.com/repos/$repository/git/trees/${treeSha}?recursive=1"
if ($tree.truncated) { throw 'The upstream Git tree response was truncated.' }

$assetFiles = $tree.tree | Where-Object { $_.type -eq 'blob' -and $_.path -match "^$([regex]::Escape($assetRoot))/([^/]+_spr)\.(skel|atlas|png)$" } | ForEach-Object {
    $match = [regex]::Match($_.path, '([^/]+_spr)\.(skel|atlas|png)$')
    [pscustomobject]@{ Base = $match.Groups[1].Value; Extension = $match.Groups[2].Value; Path = $_.path; Sha = $_.sha }
}
$models = $assetFiles | Group-Object Base | Where-Object {
    $extensions = @($_.Group.Extension)
    'skel' -in $extensions -and 'atlas' -in $extensions -and 'png' -in $extensions
} | Sort-Object Name
$audit = @{}
if (Test-Path -LiteralPath $AuditReport) {
    foreach ($item in Get-Content -Raw $AuditReport | ConvertFrom-Json) { $audit[$item.Slug] = $item }
}

New-Item -ItemType Directory -Force $Output | Out-Null

function Get-DisplayName([string]$base) {
    if ($mapping.ContainsKey($base)) { return $mapping[$base] }
    $plain = $base -replace '_spr$','' -replace '_',' '
    return (Get-Culture).TextInfo.ToTitleCase($plain.ToLowerInvariant())
}

foreach ($model in $models) {
    $base = $model.Name
    $name = Get-DisplayName $base
    $slug = ($base -replace '_spr$','' -replace '[^A-Za-z0-9]+','-').Trim('-').ToLowerInvariant()
    $metrics = $audit[$slug]
    $motionProfile = if ($metrics.Bones -ge 30 -and $metrics.IdleBoneTimelines -ge 10) { 'full' }
        elseif ($metrics.IdleBoneTimelines -gt 2) { 'limited' }
        else { 'static' }
    $assets = foreach ($extension in 'skel','atlas','png') {
        $asset = $model.Group | Where-Object Extension -eq $extension | Select-Object -First 1
        [ordered]@{ path = [IO.Path]::GetFileName($asset.Path); url = "$rawRoot/$([IO.Path]::GetFileName($asset.Path))"; gitBlobSha1 = $asset.Sha }
    }
    $manifest = [ordered]@{
        schemaVersion = 1; id = "community.ba.$slug"; name = $name; version = '1.0.0'
        skeleton = "$base.skel"; atlas = "$base.atlas"
        layout = [ordered]@{
            x = 320; y = 835; scale = 0.30
            hitPolygon = @(
                @{ x = 0.14; y = 0.22 }, @{ x = 0.82; y = 0.22 }, @{ x = 0.90; y = 0.64 },
                @{ x = 0.82; y = 1.0 }, @{ x = 0.08; y = 1.0 }, @{ x = 0.04; y = 0.62 }
            )
        }
        performance = [ordered]@{
            idle = 'Idle_01'; speaking = '02'
            emotions = [ordered]@{ Neutral = '00'; Happy = '03'; Excited = '06'; Surprised = '04'; Sad = '07'; Worried = '05'; Angry = '05'; Affectionate = '04'; Shy = '04'; Dizzy = '08' }
            gestures = [ordered]@{ Blink = 'Eye_Close_01' }
        }
    }
    $package = [ordered]@{
        schemaVersion = 1; category = 'static'; sourceRevision = $commitSha; sourceTree = $treeSha
        motion = [ordered]@{ profile = $motionProfile; bones = $metrics.Bones; constraints = $metrics.Constraints; idleBoneTimelines = $metrics.IdleBoneTimelines }
        manifest = $manifest; assets = @($assets)
    }
    $package | ConvertTo-Json -Depth 12 | Set-Content -Encoding utf8 (Join-Path $Output "$slug.planacharacter")
}

[pscustomobject]@{ SourceRevision = $commitSha; SourceTree = $treeSha; CompleteModels = $models.Count; MappedNames = @($models | Where-Object { $mapping.ContainsKey($_.Name) }).Count; Output = [IO.Path]::GetFullPath($Output) }
