# KMC 14.23.6a - Conservative projected-hull occlusion prototype.
# NOT full depth-aware mesh visibility. Requires the local 14.23.5 patch.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$relative = 'KMC.MissionControl/Pages/SchematicPage.cs'
$file = Join-Path $root $relative
if (-not (Test-Path -LiteralPath $file)) { throw "Missing SCHEM page: $file" }
$head = (& git rev-parse HEAD 2>$null)
if ($LASTEXITCODE -ne 0 -or $head.Trim() -ne '1a574b6723b2f99f1aa5c587d6f368075ad74ef9') { throw 'Unexpected HEAD. Expected verified 14.23.4c base.' }
$source = [IO.File]::ReadAllText($file)
$nl = if ($source.Contains("`r`n")) { "`r`n" } else { "`n" }
function Replace-Once([string]$text,[string]$a,[string]$b,[string]$name) {
  $a = $a.Replace("`n",$nl); $b = $b.Replace("`n",$nl)
  $i = $text.IndexOf($a,[StringComparison]::Ordinal)
  if ($i -lt 0 -or $text.IndexOf($a,$i+$a.Length,[StringComparison]::Ordinal) -ge 0) { throw "Preflight anchor missing or ambiguous: $name" }
  return $text.Substring(0,$i)+$b+$text.Substring($i+$a.Length)
}
$anchor = @'
            /*
             * Paint back to front, independent of vessel.parts ordering.
'@
$insert = @'
            /*
             * 14.23.6a prototype: clip secondary detail from projected
             * primary-hull coverage. This is NOT physical near/far occlusion:
             * v4 outline telemetry has no per-triangle depth. In particular,
             * exterior surface parts inside the hull projection may be hidden.
             * Keep this change isolated for runtime comparison.
             */
            using (Region hullMask = new Region())
            {
                hullMask.MakeEmpty();
                for (int maskIndex = 0;
                     maskIndex < topology.Nodes.Count;
                     maskIndex++)
                {
                    VesselTopologyNode maskNode = topology.Nodes[maskIndex];
                    if (maskNode == null || !IsPrimaryHullPart(maskNode) ||
                        maskNode.SideOutlinePoints == null ||
                        maskNode.SideOutlinePoints.Count < 3)
                    {
                        continue;
                    }

                    PointF[] maskPoints = new PointF[maskNode.SideOutlinePoints.Count];
                    for (int pointIndex = 0; pointIndex < maskPoints.Length; pointIndex++)
                    {
                        VesselOutlinePoint p = maskNode.SideOutlinePoints[pointIndex];
                        maskPoints[pointIndex] = ToScreen(
                            p.Horizontal, p.Vertical, centerH, centerV,
                            scale, screenCenterX, screenCenterY);
                    }
                    using (GraphicsPath maskPath = new GraphicsPath())
                    {
                        maskPath.AddPolygon(maskPoints);
                        hullMask.Union(maskPath);
                    }
                }

                GraphicsState detailState = context.Graphics.Save();
                try
                {
                    context.Graphics.SetClip(hullMask, CombineMode.Exclude);
                    // Existing layer loop draws all details. Restore the clip
                    // before hull and engines so they remain fully visible.
                    DrawSchematicLayer(context, topology, 0, centerH, centerV,
                        scale, screenCenterX, screenCenterY);
                }
                finally
                {
                    context.Graphics.Restore(detailState);
                }
                DrawSchematicLayer(context, topology, 1, centerH, centerV,
                    scale, screenCenterX, screenCenterY);
                DrawSchematicLayer(context, topology, 2, centerH, centerV,
                    scale, screenCenterX, screenCenterY);
            }
        }

        private static void DrawSchematicLayer(
            MissionRenderContext context,
            VesselTopology topology,
            int requestedLayer,
            double centerH,
            double centerV,
            double scale,
            float screenCenterX,
            float screenCenterY)
        {
            /*
             * Paint back to front, independent of vessel.parts ordering.
'@
$end = @'
            for (int layer = 0; layer < 3; layer++)
            {
                for (int nodeIndex = 0;
'@
$endReplace = @'
            {
                int layer = requestedLayer;
                for (int nodeIndex = 0;
'@
$updated = Replace-Once $source $anchor $insert 'draw loop insertion'
$updated = Replace-Once $updated $end $endReplace 'layer loop entry'
# 14.23.5 has two closing braces for loop layers; after extraction only one needed.
$close = @'
                }
            }
        }

        private static void DrawPartOutline(
'@
$closeReplace = @'
                }
        }

        private static void DrawPartOutline(
'@
$updated = Replace-Once $updated $close $closeReplace 'layer loop closure'
if ($updated -eq $source) { throw 'No update produced.' }
# Write only after all preflight and transforms complete.
$tmp = Join-Path (Split-Path $file) ('.schem-14.23.6a-'+[guid]::NewGuid().ToString('N')+'.tmp')
try {
  [IO.File]::WriteAllText($tmp,$updated,(New-Object Text.UTF8Encoding($false)))
  Move-Item -LiteralPath $tmp -Destination $file -Force
} finally { if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Force } }
Write-Host '14.23.6a prototype applied. Run NavigationTests and rebuild Mission Control.' -ForegroundColor Green
Write-Warning 'This is projected-hull masking, NOT true depth-aware external surface extraction.'
