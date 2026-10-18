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
# Preflight expected 14.23.3a local state.
# ---------------------------------------------------------------------------
if (-not $page.Contains('"ACTIVE VESSEL / SIDE"') -or
    -not $page.Contains("DrawPartOutline(") -or
    -not $page.Contains("FillPolygon(")) {
    throw "Expected 14.23.3 single-view SCHEM page was not found."
}

if (-not $tests.Contains('Run("SCHEM uses one clean side-view renderer"')) {
    throw "Expected 14.23.3 regression marker was not found."
}

# ---------------------------------------------------------------------------
# Replace DrawPartOutline with presentation-polished version.
# ---------------------------------------------------------------------------
$startMarker =
    "        private static void DrawPartOutline("

$endMarker =
    "        private static bool IsMajorPart("

$start =
    $page.IndexOf($startMarker)

$end =
    $page.IndexOf($endMarker,$start)

if ($start -lt 0 -or $end -lt 0) {
    throw "Could not locate DrawPartOutline block."
}

$newBlock = @'
        private static void DrawPartOutline(
            MissionRenderContext context,
            PointF[] polygon,
            VesselTopologyNode node,
            bool isRoot)
        {
            if (polygon == null ||
                polygon.Length < 3 ||
                node == null)
            {
                return;
            }

            RectangleF visualBounds =
                GetPolygonBounds(
                    polygon);

            if (ShouldSuppressMinorPart(
                    node,
                    visualBounds))
            {
                return;
            }

            if (node.Category ==
                VesselNodeCategory.Engine)
            {
                DrawEngineBell(
                    context,
                    visualBounds,
                    isRoot);

                return;
            }

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

            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        fillAlpha,
                        outlineColor)))
            using (Pen pen =
                new Pen(
                    Color.FromArgb(
                        lineAlpha,
                        outlineColor),
                    lineWidth))
            {
                context.Graphics.FillPolygon(
                    fill,
                    polygon);

                context.Graphics.DrawPolygon(
                    pen,
                    polygon);
            }
        }

        private static void DrawEngineBell(
            MissionRenderContext context,
            RectangleF bounds,
            bool isRoot)
        {
            if (bounds.Width <= 0.0f ||
                bounds.Height <= 0.0f)
            {
                return;
            }

            /*
             * Schematic convention: render liquid-engine parts as a clean
             * bell/nozzle rather than exposing a boxy convex hull. The bell
             * stays inside the real projected engine bounds, so positioning
             * and overall scale still come from live vessel geometry.
             */
            float centerX =
                bounds.Left +
                bounds.Width * 0.5f;

            float topY =
                bounds.Top +
                bounds.Height * 0.08f;

            float throatY =
                bounds.Top +
                bounds.Height * 0.30f;

            float bottomY =
                bounds.Bottom -
                bounds.Height * 0.04f;

            float topHalfWidth =
                Math.Max(
                    2.0f,
                    bounds.Width * 0.22f);

            float throatHalfWidth =
                Math.Max(
                    2.0f,
                    bounds.Width * 0.30f);

            float exitHalfWidth =
                Math.Max(
                    3.0f,
                    bounds.Width * 0.48f);

            PointF[] bell =
            {
                new PointF(
                    centerX - topHalfWidth,
                    topY),

                new PointF(
                    centerX + topHalfWidth,
                    topY),

                new PointF(
                    centerX + throatHalfWidth,
                    throatY),

                new PointF(
                    centerX + exitHalfWidth,
                    bottomY),

                new PointF(
                    centerX - exitHalfWidth,
                    bottomY),

                new PointF(
                    centerX - throatHalfWidth,
                    throatY)
            };

            Color color =
                isRoot
                    ? Color.White
                    : context.PhosphorColor;

            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        26,
                        color)))
            using (Pen pen =
                new Pen(
                    Color.FromArgb(
                        isRoot
                            ? 245
                            : 235,
                        color),
                    isRoot
                        ? 2.2f
                        : 1.8f))
            {
                context.Graphics.FillPolygon(
                    fill,
                    bell);

                context.Graphics.DrawPolygon(
                    pen,
                    bell);

                // Simple nozzle-exit line makes the bell read immediately.
                context.Graphics.DrawLine(
                    pen,
                    centerX - exitHalfWidth,
                    bottomY,
                    centerX + exitHalfWidth,
                    bottomY);
            }
        }

        private static RectangleF GetPolygonBounds(
            PointF[] polygon)
        {
            float minX =
                float.PositiveInfinity;

            float maxX =
                float.NegativeInfinity;

            float minY =
                float.PositiveInfinity;

            float maxY =
                float.NegativeInfinity;

            for (int index = 0;
                 index < polygon.Length;
                 index++)
            {
                minX =
                    Math.Min(
                        minX,
                        polygon[index].X);

                maxX =
                    Math.Max(
                        maxX,
                        polygon[index].X);

                minY =
                    Math.Min(
                        minY,
                        polygon[index].Y);

                maxY =
                    Math.Max(
                        maxY,
                        polygon[index].Y);
            }

            return
                RectangleF.FromLTRB(
                    minX,
                    minY,
                    maxX,
                    maxY);
        }

        private static bool ShouldSuppressMinorPart(
            VesselTopologyNode node,
            RectangleF bounds)
        {
            if (node == null ||
                !IsMinorPart(node))
            {
                return false;
            }

            /*
             * Tiny low-priority details create visual noise at mission-control
             * scale. Preserve them when they are large enough to matter.
             */
            return
                bounds.Width < 7.0f &&
                bounds.Height < 7.0f;
        }

        private static bool IsMinorPart(
            VesselTopologyNode node)
        {
            if (node == null)
            {
                return false;
            }

            return
                node.Category ==
                    VesselNodeCategory.Unknown ||
                node.Category ==
                    VesselNodeCategory.Structural ||
                node.Category ==
                    VesselNodeCategory.ReactionWheel ||
                node.Category ==
                    VesselNodeCategory.Battery ||
                node.Category ==
                    VesselNodeCategory.Generator ||
                node.Category ==
                    VesselNodeCategory.Antenna ||
                node.Category ==
                    VesselNodeCategory.RcsThruster;
        }

