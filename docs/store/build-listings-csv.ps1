# Fills the Store listings CSV exported from Partner Center with docs/store/listings.json (all 14 languages)
# and stages the screenshots, producing a folder to import with "Import listings › Import folder".
#
# 1. Partner Center › FocusHaze › Store listings › Export listing  (saves a .csv template)
# 2. pwsh ./docs/store/build-listings-csv.ps1 -Template <path to exported .csv>
# 3. Partner Center › Import listings › Import folder › artifacts/store/FocusHazeListings
#
# The Field/ID/Type columns of the template are kept exactly as exported (Partner Center requires that).
param(
    [Parameter(Mandatory)][string]$Template,
    [string]$OutRoot = (Join-Path $PSScriptRoot '..\..\artifacts\store'),
    [string]$FolderName = 'FocusHazeListings'
)
$ErrorActionPreference = 'Stop'

$listings = Get-Content (Join-Path $PSScriptRoot 'listings.json') -Raw -Encoding utf8 | ConvertFrom-Json
$rows = @(Import-Csv $Template -Encoding utf8)
if ($rows.Count -eq 0) { throw "Template has no rows: $Template" }
$columns = @($rows[0].PSObject.Properties.Name)
$fieldColumn = $columns[0]   # "Field"

$folder = Join-Path ([IO.Path]::GetFullPath($OutRoot)) $FolderName
if (Test-Path $folder) { Get-ChildItem $folder -Recurse -File | ForEach-Object { $_.Delete() } }
New-Item -ItemType Directory -Force (Join-Path $folder 'screenshots') | Out-Null

$problems = [System.Collections.Generic.List[string]]::new()
$used = [System.Collections.Generic.HashSet[string]]::new()

foreach ($locale in $listings.PSObject.Properties.Name) {
    $data = $listings.$locale
    if ($columns -notcontains $locale) {
        $columns += $locale
        foreach ($row in $rows) { $row | Add-Member -NotePropertyName $locale -NotePropertyValue '' }
    }

    Copy-Item (Join-Path $PSScriptRoot $data.Screenshot) (Join-Path $folder "screenshots\$($data.Screenshot)")
    $values = @{
        'Title'              = $data.Title
        'ShortDescription'   = $data.ShortDescription
        'Description'        = $data.Description
        'ReleaseNotes'       = $data.ReleaseNotes
        'DesktopScreenshot1' = "$FolderName/screenshots/$($data.Screenshot)"
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
    if ($data.Features.Count -gt 20) { $problems.Add("$locale has more than 20 features") }
    $data.Features | Where-Object { $_.Length -gt 200 } | ForEach-Object { $problems.Add("$locale feature over 200 characters: $_") }
    if ($data.SearchTerms.Count -gt 7) { $problems.Add("$locale has more than 7 search terms") }
    $data.SearchTerms | Where-Object { $_.Length -gt 30 } | ForEach-Object { $problems.Add("$locale search term over 30 characters: $_") }
    $words = ($data.SearchTerms | ForEach-Object { $_ -split '\s+' } | Where-Object { $_ }).Count
    if ($words -gt 21) { $problems.Add("$locale search terms have $words words (max 21)") }
}

$expected = @('Title', 'ShortDescription', 'Description', 'ReleaseNotes', 'DesktopScreenshot1', 'Feature1', 'SearchTerm1')
$missing = $expected | Where-Object { -not $used.Contains($_) }
if ($missing) { $problems.Add("Template has no rows for: $($missing -join ', ') (field names may differ; check the template)") }

$csv = Join-Path $folder 'listings.csv'
$rows | Select-Object $columns | Export-Csv $csv -NoTypeInformation -UseQuotes Always -Encoding utf8BOM

"Wrote $csv"
"Languages: $(@($listings.PSObject.Properties.Name).Count), screenshots: $((Get-ChildItem (Join-Path $folder 'screenshots')).Count)"
if ($problems.Count) { "Problems:"; $problems | ForEach-Object { "  - $_" } } else { "No problems found." }
