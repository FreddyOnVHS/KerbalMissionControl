# KMC 14.23.5 - Deterministic schematic visual layering
# Apply at the repository root, based on the verified 14.23.4c master.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$file = Join-Path $root 'KMC.MissionControl/Pages/SchematicPage.cs'
if (-not (Test-Path -LiteralPath $file)) { throw "SCHEM source not found: $file. Run from KMC repo root." }
$head = (& git rev-parse HEAD 2>$null)
if ($LASTEXITCODE -ne 0) { throw 'Git repository could not be verified.' }
$expectedHead = '1a574b6723b2f99f1aa5c587d6f368075ad74ef9'
if ($head.Trim() -ne $expectedHead) { throw "Unexpected HEAD ($head). Expected $expectedHead. Do not apply to another revision." }
$tracked = (& git status --porcelain -- 'KMC.MissionControl/Pages/SchematicPage.cs')
if ($tracked) { throw 'SchematicPage.cs has uncommitted changes. Refusing to overwrite.' }
$encoding = New-Object System.Text.UTF8Encoding($false)
$source = [System.IO.File]::ReadAllText($file)
$newline = if ($source.Contains("`r`n")) { "`r`n" } else { "`n" }
$start = @'
            for (int nodeIndex = 0;
                 nodeIndex < topology.Nodes.Count;
                 nodeIndex++)
            {
                VesselTopologyNode node =
                    topology.Nodes[nodeIndex];

                if (node == null)
                {
                    continue;
                }

                bool isRoot =
'@
$replacement = @'
            /*
             * Paint back to front, independent of vessel.parts ordering.
             * Secondary detail goes underneath the hull, and engine bells
             * remain legible above it. Geometry and telemetry are unchanged.
             */
            for (int layer = 0; layer < 3; layer++)
            {
                for (int nodeIndex = 0;
                     nodeIndex < topology.Nodes.Count;
                     nodeIndex++)
                {
                    VesselTopologyNode node =
                        topology.Nodes[nodeIndex];

                    if (node == null)
                    {
                        continue;
                    }

                    int partLayer =
                        node.Category == VesselNodeCategory.Engine
                            ? 2
                            : IsPrimaryHullPart(node)
                                ? 1
                                : 0;

                    if (partLayer != layer)
                    {
                        continue;
                    }

                    bool isRoot =
'@
$end = @'
                    DrawFallbackGlyph(
                        context,
                        center,
                        node,
                        isRoot);
                }
            }
        }

        private static void DrawPartOutline(
'@
$endReplacement = @'
                    DrawFallbackGlyph(
                        context,
                        center,
                        node,
                        isRoot);
                }
                }
            }
        }

        private static void DrawPartOutline(
'@
function Replace-Once([string]$text, [string]$old, [string]$new, [string]$label) {
    $old = $old.Replace("`n", $newline)
    $new = $new.Replace("`n", $newline)
    $one = $text.IndexOf($old, [StringComparison]::Ordinal)
    if ($one -lt 0) { throw "Preflight failed: $label anchor missing." }
    if ($text.IndexOf($old, $one + $old.Length, [StringComparison]::Ordinal) -ge 0) { throw "Preflight failed: $label anchor ambiguous." }
    return $text.Substring(0, $one) + $new + $text.Substring($one + $old.Length)
}
# All changes happen in memory. No file is written if either anchor fails.
$updated = Replace-Once $source $start $replacement 'render loop start'
$updated = Replace-Once $updated $end $endReplacement 'render loop end'
if ($updated -eq $source) { throw 'No changes generated.' }
# Move into place only after constructing a full replacement.
$temp = Join-Path (Split-Path $file) ('.schematic-14.23.5-' + [guid]::NewGuid().ToString('N') + '.tmp')
try {
    [System.IO.File]::WriteAllText($temp, $updated, $encoding)
    Move-Item -LiteralPath $temp -Destination $file -Force
} finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Force }
}
Write-Host 'KMC 14.23.5 applied: secondary -> hull -> engine draw ordering.' -ForegroundColor Green
Write-Host 'Run: powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1'
Write-Host 'Rebuild KMC.MissionControl. No KMC.Plugin.dll replacement required.'
