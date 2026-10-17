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
        /*
         * Legacy 10-field node remains accepted.
         */
        string extendedNode =
            string.Join(
                "~",
                new[]
                {
                    "0",
                    "5000",
                    "1000",
                    "-100",
                    "20",
                    "1005.18654985",
                    "1",
                    "2",
                    "3",
                    "0",
                    "1",
                    "0.25",
                    "0.75",
                    Math.Sqrt(0.375).ToString(
                        "R",
                        System.Globalization.CultureInfo.InvariantCulture)
                });

        string legacyNode =
            string.Join(
                "~",
                extendedNode.Split('~'),
                0,
                10);

        string legacyPacket =
            serialized.Replace(
                extendedNode,
                legacyNode);

        Check(
            OrbitMapPacket.TryParse(
                legacyPacket,
                out parsed),
            "legacy 10-field OrbitMap maneuver node no longer parses");

        Check(
            parsed.ManeuverNodes.Count == 1 &&
            !parsed.ManeuverNodes[0].AuthoritativeBurnVectorAvailable,
            "legacy maneuver node invented authoritative vector");
'@

$new = @'
        /*
         * Legacy 10-field node remains accepted.
         *
         * Reuse the packet we just serialized, but structurally trim only the
         * first maneuver-node entry from the new 14-field layout back to the
         * pre-14.22.71 10-field layout. This avoids brittle floating-point
         * substring replacement.
         */
        string[] packetFields =
            serialized.Split('|');

        int nodeCountIndex =
            12 +
            packet.Bodies.Count;

        int firstNodeIndex =
            nodeCountIndex + 1;

        string[] nodeFields =
            packetFields[firstNodeIndex].Split('~');

        Check(
            nodeFields.Length == 14,
            "extended maneuver node did not serialize to 14 fields");

        packetFields[firstNodeIndex] =
            string.Join(
                "~",
                nodeFields,
                0,
                10);

        string legacyPacket =
            string.Join(
                "|",
                packetFields);

        Check(
            OrbitMapPacket.TryParse(
                legacyPacket,
                out parsed),
            "legacy 10-field OrbitMap maneuver node no longer parses");

        Check(
            parsed.ManeuverNodes.Count == 1 &&
            !parsed.ManeuverNodes[0].AuthoritativeBurnVectorAvailable,
            "legacy maneuver node invented authoritative vector");
'@

if (-not $text.Contains($old)) {
    throw "Could not locate 14.22.71 legacy maneuver-node regression block."
}

$text =
    $text.Replace(
        $old,
        $new)

[System.IO.File]::WriteAllText(
    $path,
    $text,
    [System.Text.UTF8Encoding]::new($false))

Write-Host "Applied KMC 14.22.71b OrbitMap legacy-node regression fix."
