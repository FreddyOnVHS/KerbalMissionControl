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

$old = @'
        int manualStart =
            planner.IndexOf(
                "CalculateManualProgradeRetrograde",
                StringComparison.Ordinal);

        int manualEnd =
            planner.IndexOf(
                "ValidateTargetPeriapsis",
                manualStart,
                StringComparison.Ordinal);
'@

$new = @'
        int manualStart =
            planner.IndexOf(
                "private ManeuverPlanModel CalculateManualProgradeRetrograde(",
                StringComparison.Ordinal);

        int manualEnd =
            planner.IndexOf(
                "private static bool ValidateTargetPeriapsis(",
                manualStart,
                StringComparison.Ordinal);
'@

if (-not $text.Contains($old)) {
    throw "Could not locate 14.22.75b1 manual regression anchors."
}

$text =
    $text.Replace(
        $old,
        $new)

[System.IO.File]::WriteAllText(
    $path,
    $text,
    [System.Text.UTF8Encoding]::new($false))

Write-Host "Applied KMC 14.22.75b2 manual-node regression anchor fix."
