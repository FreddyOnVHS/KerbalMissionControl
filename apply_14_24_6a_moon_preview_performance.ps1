$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$path = Join-Path $root 'KMC.MissionControl\Pages\MapPage.cs'
if (!(Test-Path $path)) { throw 'Run from the KMC repository root.' }
$m = [IO.File]::ReadAllText($path).Replace("`r`n", "`n")
if (!$m.Contains('private void DrawMoonTransferPreview(') -or !$m.Contains('private MoonTransferPlan _moonPreview;')) {
    throw 'Expected local 14.24.6 preview not found; no changes made.'
}
if ($m.Contains('CALCULATE MOON TRANSFER')) { throw '14.24.6a already applied; no changes made.' }
$oldUsing = 'using System.Windows.Forms;'
$oldFields = @'
        private MoonTransferPlan _moonPreview;
        private bool _moonPreviewAttempted;
        private string _moonPreviewKey = string.Empty;
'@
$newFields = @'
        private MoonTransferPlan _moonPreview;
        private string _moonPreviewKey = string.Empty;
        private string _moonPreviewStatus = "SELECT CALCULATE TO SEARCH";
        private Rectangle _moonCalculateButton = Rectangle.Empty;
        private OrbitMapPacket _moonPreviewPacket;
        private OrbitMapBody _moonPreviewDestination;
        private double _moonPreviewRadius = double.NaN;
        private volatile bool _moonPreviewRunning;
        private int _moonPreviewGeneration;
