param(
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$path =
    Join-Path $RepoRoot "Tools\NavigationTests\Program.cs"

if (-not (Test-Path $path)) {
    throw "NavigationTests Program.cs not found at $path"
}

$text =
    [System.IO.File]::ReadAllText($path)

if (-not $text.Contains("using KMC.Engine.Orbit;")) {
    $needle =
        "using KMC.Engine.Navigation;"

    if (-not $text.Contains($needle)) {
        throw "Could not locate KMC.Engine.Navigation import."
    }

    $text =
        $text.Replace(
            $needle,
            $needle + "`r`nusing KMC.Engine.Orbit;")

    [System.IO.File]::WriteAllText(
        $path,
        $text,
        [System.Text.UTF8Encoding]::new($false))
}

Write-Host "Applied KMC 14.22.72b NavigationTests orbit namespace fix."
