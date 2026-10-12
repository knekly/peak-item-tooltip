param(
    [string]$GameVersion = "2.6.b",
    [int]$BatchSize = 40
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$defaultsPath = Join-Path $repoRoot "assets/default-descriptions.json"
$manifestPath = Join-Path $repoRoot "assets/wiki-page-manifest.json"
$api = "https://peak.wiki.gg/api.php"
$userAgent = "PEAK-Item-Tooltip-Wiki-Indexer/1.0 (open-source mod research)"

$defaults = Get-Content -LiteralPath $defaultsPath -Raw | ConvertFrom-Json -AsHashtable
$keys = @($defaults.items.Keys | Sort-Object)

# Wiki display/page names that intentionally differ from the game's internal keys.
$aliases = @{
    "AMULET_CLONE" = "Scout's Generosity"
    "AMULET_DOUBLEJUMP" = "Scout's Initiative"
    "AMULET_HEALING" = "Scout's Tenacity"
    "AMULET_INFINITESTAM" = "Scout's Ambition"
    "Fannypack" = "Fanny Pack"
    "Frisbee" = "Flying Disc"
    "Bird" = "Cooked Bird"
    "Coconut Half" = "Half-Coconut"
    "EARLYWORM" = "The Early Worm"
    "FrogLegs" = "Frog Legs"
    "anti-rope cannon" = "Anti-Rope Cannon"
    "anti-rope spool" = "Anti-Rope Spool"
    "RitualDagger" = "Ritual Dagger"
    "rope cannon" = "Rope Cannon"
    "rope spool" = "Rope Spool"
    "ScoutsHonor" = "Scout's Honor"
    "SNOWBALL" = "Snowball"
    "THEBOOKOFBONES" = "The Book of Bones"
    "VOIDLAUNCHER" = "Anti-Zooka"
}

# Some game keys have no dedicated item page. Keep a cited secondary page or variant set
# without pretending that a redirect to an unrelated item is a direct match.
$secondarySources = @{
    "AUTOPARACHUTE" = @([ordered]@{ title = "Unused content"; revision = 11407; section = "Auto-Parachute" })
    "Berrynana Peel" = @(
        [ordered]@{ title = "Blue Berrynana Peel"; revision = 11170; section = "Blue Berrynana Peel" }
        [ordered]@{ title = "Brown Berrynana Peel"; revision = 11313; section = "Brown Berrynana Peel" }
        [ordered]@{ title = "Pink Berrynana Peel"; revision = 11168; section = "Pink Berrynana Peel" }
        [ordered]@{ title = "Yellow Berrynana Peel"; revision = 11240; section = "Yellow Berrynana Peel" }
    )
    "Climbing Chalk" = @([ordered]@{ title = "Unused content"; revision = 11407; section = "Climbing Chalk" })
    "Green Clusterberry" = @([ordered]@{ title = "Unused content"; revision = 11407; section = "Green Clusterberry" })
    "SCOUTMASTERSOUL" = @(
        [ordered]@{ title = "Nadir"; revision = 11745; section = "Scoutmaster's Soul" }
        [ordered]@{ title = "Scoutmaster Myres"; revision = 11913; section = "Gameplay" }
        [ordered]@{ title = "Scoutmaster Myres"; revision = 11913; section = "Lore" }
    )
    "Skull" = @([ordered]@{ title = "Unused content"; revision = 11407; section = "Skull" })
    "Megaphone" = @([ordered]@{ title = "Unused content"; revision = 11407; section = "Megaphone" })
    "Stick" = @([ordered]@{ title = "Unused content"; revision = 11407; section = "Stick" })
    "Weird Shroom" = @([ordered]@{ title = "Unused content"; revision = 11407; section = "Weird Shroom" })
    "Stone" = @([ordered]@{ title = "Unused content"; revision = 11407; section = "Stone" })
    "Torn Page" = @([ordered]@{ title = "Scroll"; revision = 11890; section = "Torn Pages" })
    "Warpsketball" = @([ordered]@{ title = "Unused content"; revision = 11407; section = "Warpsketball" })
}

$secondaryReviewNotes = @{
    "AUTOPARACHUTE" = "Unused content page documents a one-use parachute that deploys from inventory during a fall to prevent fall damage."
    "Berrynana Peel" = "The four color-variant pages share the slipping effect; their source biome and acquisition vary by Berrynana color."
    "Climbing Chalk" = "Unused content page says it has no model or texture and records a temporary reduction in climbing stamina use."
    "Green Clusterberry" = "Unused content page identifies prefab Clusterberry_UNUSED and records 10 Hunger restoration and 30 Poison removal; cooking behavior is not documented."
    "SCOUTMASTERSOUL" = "Nadir and Scoutmaster Myres pages describe communing with the soul, following it to the Peak, and restoring Myres in the ending."
    "Skull" = "Unused content page distinguishes this plain, non-functional skull from Cursed Skull."
    "Megaphone" = "Unused content page describes voice-range amplification as a possibility, not a confirmed effect."
    "Stick" = "Unused content page says it may have been intended as campfire fuel; a working use is not documented."
    "Stone" = "Unused content page notes a campfire heating behavior intended to prevent Cold, but says it does not appear functional."
    "Torn Page" = "Scroll page documents that opening the scroll reveals a page selected from the player's unread pages and progress."
    "Warpsketball" = "Unused content page describes a retextured basketball that teleports players to where it lands and is consumed."
    "Weird Shroom" = "Unused content page describes a non-poisonous mushroom that can be eaten twice; its intended glowing effect does not work."
}

$sourceConflicts = @{
    "Gold Prickleberry" = "The infobox says cooking removes Poison, while the article prose and patch history say cooking removes Thorns. Do not publish a state claim until reconciled with PEAK 2.6.b behavior."
}
$missingSourceNotes = @{}

$secondaryTitleSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($sourceSet in $secondarySources.Values) {
    foreach ($source in $sourceSet) { [void]$secondaryTitleSet.Add($source.title) }
}
$secondaryQuery = @{
    action = "query"
    format = "json"
    formatversion = "2"
    prop = "info|revisions"
    redirects = "1"
    rvprop = "ids|timestamp"
    titles = (@($secondaryTitleSet) | Sort-Object) -join "|"
}
$secondaryResponse = $null
for ($attempt = 1; $attempt -le 3; $attempt++) {
    try {
        $secondaryResponse = Invoke-RestMethod -Method Get -Uri $api -Body $secondaryQuery -Headers @{ "User-Agent" = $userAgent } -TimeoutSec 30
        break
    }
    catch {
        if ($attempt -eq 3) { throw "Wiki API request failed while refreshing secondary sources: $($_.Exception.Message)" }
        Start-Sleep -Seconds $attempt
    }
}
$secondaryCanonicalByAlias = [System.Collections.Generic.Dictionary[string, string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($rename in @($secondaryResponse.query.normalized) + @($secondaryResponse.query.converted) + @($secondaryResponse.query.redirects)) {
    if ($rename.from -and $rename.to) { $secondaryCanonicalByAlias[$rename.from] = $rename.to }
}
$secondaryPageByTitle = [System.Collections.Generic.Dictionary[string, object]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($page in $secondaryResponse.query.pages) { $secondaryPageByTitle[$page.title] = $page }

$entries = [System.Collections.Generic.List[object]]::new()
for ($offset = 0; $offset -lt $keys.Count; $offset += $BatchSize) {
    $end = [Math]::Min($offset + $BatchSize - 1, $keys.Count - 1)
    $batch = @($keys[$offset..$end])
    $requestedTitles = @($batch | ForEach-Object { if ($aliases.ContainsKey($_)) { $aliases[$_] } else { $_ } })
    $query = @{
        action = "query"
        format = "json"
        formatversion = "2"
        prop = "info|revisions"
        redirects = "1"
        rvprop = "ids|timestamp"
        rvslots = "main"
        titles = ($requestedTitles -join "|")
    }

    $response = $null
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        try {
            $response = Invoke-RestMethod -Method Get -Uri $api -Body $query -Headers @{ "User-Agent" = $userAgent } -TimeoutSec 30
            break
        }
        catch {
            if ($attempt -eq 3) { throw "Wiki API request failed for batch starting at item ${offset}: $($_.Exception.Message)" }
            Start-Sleep -Seconds $attempt
        }
    }

    $canonicalByAlias = [System.Collections.Generic.Dictionary[string, string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($rename in @($response.query.normalized) + @($response.query.converted) + @($response.query.redirects)) {
        if ($rename.from -and $rename.to) { $canonicalByAlias[$rename.from] = $rename.to }
    }

    $pageByTitle = [System.Collections.Generic.Dictionary[string, object]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($page in $response.query.pages) { $pageByTitle[$page.title] = $page }

    foreach ($gameKey in $batch) {
        $requestedTitle = if ($aliases.ContainsKey($gameKey)) { $aliases[$gameKey] } else { $gameKey }
        if ($secondarySources.ContainsKey($gameKey)) {
            $allSourcesCurrent = $true
            foreach ($source in $secondarySources[$gameKey]) {
                $resolvedSourceTitle = $source.title
                if ($secondaryCanonicalByAlias.ContainsKey($resolvedSourceTitle)) { $resolvedSourceTitle = $secondaryCanonicalByAlias[$resolvedSourceTitle] }
                $sourcePage = if ($secondaryPageByTitle.ContainsKey($resolvedSourceTitle)) { $secondaryPageByTitle[$resolvedSourceTitle] } else { $null }
                $sourceRevision = if ($sourcePage -and $sourcePage.revisions -and $sourcePage.revisions.Count -gt 0) { $sourcePage.revisions[0] } else { $null }
                if (-not $sourceRevision -or $sourceRevision.revid -ne $source.revision) { $allSourcesCurrent = $false }
                if ($sourcePage) { $source.currentTitle = $sourcePage.title }
                if ($sourceRevision) {
                    $source.currentRevision = $sourceRevision.revid
                    $source.currentTimestamp = $sourceRevision.timestamp
                }
            }
            $reviewNote = if ($secondaryReviewNotes.ContainsKey($gameKey)) { $secondaryReviewNotes[$gameKey] } else { $null }
            $secondaryStatus = if ($allSourcesCurrent -and $reviewNote) { "no-direct-item-page; secondary-source-reviewed" } else { "no-direct-item-page; secondary-source-needs-review" }
            $secondaryReviewStatus = if ($secondaryStatus -eq "no-direct-item-page; secondary-source-reviewed") { "secondary-section-reviewed; in-game-validation-pending" } else { $null }
            if (-not $allSourcesCurrent) { $reviewNote = "A cited secondary source revision changed or could not be resolved; reread the source before using it." }
            $entries.Add([ordered]@{
                gameKey = $gameKey
                requestedTitle = $requestedTitle
                wikiTitle = $null
                revision = $null
                timestamp = $null
                status = $secondaryStatus
                reviewStatus = $secondaryReviewStatus
                reviewNote = $reviewNote
                secondarySources = $secondarySources[$gameKey]
            })
            continue
        }
        $resolvedTitle = $requestedTitle
        if ($canonicalByAlias.ContainsKey($resolvedTitle)) { $resolvedTitle = $canonicalByAlias[$resolvedTitle] }
        $page = if ($pageByTitle.ContainsKey($resolvedTitle)) { $pageByTitle[$resolvedTitle] } else { $null }

        if ($null -eq $page -or $page.missing) {
            $missingNote = if ($missingSourceNotes.ContainsKey($gameKey)) { $missingSourceNotes[$gameKey] } else { $null }
            $entries.Add([ordered]@{ gameKey = $gameKey; requestedTitle = $requestedTitle; wikiTitle = $null; revision = $null; timestamp = $null; status = "missing"; reviewNote = $missingNote })
        }
        else {
            $revision = if ($page.revisions -and $page.revisions.Count -gt 0) { $page.revisions[0] } else { $null }
            $defaultItem = $defaults.items[$gameKey]
            $profile = if ($defaultItem -and $defaultItem.wiki) { $defaultItem.wiki } else { $null }
            $reviewStatus = $null
            $reviewNote = $null
            $entryStatus = if ($revision) { "page-found-needs-content-review" } else { "page-found-no-revision" }
            if ($revision -and $sourceConflicts.ContainsKey($gameKey)) {
                $entryStatus = "source-conflict; in-game-resolution-required"
                $reviewNote = $sourceConflicts[$gameKey]
            }
            elseif ($revision -and $profile -and $profile.revision -eq $revision.revid) {
                $reviewStatus = $profile.reviewStatus
                $entryStatus = "facts-reviewed; in-game-validation-pending"
            }
            elseif ($revision -and $profile) {
                $entryStatus = "page-revision-changed-needs-review"
            }
            $entries.Add([ordered]@{
                gameKey = $gameKey
                requestedTitle = $requestedTitle
                wikiTitle = $page.title
                revision = if ($revision) { $revision.revid } else { $null }
                timestamp = if ($revision) { $revision.timestamp } else { $null }
                status = $entryStatus
                reviewStatus = $reviewStatus
                reviewNote = $reviewNote
            })
        }
    }

    if ($end -lt $keys.Count - 1) { Start-Sleep -Milliseconds 500 }
}

$manifest = [ordered]@{
    source = "PEAK Wiki (community-maintained)"
    api = $api
    retrievalDateUtc = [DateTime]::UtcNow.ToString("yyyy-MM-dd")
    checkedGameVersion = $GameVersion
    reuseNotice = "Wiki page text is CC BY-SA 4.0 unless the individual page states otherwise; verify each source page."
    reviewPolicy = "A page lookup is only a candidate. Read and compare its item facts and cooking notes before marking it reviewed. A reviewed page can conclude that no item-specific state rule is needed; unresolved source conflicts remain separate."
    aliases = $aliases
    items = $entries
}

$json = ConvertTo-Json -InputObject $manifest -Depth 8
[IO.File]::WriteAllText($manifestPath, $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
$unreviewed = @($entries | Where-Object { $_.status -eq "page-found-needs-content-review" }).Count
$reviewed = @($entries | Where-Object { $_.status -eq "facts-reviewed; in-game-validation-pending" }).Count
$missing = @($entries | Where-Object { $_.status -eq "missing" }).Count
$secondaryOnly = @($entries | Where-Object { $_.status -like "no-direct-item-page;*" }).Count
$secondaryReviewed = @($entries | Where-Object { $_.status -eq "no-direct-item-page; secondary-source-reviewed" }).Count
$secondaryUnreviewed = @($entries | Where-Object { $_.status -eq "no-direct-item-page; secondary-source-needs-review" }).Count
$conflicts = @($entries | Where-Object { $_.status -like "source-conflict;*" }).Count
[PSCustomObject]@{
    Path = $manifestPath
    GameItems = $entries.Count
    FactReviewedPages = $reviewed
    UnreviewedCandidatePages = $unreviewed
    SecondarySourceOnlyItems = $secondaryOnly
    SecondarySourceReviewedItems = $secondaryReviewed
    SecondarySourceUnreviewedItems = $secondaryUnreviewed
    MissingWithoutSecondarySource = $missing
    SourceConflictPages = $conflicts
    NoDirectItemPages = $secondaryOnly + $missing
    InGameValidationPending = $true
} | ConvertTo-Json