'@
$oldClick = @'
            if (_subpage == 1)
            {
                if (HasProductionNodeCandidate() &&
'@
$newClick = @'
            if (_subpage == 1)
            {
                if (!_moonCalculateButton.IsEmpty && _moonCalculateButton.Contains(q))
                {
                    StartMoonTransferCalculation();
                    return true;
                }
                if (HasProductionNodeCandidate() &&
'@
foreach ($entry in @(@($m,$oldUsing),@($m,$oldFields),@($m,$oldClick))) {
    if (!$entry[0].Contains($entry[1])) { throw ('14.24.6a expected anchor missing: ' + $entry[1].Substring(0,[Math]::Min(50,$entry[1].Length))) }
}
$startTag = '        private void DrawMoonTransferPreview('
$endTag = '        private void ResetLambertPreview()'
$start = $m.IndexOf($startTag, [StringComparison]::Ordinal)
$end = $m.IndexOf($endTag, $start, [StringComparison]::Ordinal)
if ($start -lt 0 -or $end -le $start) { throw 'Moon preview method boundaries missing; no changes made.' }
$replacement = @'
        // 14.24.6a: drawing never performs an orbital solve.
        private void DrawMoonTransferPreview(MissionRenderContext context,
            OrbitMapPacket packet, OrbitMapBody destination, int x, ref int y,
            int lineHeight)
        {
            double pe = ResolveRequestedPeriapsisRadius(destination);
            if (!IsFinitePositive(pe)) pe = ComputeAutoTargetPeriapsisRadius(destination);
            string key = (packet.VesselId ?? string.Empty) + "|" +
                (packet.ReferenceBodyName ?? string.Empty) + "|" +
                destination.Name + "|" +
                Math.Floor(packet.UniversalTimeSeconds / 600.0).ToString("0") + "|" +
                pe.ToString("R") + "|" +
                (packet.ActiveOrbit != null ?
                    packet.ActiveOrbit.SemiMajorAxisMeters.ToString("R") + "|" +
                    packet.ActiveOrbit.Eccentricity.ToString("R") + "|" +
                    packet.ActiveOrbit.InclinationDegrees.ToString("R") : "-");
            if (!string.Equals(_moonPreviewKey, key, StringComparison.Ordinal))
            {
                System.Threading.Interlocked.Increment(ref _moonPreviewGeneration);
                _moonPreviewKey = key;
                _moonPreview = null;
                _moonPreviewStatus = "SELECT CALCULATE TO SEARCH";
            }
            _moonPreviewPacket = packet;
            _moonPreviewDestination = destination;
            _moonPreviewRadius = pe;
            using (SolidBrush bright = new SolidBrush(context.PhosphorColor))
            using (SolidBrush dim = new SolidBrush(context.DimPhosphorColor))
            {
                context.Graphics.DrawString("MOON TRANSFER / SAME PRIMARY",
                    context.SmallFont, bright, x, y); y += lineHeight;
                if (_moonPreview != null)
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
                        context.SmallFont, dim, x, y); y += lineHeight;
                }
                else
                {
                    context.Graphics.DrawString(_moonPreviewRunning ?
                        "SEARCH IN PROGRESS..." : _moonPreviewStatus,
                        context.SmallFont, dim, x, y); y += lineHeight;
                }
                _moonCalculateButton = new Rectangle(x, y + 6, 245, 26);
                DrawButton(context, _moonCalculateButton, "CALCULATE MOON TRANSFER");
                y += lineHeight + 34;
                context.Graphics.DrawString("PREVIEW ONLY - NODE CREATION NOT ENABLED",
                    context.SmallFont, dim, x, y);
            }
        }

        private void StartMoonTransferCalculation()
        {
            if (_moonPreviewRunning || _moonPreviewPacket == null ||
                _moonPreviewDestination == null ||
                !IsFinitePositive(_moonPreviewRadius)) return;
            // Copy the raw protocol packet on the UI thread, so the worker does
            // not read a mutable telemetry snapshot belonging to the receiver.
            string snapshot = _moonPreviewPacket.Serialize();
            string destinationName = _moonPreviewDestination.Name;
            double pe = _moonPreviewRadius;
            string key = _moonPreviewKey;
            int generation = System.Threading.Interlocked.Increment(ref _moonPreviewGeneration);
            _moonPreviewRunning = true;
            _moonPreviewStatus = "SEARCH IN PROGRESS...";
            System.Threading.Tasks.Task.Run(() =>
            {
                MoonTransferPlan plan = null;
                string state = "NO VERIFIED MOON PLAN IN SEARCH WINDOW";
                try
                {
                    OrbitMapPacket packet;
                    if (OrbitMapPacket.TryParse(snapshot, out packet))
                    {
                        OrbitMapBody destination = null;
                        for (int i = 0; i < packet.Bodies.Count; i++)
                            if (packet.Bodies[i] != null &&
                                string.Equals(packet.Bodies[i].Name, destinationName,
                                    StringComparison.OrdinalIgnoreCase))
                            { destination = packet.Bodies[i]; break; }
                        if (destination != null &&
                            OrbitMapNavigationAdapter.TryPlanMoonPreview(
                                packet, destination, pe, out plan))
                            state = "VERIFIED PLAN AVAILABLE";
                    }
                }
                catch (Exception)
                {
                    state = "MOON PLAN CALCULATION FAILED";
                }
                if (System.Threading.Interlocked.CompareExchange(
                        ref _moonPreviewGeneration, generation, generation) == generation &&
                    string.Equals(_moonPreviewKey, key, StringComparison.Ordinal))
                {
                    _moonPreview = plan;
                    _moonPreviewStatus = state;
                }
                _moonPreviewRunning = false;
            });
        }

'@
# A light-weight synchronized state update is adequate for an experimental
# preview because MAP's normal FPS redraw reads the completed references.
$backup = Join-Path $env:TEMP ('KMC_14_24_6a_backup_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
New-Item -ItemType Directory -Path $backup | Out-Null
Copy-Item $path (Join-Path $backup 'MapPage.cs')
$m = $m.Substring(0, $start) + $replacement + $m.Substring($end)
$m = $m.Replace($oldFields, $newFields)
$m = $m.Replace($oldClick, $newClick)
# Discard stale button hit targets when switching back to a planet transfer.
$m = $m.Replace('            _transferNodeCandidate = null;', '            _transferNodeCandidate = null;' + "`n" + '            _moonCalculateButton = Rectangle.Empty;')
[IO.File]::WriteAllText($path, $m, (New-Object Text.UTF8Encoding($false)))
Write-Host '14.24.6a responsive moon planning patch applied (MissionControl only).'
Write-Host "Backup: $backup"
Write-Host 'No KSP DLL replacement needed. Build and run NavigationTests.'
