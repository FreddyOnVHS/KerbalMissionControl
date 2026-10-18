param([string]$RepoRoot = (Get-Location).Path)
$ErrorActionPreference = "Stop"

$path =
    Join-Path $RepoRoot "Tools\NavigationTests\Program.cs"

if (-not (Test-Path $path)) {
    throw "Missing NavigationTests Program.cs"
}

$text =
    [System.IO.File]::ReadAllText($path)

$oldFoundation = @'
        Check(
            page.IndexOf(
                "\"FRONT\"",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "\"SIDE\"",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "\"TOP\"",
                StringComparison.Ordinal) >= 0,
            "SCHEM orthographic view selectors missing");

        Check(
            page.IndexOf(
                "node.ParentPartId",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "node.VesselX",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "node.VesselY",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "node.VesselZ",
                StringComparison.Ordinal) >= 0,
            "SCHEM structural projection path incomplete");
'@

$newFoundation = @'
        Check(
            page.IndexOf(
                "\"ACTIVE VESSEL / SIDE\"",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "\"SINGLE SIDE VIEW\"",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "\"FRONT\"",
                StringComparison.Ordinal) < 0 &&
            page.IndexOf(
                "\"TOP\"",
                StringComparison.Ordinal) < 0,
            "SCHEM single side-view presentation missing");

        Check(
            page.IndexOf(
                "SideOutlinePoints",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "DrawPartOutline",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "node.VesselZ",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "node.VesselY",
                StringComparison.Ordinal) >= 0,
            "SCHEM side-outline/fallback projection path incomplete");
'@

$oldGeometry = @'
        Check(
            page.IndexOf(
                "BuildVesselBoxCorners",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "DrawProjectedBox",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "AxisForward",
                StringComparison.Ordinal) >= 0,
            "Mission Control oriented-box renderer missing");
'@

$newGeometry = @'
        Check(
            page.IndexOf(
                "SideOutlinePoints",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "DrawPartOutline",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "FillPolygon",
                StringComparison.Ordinal) >= 0,
            "Mission Control mesh-outline renderer missing");
'@

$changed = $false

if ($text.Contains($oldFoundation)) {
    $text =
        $text.Replace(
            $oldFoundation,
            $newFoundation)

    $changed = $true
}
elseif (-not $text.Contains("SCHEM single side-view presentation missing")) {
    throw "Could not locate stale 14.23.1 SCHEM assertions. No file changed."
}

if ($text.Contains($oldGeometry)) {
    $text =
        $text.Replace(
            $oldGeometry,
            $newGeometry)

    $changed = $true
}
elseif (-not $text.Contains("Mission Control mesh-outline renderer missing")) {
    throw "Could not locate stale 14.23.2 SCHEM assertion. No file changed."
}

if ($changed) {
    [System.IO.File]::WriteAllText(
        $path,
        $text,
        [System.Text.UTF8Encoding]::new($false))
}

Write-Host "Applied KMC 14.23.3a stale SCHEM regression update."
Write-Host "Production code was not changed."
Write-Host "The old FRONT/SIDE/TOP and oriented-box expectations now match the new single-view mesh-outline design."
