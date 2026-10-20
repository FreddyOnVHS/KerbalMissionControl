$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$adapterPath = Join-Path $root 'KMC.MissionControl\Navigation\OrbitMapNavigationAdapter.cs'
$mapPath = Join-Path $root 'KMC.MissionControl\Pages\MapPage.cs'
if (!(Test-Path $adapterPath) -or !(Test-Path $mapPath)) { throw 'Run from KMC repository root.' }
$a = [IO.File]::ReadAllText($adapterPath)
$m = [IO.File]::ReadAllText($mapPath)
$a = $a.Replace("`r`n", "`n")
$m = $m.Replace("`r`n", "`n")
if ($a.Contains('TryPlanMoonPreview(') -or $m.Contains('DrawMoonTransferPreview(')) { throw 'Moon preview already installed; no changes made.' }
$adapterAnchor = '        private static TransferSearchRequest CreateTransferSearchRequest('
$mapAnchor = '        private void ResetLambertPreview()'
$mapFieldAnchor = '        private ManeuverUplinkPacket _transferNodeCandidate;'
$oldBranch = @'
                        context.Graphics.DrawString(
                            "WINDOW SOLUTION NOT AVAILABLE",
'@
$newBranch = @'
                        if (MoonTransferRoute.Classify(origin, destination.Name,
                            destination.ParentName) == TransferRouteKind.SamePrimaryMoon)
                        {
                            // Never expose stale interplanetary node authority on moon route.
                            ResetLambertPreview();
                            DrawMoonTransferPreview(context, packet, destination,
                                x, ref y, transferLineHeight);
                        }
                        else
                        {
                        context.Graphics.DrawString(
                            "WINDOW SOLUTION NOT AVAILABLE",
'@
$oldClose = @'
                                : "REQUIRES VALID ELLIPTIC BODY ORBITS",
                            context.SmallFont,
                            dim,
                            x,
                            y);
                    }
                }

                DrawTransferNodeControls(
'@
$newClose = @'
                                : "REQUIRES VALID ELLIPTIC BODY ORBITS",
                            context.SmallFont,
                            dim,
                            x,
                            y);
                        }
                    }
                }

                DrawTransferNodeControls(
'@
foreach ($entry in @(@($a,$adapterAnchor),@($m,$mapAnchor),@($m,$mapFieldAnchor),@($m,$oldBranch),@($m,$oldClose))) {
  if (!$entry[0].Contains($entry[1])) { throw ('Baseline source anchor missing: ' + $entry[1].Substring(0,[Math]::Min(65,$entry[1].Length))) }
}
$a = $a.Replace($adapterAnchor, @'

        // 14.24.6: explicit moon route; never use interplanetary ejection here.
        // Preview only. The caller controls caching and never uplinks this result.
        public static bool TryPlanMoonPreview(
            OrbitMapPacket packet, OrbitMapBody moon, double requestedPeRadius,
            out MoonTransferPlan plan)
        {
            plan = null;
            if (packet == null || moon == null || packet.ActiveOrbit == null ||
                MoonTransferRoute.Classify(packet.ReferenceBodyName, moon.Name,
                    moon.ParentName) != TransferRouteKind.SamePrimaryMoon ||
                !IsFinitePositive(requestedPeRadius)) return false;
            double primaryMu = double.NaN;
            if (packet.Bodies != null)
                for (int i = 0; i < packet.Bodies.Count; i++)
                    if (packet.Bodies[i] != null &&
                        string.Equals(packet.Bodies[i].Name, packet.ReferenceBodyName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        primaryMu = packet.Bodies[i].GravParameter;
                        break;
                    }
            if (!IsFinitePositive(primaryMu)) return false;
            CelestialBodyState target = ToBody(moon);
            OrbitalElements craft = ToElements(packet.ActiveOrbit);
            double now = packet.UniversalTimeSeconds;
            if (craft == null || target == null || target.Orbit == null ||
                !IsFinitePositive(target.Orbit.SemiMajorAxisMeters) ||
                !IsFinitePositive(craft.SemiMajorAxisMeters) ||
                !IsFinitePositive(now + 1.0)) return false;
            double a = craft.SemiMajorAxisMeters;
            double b = target.Orbit.SemiMajorAxisMeters;
            // Half-ellipse is an initialization scale, not an encounter proof.
            double halfPeriod = Math.PI * Math.Sqrt(
                Math.Pow((a + b) * 0.5, 3) / primaryMu);
            if (!IsFinitePositive(halfPeriod)) return false;
            double targetPeriod = target.Orbit.PeriodSeconds;
            if (!IsFinitePositive(targetPeriod))
                targetPeriod = 2.0 * Math.PI *
                    Math.Sqrt(b * b * b / primaryMu);
            if (!IsFinitePositive(targetPeriod)) return false;
            return MoonTransferPlanner.TryPlan(craft, target, primaryMu,
                requestedPeRadius, now + 60.0,
                now + Math.Min(targetPeriod, 3.0 * halfPeriod),
                Math.Max(600.0, 0.55 * halfPeriod),
                1.65 * halfPeriod, 9, 9, 8, out plan);
        }
'@ + "`r`n" + $adapterAnchor)
$m = $m.Replace($mapFieldAnchor, $mapFieldAnchor + "`r`n" + @'

        // 14.24.6 cached moon preview; no node authorization.
        private MoonTransferPlan _moonPreview;
        private bool _moonPreviewAttempted;
        private string _moonPreviewKey = string.Empty;
'@)
$m = $m.Replace($mapAnchor, @'

        private void DrawMoonTransferPreview(MissionRenderContext context,
            OrbitMapPacket packet, OrbitMapBody destination, int x, ref int y,
            int lineHeight)
        {
            double pe = ResolveRequestedPeriapsisRadius(destination);
            if (!IsFinitePositive(pe)) pe = ComputeAutoTargetPeriapsisRadius(destination);
            double utBucket = Math.Floor(packet.UniversalTimeSeconds / 300.0);
            string key = (packet.VesselId ?? string.Empty) + "|" +
                destination.Name + "|" + utBucket.ToString("0") + "|" +
                pe.ToString("R") + "|" +
                (packet.ActiveOrbit != null ?
                    packet.ActiveOrbit.SemiMajorAxisMeters.ToString("R") : "-");
            if (!_moonPreviewAttempted || !string.Equals(_moonPreviewKey, key,
                    StringComparison.Ordinal))
            {
                _moonPreviewKey = key;
                _moonPreviewAttempted = true;
                _moonPreview = null;
                MoonTransferPlan planned;
                if (OrbitMapNavigationAdapter.TryPlanMoonPreview(
                        packet, destination, pe, out planned))
                    _moonPreview = planned;
            }
            using (SolidBrush bright = new SolidBrush(context.PhosphorColor))
            using (SolidBrush dim = new SolidBrush(context.DimPhosphorColor))
            {
                context.Graphics.DrawString("MOON TRANSFER / SAME PRIMARY",
                    context.SmallFont, bright, x, y); y += lineHeight;
                if (_moonPreview == null)
                    context.Graphics.DrawString("NO VERIFIED MOON PLAN IN SEARCH WINDOW",
                        context.SmallFont, dim, x, y);
                else
                {
                    MoonPeriapsisCorrection c = _moonPreview.Correction;
                    context.Graphics.DrawString("VERIFIED SOI / PE ERROR " +
                        c.Encounter.PeriapsisErrorMeters.ToString("0") + " m",
                        context.SmallFont, bright, x, y); y += lineHeight;
                    context.Graphics.DrawString("DEPART DV " +
                        c.DepartureDeltaVMetersPerSecond.ToString("0.0") + " m/s",
                        context.SmallFont, bright, x, y); y += lineHeight;
                    context.Graphics.DrawString("DEPART IN " +
                        FormatTransferInterval(Math.Max(0,
                            c.Injection.DepartureUniversalTimeSeconds -
                            packet.UniversalTimeSeconds)),
                        context.SmallFont, dim, x, y);
                }
                y += lineHeight;
                context.Graphics.DrawString("PREVIEW ONLY - NODE CREATION NOT ENABLED",
                    context.SmallFont, dim, x, y);
            }
        }
'@ + "`r`n" + $mapAnchor)
$m = $m.Replace($oldBranch,$newBranch).Replace($oldClose,$newClose)
$backup = Join-Path $env:TEMP ('KMC_14_24_6_backup_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
New-Item -ItemType Directory -Path $backup | Out-Null
Copy-Item $adapterPath (Join-Path $backup 'OrbitMapNavigationAdapter.cs')
Copy-Item $mapPath (Join-Path $backup 'MapPage.cs')
$utf8 = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllText($adapterPath, $a, $utf8)
[IO.File]::WriteAllText($mapPath, $m, $utf8)
Write-Host '14.24.6 moon MAP preview applied. No KSP DLL replacement.'
Write-Host "Backup: $backup"
Write-Host 'Build MissionControl and run NavigationTests before testing MAP.'