'@

$page =
    $page.Substring(0,$start) +
    $newBlock +
    $page.Substring($end)

# ---------------------------------------------------------------------------
# Slightly tighten major/minor hierarchy.
# Keep engine major classification for status/style semantics.
# ---------------------------------------------------------------------------
# No transport/plugin changes needed.

# ---------------------------------------------------------------------------
# Regression coverage.
# ---------------------------------------------------------------------------
if (-not $tests.Contains('Run("SCHEM presentation prioritizes hull and engine bell"')) {
    $anchor =
        '        Run("SCHEM uses one clean side-view renderer", VesselSchematicSingleViewRegression);'

    if (-not $tests.Contains($anchor)) {
        throw "Could not locate 14.23.3 single-view regression registration."
    }

    $tests =
        $tests.Replace(
            $anchor,
            $anchor + "`r`n" +
            '        Run("SCHEM presentation prioritizes hull and engine bell", VesselSchematicPresentationPolishRegression);')
}

if (-not $tests.Contains("private static void VesselSchematicPresentationPolishRegression()")) {
    $anchor =
        "    private static void ManeuverUplinkDesiredPeriapsis()"

    if (-not $tests.Contains($anchor)) {
        throw "Could not locate NavigationTests method insertion point."
    }

    $method = @'
    private static void VesselSchematicPresentationPolishRegression()
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
                "DrawEngineBell",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "VesselNodeCategory.Engine",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "exitHalfWidth",
                StringComparison.Ordinal) >= 0,
            "SCHEM engine-bell presentation missing");

        Check(
            page.IndexOf(
                "ShouldSuppressMinorPart",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "IsMinorPart",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "bounds.Width < 7.0f",
                StringComparison.Ordinal) >= 0,
            "SCHEM tiny-detail suppression missing");

        Check(
            page.IndexOf(
                "lineAlpha",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "lineWidth",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "fillAlpha",
                StringComparison.Ordinal) >= 0,
            "SCHEM visual hierarchy styling missing");

        Check(
            page.IndexOf(
                "\"ACTIVE VESSEL / SIDE\"",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "\"FRONT\"",
                StringComparison.Ordinal) < 0 &&
            page.IndexOf(
                "\"TOP\"",
                StringComparison.Ordinal) < 0,
            "SCHEM single-view layout changed unexpectedly");
    }


'@

    $tests =
        $tests.Replace(
            $anchor,
            $method + $anchor)
}

# ---------------------------------------------------------------------------
# Write only after all transformations succeed.
# ---------------------------------------------------------------------------
WriteText $pagePath $page
WriteText $testPath $tests

Write-Host "Applied KMC 14.23.4 Schematic Presentation Polish."
Write-Host "Engine parts now render as tapered bell/nozzle symbols."
Write-Host "Major hull parts are stronger; minor details are dimmer and tiny clutter can be suppressed."
Write-Host "Mission Control only: KMC.Plugin.dll does NOT need replacement."
