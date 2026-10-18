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

if (-not $page.Contains("private static void DrawEngineBell(")) {
    throw "14.23.4 engine-bell renderer not found."
}

$old = @'
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
'@

$new = @'
            float centerX =
                bounds.Left +
                bounds.Width * 0.5f;

            /*
             * Keep the bell inside the live engine envelope but use only the
             * central portion of that envelope. The raw engine mesh bounds
             * can include mount/housing geometry that made the schematic bell
             * look oversized.
             *
             * Also bias the bell upward so its throat visually attaches to
             * the engine mount instead of floating below the spacecraft.
             */
            float bellHeight =
                bounds.Height * 0.74f;

            float topY =
                bounds.Top -
                bounds.Height * 0.04f;

            float bottomY =
                topY +
                bellHeight;

            float throatY =
                topY +
                bellHeight * 0.28f;

            float topHalfWidth =
                Math.Max(
                    2.0f,
                    bounds.Width * 0.16f);

            float throatHalfWidth =
                Math.Max(
                    2.0f,
                    bounds.Width * 0.22f);

            float exitHalfWidth =
                Math.Max(
                    3.0f,
                    bounds.Width * 0.34f);
'@

if ($page.Contains($old)) {
    $page =
        $page.Replace(
            $old,
            $new)
}
elseif (-not $page.Contains("float bellHeight =")) {
    throw "Expected 14.23.4 bell sizing block not found. No file changed."
}

if (-not $tests.Contains('Run("SCHEM engine bell is compact and attached"')) {
    $anchor =
        '        Run("SCHEM presentation prioritizes hull and engine bell", VesselSchematicPresentationPolishRegression);'

    if (-not $tests.Contains($anchor)) {
        throw "Could not locate 14.23.4 regression registration."
    }

    $tests =
        $tests.Replace(
            $anchor,
            $anchor + "`r`n" +
            '        Run("SCHEM engine bell is compact and attached", VesselSchematicEngineBellScaleRegression);')
}

if (-not $tests.Contains("private static void VesselSchematicEngineBellScaleRegression()")) {
    $anchor =
        "    private static void ManeuverUplinkDesiredPeriapsis()"

    if (-not $tests.Contains($anchor)) {
        throw "Could not locate NavigationTests insertion point."
    }

    $method = @'
    private static void VesselSchematicEngineBellScaleRegression()
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
                "float bellHeight =",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "bounds.Height * 0.74f",
                StringComparison.Ordinal) >= 0,
            "SCHEM compact engine-bell height missing");

        Check(
            page.IndexOf(
                "bounds.Width * 0.34f",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "bounds.Width * 0.16f",
                StringComparison.Ordinal) >= 0,
            "SCHEM compact engine-bell width missing");

        Check(
            page.IndexOf(
                "bounds.Top -",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "bounds.Height * 0.04f",
                StringComparison.Ordinal) >= 0,
            "SCHEM engine-bell upward attachment bias missing");
    }


'@

    $tests =
        $tests.Replace(
            $anchor,
            $method + $anchor)
}

WriteText $pagePath $page
WriteText $testPath $tests

Write-Host "Applied KMC 14.23.4a Engine Bell Scale / Attach."
Write-Host "Engine bell is narrower, shorter, and biased upward toward the engine mount."
Write-Host "Mission Control only: KMC.Plugin.dll does NOT need replacement."
