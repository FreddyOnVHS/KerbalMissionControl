param([string]$RepoRoot = (Get-Location).Path)
$ErrorActionPreference = "Stop"

function ReadText([string]$path) {
    if (-not (Test-Path $path)) {
        throw "Missing file: $path"
    }

    return [System.IO.File]::ReadAllText($path)
}

function WriteText([string]$path,[string]$text) {
    [System.IO.File]::WriteAllText(
        $path,
        $text,
        [System.Text.UTF8Encoding]::new($false))
}

$pagePath =
    Join-Path $RepoRoot "KMC.MissionControl\Pages\SchematicPage.cs"

$testPath =
    Join-Path $RepoRoot "Tools\NavigationTests\Program.cs"

$page =
    ReadText $pagePath

$tests =
    ReadText $testPath

# ---------------------------------------------------------------------------
# Preflight expected 14.23.4a state.
# ---------------------------------------------------------------------------
if (-not $page.Contains("private static void DrawEngineBell(") -or
    -not $page.Contains("float bellHeight =") -or
    -not $page.Contains("private static bool IsMinorPart(")) {
    throw "Expected 14.23.4a SCHEM presentation state was not found."
}

if (-not $tests.Contains('Run("SCHEM engine bell is compact and attached"')) {
    throw "Expected 14.23.4a regression marker was not found."
}

# ---------------------------------------------------------------------------
# Strengthen primary hull, dim secondary details.
# ---------------------------------------------------------------------------
$oldStyle = @'
            bool major =
                IsMajorPart(
                    node);

            bool minor =
                IsMinorPart(
                    node);

            Color outlineColor =
                isRoot
                    ? Color.White
                    : context.PhosphorColor;

            int fillAlpha =
                isRoot
                    ? 34
                    : major
                        ? 24
                        : minor
                            ? 3
                            : 8;

            int lineAlpha =
                isRoot
                    ? 245
                    : major
                        ? 235
                        : minor
                            ? 82
                            : 145;

            float lineWidth =
                isRoot
                    ? 2.2f
                    : major
                        ? 1.7f
                        : minor
                            ? 0.85f
                            : 1.05f;
'@

$newStyle = @'
            bool primaryHull =
                IsPrimaryHullPart(
                    node);

            bool major =
                IsMajorPart(
                    node);

            bool minor =
                IsMinorPart(
                    node);

            Color outlineColor =
                isRoot
                    ? Color.White
                    : context.PhosphorColor;

            /*
             * Visual hierarchy:
             * - primary hull establishes the spacecraft silhouette
             * - important subsystem parts remain readable but secondary
             * - small/internal details recede into the background
             */
            int fillAlpha =
                isRoot
                    ? 44
                    : primaryHull
                        ? 38
                        : major
                            ? 20
                            : minor
                                ? 2
                                : 6;

            int lineAlpha =
                isRoot
                    ? 255
                    : primaryHull
                        ? 245
                        : major
                            ? 195
                            : minor
                                ? 58
                                : 115;

            float lineWidth =
                isRoot
                    ? 2.35f
                    : primaryHull
                        ? 2.0f
                        : major
                            ? 1.4f
                            : minor
                                ? 0.70f
                                : 0.95f;
'@

if ($page.Contains($oldStyle)) {
    $page =
        $page.Replace(
            $oldStyle,
            $newStyle)
}
elseif (-not $page.Contains("bool primaryHull =")) {
    throw "Could not locate 14.23.4a presentation style block."
}

# ---------------------------------------------------------------------------
# Increase tiny-detail suppression threshold.
# ---------------------------------------------------------------------------
$page =
    $page.Replace(
        "bounds.Width < 7.0f &&`r`n                bounds.Height < 7.0f;",
        "bounds.Width < 11.0f &&`r`n                bounds.Height < 11.0f;")

$page =
    $page.Replace(
        "bounds.Width < 7.0f &&`n                bounds.Height < 7.0f;",
        "bounds.Width < 11.0f &&`n                bounds.Height < 11.0f;")

