param(
    [Parameter(Mandatory = $true)]
    [string]$Output
)

$source = Join-Path (Join-Path $PSScriptRoot '..') 'README.md'
$content = Get-Content -Raw -Path $source
$content = $content -replace '\]\(docs/', '](https://github.com/mihakralj/QuanTAlib/blob/main/docs/'
$content = $content -replace '\]\(lib/', '](https://github.com/mihakralj/QuanTAlib/blob/main/lib/'
$content = $content -replace '\]\(LICENSE\)', '](https://github.com/mihakralj/QuanTAlib/blob/main/LICENSE)'

$dir = Split-Path -Parent $Output
if ($dir -and -not (Test-Path -LiteralPath $dir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

[System.IO.File]::WriteAllText($Output, $content, (New-Object System.Text.UTF8Encoding($false)))
