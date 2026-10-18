param([string]$RepoRoot = (Get-Location).Path)
$ErrorActionPreference = "Stop"

$path = Join-Path $RepoRoot "Tools\NavigationTests\Program.cs"

if (-not (Test-Path $path)) {
    throw "Missing NavigationTests Program.cs"
}

$text = [System.IO.File]::ReadAllText($path)

if ($text.Contains("using KMC.Shared.Topology;")) {
    Write-Host "KMC.Shared.Topology using already present."
    exit 0
}

$needle = "using KMC.Shared;"

$index = $text.IndexOf(
    $needle,
    [StringComparison]::Ordinal)

if ($index -lt 0) {
    throw "Could not locate using KMC.Shared; in NavigationTests Program.cs"
}

$insertAt = $index + $needle.Length

$newline = "`r`n"

if ($insertAt -lt $text.Length -and
    $text[$insertAt] -eq "`n") {
    $newline = "`n"
}
elseif ($insertAt + 1 -lt $text.Length -and
        $text[$insertAt] -eq "`r" -and
        $text[$insertAt + 1] -eq "`n") {
    $newline = "`r`n"
}

$text =
    $text.Substring(0, $insertAt) +
    $newline +
    "using KMC.Shared.Topology;" +
    $text.Substring($insertAt + $newline.Length)

[System.IO.File]::WriteAllText(
    $path,
    $text,
    [System.Text.UTF8Encoding]::new($false))

Write-Host "Applied KMC 14.23.1a NavigationTests topology namespace fix."
