param(
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

function ReadText([string]$path) {
    if (-not (Test-Path $path)) {
        throw "Missing file: $path"
    }

    return [System.IO.File]::ReadAllText($path)
}

function WriteText([string]$path, [string]$text) {
    [System.IO.File]::WriteAllText(
        $path,
        $text,
        [System.Text.UTF8Encoding]::new($false))
}

# ===========================================================================
# 1. GUID: FINAL COUNT / IGNITION CUE
# ===========================================================================

$pagePath =
    Join-Path $RepoRoot "KMC.MissionControl\Pages\GuidancePage.cs"

$page =
    ReadText $pagePath

# Add crew-facing short countdown helper.
if (-not $page.Contains("private static string FormatIgnitionCountdown(")) {
    $insert =
        "        private static void DrawCompactField("

    if (-not $page.Contains($insert)) {
        throw "Could not locate DrawCompactField insertion point."
    }

    $helper = @'
        private static string FormatIgnitionCountdown(
            double seconds)
        {
            if (!IsFinite(seconds))
            {
                return "--.-";
            }

            double remaining =
                Math.Max(
                    0.0,
                    seconds);

            return
                remaining.ToString(
                    "0.0",
                    System.Globalization.CultureInfo.InvariantCulture) +
                " S";
        }

'@

    $page =
        $page.Replace(
            $insert,
            $helper + $insert)
}

# Replace crew-command block with explicit FINAL COUNT / IGNITION DUE visuals.
$oldBlock = @'
            bool ignitionStandby =
                string.Equals(
                    command,
                    "IGNITION STANDBY",
                    StringComparison.OrdinalIgnoreCase);

            /*
             * Render-time blink only. GUID is already continuously redrawn;
             * no UI timer or Engine state is introduced.
             */
            bool showStandbyBox =
                ignitionStandby &&
                DateTime.UtcNow.Millisecond < 500;

            using (SolidBrush brush =
                new SolidBrush(
                    color))
            using (SolidBrush dimBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (Font commandFont =
                new Font(
                    "Consolas",
                    ignitionStandby
                        ? 30.0f
                        : 22.0f,
                    FontStyle.Bold))
'@

$newBlock = @'
            bool ignitionStandby =
                guidance != null &&
                string.Equals(
                    guidance.Status,
                    "FINAL COUNT",
                    StringComparison.OrdinalIgnoreCase);

            bool ignitionDue =
                guidance != null &&
                string.Equals(
                    guidance.Status,
                    "IGNITION DUE",
                    StringComparison.OrdinalIgnoreCase);

            if (ignitionStandby)
            {
                command =
                    "IGNITION STANDBY" +
                    Environment.NewLine +
                    "T- " +
                    FormatIgnitionCountdown(
                        guidance.TimeToIgnitionSeconds);

                color =
                    Color.Orange;
            }
            else if (ignitionDue)
            {
                command =
                    "IGNITE NOW" +
                    Environment.NewLine +
                    "HOLD MANEUVER VECTOR";

                color =
                    Color.Red;
            }

            /*
             * Render-time alert only. GUID is already continuously redrawn;
             * no UI timer or Engine state is introduced.
             */
            bool showAlertBox =
                (ignitionStandby ||
                 ignitionDue) &&
                DateTime.UtcNow.Millisecond < 500;

            using (SolidBrush brush =
                new SolidBrush(
                    color))
            using (SolidBrush dimBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (Font commandFont =
                new Font(
                    "Consolas",
                    ignitionStandby || ignitionDue
                        ? 30.0f
                        : 22.0f,
                    FontStyle.Bold))
'@

if ($page.Contains($oldBlock)) {
    $page =
        $page.Replace(
            $oldBlock,
            $newBlock)
}
elseif (-not $page.Contains("FormatIgnitionCountdown(") -or
        -not $page.Contains('"IGNITE NOW"')) {
    throw "Could not locate current crew-command ignition block."
}

$page =
    $page.Replace(
        "if (showStandbyBox)",
        "if (showAlertBox)")

# Keep GetCrewCommandColor semantically correct even outside the render override.
$oldColors = @'
            if (string.Equals(
                    command,
                    "HOLD MANEUVER VECTOR",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    command,
                    "IGNITION STANDBY",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    command,
                    "IGNITE / HOLD MANEUVER VECTOR",
                    StringComparison.OrdinalIgnoreCase))
            {
                return
                    GetAlignmentColor(
                        guidance);
            }
'@

$newColors = @'
            if (string.Equals(
                    command,
                    "IGNITION STANDBY",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Color.Orange;
            }

            if (string.Equals(
                    command,
                    "IGNITE / HOLD MANEUVER VECTOR",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Color.Red;
            }

            if (string.Equals(
                    command,
                    "HOLD MANEUVER VECTOR",
                    StringComparison.OrdinalIgnoreCase))
            {
                return
                    GetAlignmentColor(
                        guidance);
            }
'@

if ($page.Contains($oldColors)) {
    $page =
        $page.Replace(
            $oldColors,
            $newColors)
}

WriteText $pagePath $page

# ===========================================================================
# 2. REGRESSION
# ===========================================================================

$testPath =
    Join-Path $RepoRoot "Tools\NavigationTests\Program.cs"

$tests =
    ReadText $testPath

if (-not $tests.Contains(
        'Run("GUID final count and ignition cues are explicit"')) {

    $anchor =
        '        Run("maneuver execution readiness is Engine-owned and explicit", ManeuverExecutionReadinessRegression);'

    if (-not $tests.Contains($anchor)) {
        throw "Could not locate 14.22.74 test anchor."
    }

    $tests =
        $tests.Replace(
            $anchor,
            $anchor + "`r`n" +
            '        Run("GUID final count and ignition cues are explicit", GuidanceFinalCountCueRegression);')
}

if (-not $tests.Contains(
        "private static void GuidanceFinalCountCueRegression()")) {

    $methodAnchor =
        "    private static void ManeuverUplinkDesiredPeriapsis()"

    if (-not $tests.Contains($methodAnchor)) {
        throw "Could not locate NavigationTests method insertion point."
    }

    $method = @'
    private static void GuidanceFinalCountCueRegression()
    {
        string enginePath =
            Path.Combine(
                Environment.CurrentDirectory,
                "KMC.Engine",
                "Guidance",
                "GuidanceSystem.cs");

        string pagePath =
            Path.Combine(
                Environment.CurrentDirectory,
                "KMC.MissionControl",
                "Pages",
                "GuidancePage.cs");

        Check(
            File.Exists(enginePath) &&
            File.Exists(pagePath),
            "final-count regression source unavailable");

        string engine =
            File.ReadAllText(
                enginePath);

        string page =
            File.ReadAllText(
                pagePath);

        /*
         * The Engine remains the owner of the timing thresholds/states.
         * The page only renders those states.
         */
        Check(
            engine.IndexOf(
                "IgnitionStandbySeconds = 10.0",
                StringComparison.Ordinal) >= 0 &&
            engine.IndexOf(
                "\"FINAL COUNT\"",
                StringComparison.Ordinal) >= 0 &&
            engine.IndexOf(
                "\"IGNITION DUE\"",
                StringComparison.Ordinal) >= 0,
            "Engine final-count/ignition states changed or missing");

        Check(
            page.IndexOf(
                "FormatIgnitionCountdown",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "\"IGNITION STANDBY\"",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "\"T- \"",
                StringComparison.Ordinal) >= 0,
            "GUID final countdown cue missing");

        Check(
            page.IndexOf(
                "\"IGNITE NOW\"",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "\"HOLD MANEUVER VECTOR\"",
                StringComparison.Ordinal) >= 0,
            "GUID ignition-due crew cue missing");

        Check(
            page.IndexOf(
                "Color.Orange",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "Color.Red",
                StringComparison.Ordinal) >= 0,
            "final-count / ignition alert colors missing");
    }


'@

    $tests =
        $tests.Replace(
            $methodAnchor,
            $method + $methodAnchor)
}

WriteText $testPath $tests

Write-Host "Applied KMC 14.22.75 Final Count / Ignition Cue."
Write-Host "Engine timing and burn-start logic are unchanged."
Write-Host "GUID now renders an explicit T-minus final count and IGNITE NOW alert."