# ---------------------------------------------------------------------------
# Add primary-hull classification.
# ---------------------------------------------------------------------------
if (-not $page.Contains("private static bool IsPrimaryHullPart(")) {
    $marker =
        "        private static bool IsMajorPart("

    if (-not $page.Contains($marker)) {
        throw "Could not locate IsMajorPart insertion point."
    }

    $helper = @'
        private static bool IsPrimaryHullPart(
            VesselTopologyNode node)
        {
            if (node == null)
            {
                return false;
            }

            /*
             * These categories should visually define the vehicle's main
             * silhouette before secondary equipment is noticed.
             */
            return
                node.Category ==
                    VesselNodeCategory.Command ||
                node.Category ==
                    VesselNodeCategory.FuelTank ||
                node.Category ==
                    VesselNodeCategory.Fairing ||
                node.Category ==
                    VesselNodeCategory.Payload ||
                node.Category ==
                    VesselNodeCategory.SolidBooster;
        }

'@

    $page =
        $page.Replace(
            $marker,
            $helper + $marker)
}

# ---------------------------------------------------------------------------
# Regression coverage.
# ---------------------------------------------------------------------------
if (-not $tests.Contains('Run("SCHEM primary hull dominates visual hierarchy"')) {
    $anchor =
        '        Run("SCHEM engine bell is compact and attached", VesselSchematicEngineBellScaleRegression);'

    if (-not $tests.Contains($anchor)) {
        throw "Could not locate 14.23.4a regression registration."
    }

    $tests =
        $tests.Replace(
            $anchor,
            $anchor + "`r`n" +
            '        Run("SCHEM primary hull dominates visual hierarchy", VesselSchematicHullHierarchyRegression);')
}

if (-not $tests.Contains("private static void VesselSchematicHullHierarchyRegression()")) {
    $anchor =
        "    private static void ManeuverUplinkDesiredPeriapsis()"

    if (-not $tests.Contains($anchor)) {
        throw "Could not locate NavigationTests insertion point."
    }

    $method = @'
    private static void VesselSchematicHullHierarchyRegression()
    {
        string page =
            File.ReadAllText(
                Path.Combine(
                    Environment.CurrentDirectory,
                    "KMC.MissionControl",
                    "Pages",
                    "SchematicPage.cs"));

        Check(
            page.IndexOf(
                "IsPrimaryHullPart",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "VesselNodeCategory.Fairing",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "VesselNodeCategory.FuelTank",
                StringComparison.Ordinal) >= 0,
            "SCHEM primary hull classification missing");

        Check(
            page.IndexOf(
                "primaryHull" +
                Environment.NewLine +
                "                        ? 38",
                StringComparison.Ordinal) >= 0 ||
            page.IndexOf(
                "primaryHull\r\n                        ? 38",
                StringComparison.Ordinal) >= 0,
            "SCHEM primary hull fill emphasis missing");

        Check(
            page.IndexOf(
                "primaryHull" +
                Environment.NewLine +
                "                        ? 245",
                StringComparison.Ordinal) >= 0 ||
            page.IndexOf(
                "primaryHull\r\n                        ? 245",
                StringComparison.Ordinal) >= 0,
            "SCHEM primary hull outline emphasis missing");

        Check(
            page.IndexOf(
                "bounds.Width < 11.0f",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "bounds.Height < 11.0f",
                StringComparison.Ordinal) >= 0,
            "SCHEM stronger tiny-detail suppression missing");

        Check(
            page.IndexOf(
                "DrawEngineBell",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "float bellHeight =",
                StringComparison.Ordinal) >= 0,
            "SCHEM engine bell changed unexpectedly");
    }


'@

    $tests =
        $tests.Replace(
            $anchor,
            $method + $anchor)
}

# ---------------------------------------------------------------------------
# Write after all transformations succeed.
# ---------------------------------------------------------------------------
WriteText $pagePath $page
WriteText $testPath $tests

Write-Host "Applied KMC 14.23.4b Hull Visual Hierarchy."
Write-Host "Primary hull sections are stronger; secondary/minor details are dimmer."
Write-Host "Tiny low-priority details under ~11x11 pixels are suppressed."
Write-Host "Engine bell geometry is unchanged."
Write-Host "Mission Control only: KMC.Plugin.dll does NOT need replacement."
