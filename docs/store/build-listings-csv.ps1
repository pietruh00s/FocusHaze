# Fills the Store listings CSV exported from Partner Center with the text from docs/store/listings.json
# (all 14 languages). Screenshots are not part of the import: add them in Partner Center (docs/store/screenshot-*.png).
#
# 1. Partner Center › FocusHaze › Store listings › Export listing  (saves a .csv template)
# 2. pwsh ./docs/store/build-listings-csv.ps1 -Template <path to exported .csv>
# 3. Partner Center › Import listings › Import .csv › artifacts/store/listings.csv
#
# The Field/ID/Type columns of the template are kept exactly as exported (Partner Center requires that).
# Rows for screenshots and other images are left out of the output, so whatever is set in Partner Center stays.
param(
    [Parameter(Mandatory)][string]$Template,
    [string]$Out = (Join-Path $PSScriptRoot '..\..\artifacts\store\listings.csv')
)
$ErrorActionPreference = 'Stop'

$listings = Get-Content (Join-Path $PSScriptRoot 'listings.json') -Raw -Encoding utf8 | ConvertFrom-Json
# Trailing blank lines in the export come through as empty rows; drop them.
$rows = @(Import-Csv $Template -Encoding utf8 | Where-Object { $_.PSObject.Properties.Value[0] })
if ($rows.Count -eq 0) { throw "Template has no rows: $Template" }
$columns = @($rows[0].PSObject.Properties.Name)
$fieldColumn = $columns[0]   # "Field"
$typeColumn = $columns[2]    # "Type (Type)"

# Keep only text fields: deleting a row leaves that field untouched in Partner Center (per Microsoft's docs),
# while image rows with local paths would require a folder import. Trailer rows are the documented exception
# (they must not be deleted), so they stay exactly as exported.
$rows = @($rows | Where-Object { $_.$typeColumn -notlike 'Relative path*' -or $_.$fieldColumn -like 'Trailer*' })

$problems = [System.Collections.Generic.List[string]]::new()
$used = [System.Collections.Generic.HashSet[string]]::new()

foreach ($locale in $listings.PSObject.Properties.Name) {
    $data = $listings.$locale
    if ($columns -notcontains $locale) {
        $columns += $locale
        foreach ($row in $rows) { $row | Add-Member -NotePropertyName $locale -NotePropertyValue '' }
    }

    $values = @{
        'Title'            = $data.Title
        # ShortTitle is a shorter product name, not a tagline; FocusHaze is short already, so leave it empty.
        'ShortTitle'       = ''
        'ShortDescription' = $data.ShortDescription
        'Description'      = $data.Description
        'ReleaseNotes'     = $data.ReleaseNotes
        # Caption for the first screenshot (the screenshots themselves are added in Partner Center).
        'DesktopScreenshotCaption1' = $data.ScreenshotCaption
    }
    for ($i = 0; $i -lt $data.Features.Count; $i++) { $values["Feature$($i + 1)"] = $data.Features[$i] }
    for ($i = 0; $i -lt $data.SearchTerms.Count; $i++) { $values["SearchTerm$($i + 1)"] = $data.SearchTerms[$i] }

    foreach ($row in $rows) {
        $field = $row.$fieldColumn
        if ($values.ContainsKey($field)) {
            $row.$locale = $values[$field]
            [void]$used.Add($field)
        }
    }

    # Store limits (Partner Center › Create app Store listings).
    if ($data.Description.Length -gt 10000) { $problems.Add("$locale Description is longer than 10000 characters") }
    if ($data.ShortDescription.Length -gt 1000) { $problems.Add("$locale ShortDescription is longer than 1000 characters") }
    if ($data.ReleaseNotes.Length -gt 1500) { $problems.Add("$locale ReleaseNotes is longer than 1500 characters") }
    if ($data.ScreenshotCaption.Length -gt 200) { $problems.Add("$locale ScreenshotCaption is longer than 200 characters") }
    if ($data.Features.Count -gt 20) { $problems.Add("$locale has more than 20 features") }
    $data.Features | Where-Object { $_.Length -gt 200 } | ForEach-Object { $problems.Add("$locale feature over 200 characters: $_") }
    if ($data.SearchTerms.Count -gt 7) { $problems.Add("$locale has more than 7 search terms") }
    $data.SearchTerms | Where-Object { $_.Length -gt 30 } | ForEach-Object { $problems.Add("$locale search term over 30 characters: $_") }
    $words = ($data.SearchTerms | ForEach-Object { $_ -split '\s+' } | Where-Object { $_ }).Count
    if ($words -gt 21) { $problems.Add("$locale search terms have $words words (max 21)") }
}

$expected = @('Title', 'ShortDescription', 'Description', 'ReleaseNotes', 'DesktopScreenshotCaption1', 'Feature1', 'SearchTerm1')
$missing = $expected | Where-Object { -not $used.Contains($_) }
if ($missing) { $problems.Add("Template has no rows for: $($missing -join ', ') (field names may differ; check the template)") }

$Out = [IO.Path]::GetFullPath($Out)
New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
$rows | Select-Object $columns | Export-Csv $Out -NoTypeInformation -UseQuotes Always -Encoding utf8BOM

"Wrote $Out ($($rows.Count) rows, $(@($listings.PSObject.Properties.Name).Count) languages, no image rows)"
if ($problems.Count) { "Problems:"; $problems | ForEach-Object { "  - $_" } } else { "No problems found." }
