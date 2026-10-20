$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$map = Join-Path $root 'KMC.MissionControl\Pages\MapPage.cs'
$adapter = Join-Path $root 'KMC.MissionControl\Navigation\OrbitMapNavigationAdapter.cs'
$tests = Join-Path $root 'Tools\NavigationTests\Program.cs'
foreach ($file in @($map,$adapter,$tests)) { if (!(Test-Path $file)) { throw "Missing $file; run from KMC root." } }
$enc = New-Object System.Text.UTF8Encoding($false)
$m = [IO.File]::ReadAllText($map)
$a = [IO.File]::ReadAllText($adapter)
$t = [IO.File]::ReadAllText($tests)
$m = $m.Replace("`r`n","`n")
$a = $a.Replace("`r`n","`n")
$t = $t.Replace("`r`n","`n")
function ReplaceOnce([string]$source,[string]$needle,[string]$replacement) {
    $replacement = $replacement.Replace('`n', "`n")
    if (!$source.Contains($needle)) { throw "Preflight anchor not found: $needle" }
    if ($source.IndexOf($needle) -ne $source.LastIndexOf($needle)) { throw "Nonunique anchor: $needle" }
    return $source.Replace($needle,$replacement)
}
if ($m.Contains('private void UploadMoonTransferNode()') -or $a.Contains('TryBuildMoonNodePacket(')) { throw '14.24.7 already applied.' }
# Require production node path and moon preview already present.
foreach ($anchor in @('private void StartMoonTransferCalculation()', 'private void UploadTransferNode()', 'private bool HasProductionNodeCandidate()')) {
    if (!$m.Contains($anchor)) { throw "Incompatible MAP baseline: $anchor" }
}
$helper = @'
        // 14.24.7: same-primary direct-injection P/N/R projection.
        // The Engine's inertial frame is the same reference used by
        // KeplerPropagator. No hyperbolic departure or Unity axis swap.
        public static bool TryBuildMoonNodePacket(
            OrbitMapPacket source, MoonTransferPlan plan, string destinationName,
            string planId, out ManeuverUplinkPacket packet)
        {
            packet = null;
            if (source == null || source.ActiveOrbit == null ||
                source.Bodies == null || string.IsNullOrWhiteSpace(source.VesselId) ||
                string.IsNullOrWhiteSpace(planId) ||
                string.IsNullOrWhiteSpace(destinationName) ||
                plan == null || plan.Correction == null ||
                plan.Correction.Injection == null ||
                plan.Correction.Encounter == null ||
                !plan.Correction.Encounter.EntersSoi ||
                plan.Correction.Encounter.SurfaceImpact ||
                !plan.Correction.Encounter.RequestedPeriapsisSatisfied)
                return false;
            CelestialBodyState moon = null;
            CelestialBodyState primary = null;
            for (int i = 0; i < source.Bodies.Count; i++)
            {
                OrbitMapBody body = source.Bodies[i];
                if (body == null) continue;
                if (string.Equals(body.Name, destinationName,
                    StringComparison.OrdinalIgnoreCase)) moon = ToBody(body);
                if (string.Equals(body.Name, source.ReferenceBodyName,
                    StringComparison.OrdinalIgnoreCase)) primary = ToBody(body);
            }
            if (moon == null || primary == null ||
                MoonTransferRoute.Classify(source.ReferenceBodyName,
                    moon.Name, moon.ParentName) != TransferRouteKind.SamePrimaryMoon ||
                !IsFinitePositive(primary.GravParameter)) return false;
            MoonTransferCandidate injection = plan.Correction.Injection;
            double ut = injection.DepartureUniversalTimeSeconds;
            if (double.IsNaN(ut) || double.IsInfinity(ut) ||
                ut <= source.UniversalTimeSeconds + 1.0 ||
                ut >= injection.ArrivalUniversalTimeSeconds ||
                !injection.DeltaV.IsFinite ||
                !IsFinitePositive(injection.DeltaVMetersPerSecond)) return false;
            StateVector state;
            if (!KeplerPropagator.TryPropagate(ToElements(source.ActiveOrbit),
                primary.GravParameter, ut, out state)) return false;
            Vector3d p = state.Velocity;
            Vector3d n = Vector3d.Cross(state.Position, state.Velocity);
            if (!IsFinitePositive(p.Magnitude) || !IsFinitePositive(n.Magnitude))
                return false;
            p = p * (1.0 / p.Magnitude);
            n = n * (1.0 / n.Magnitude);
            Vector3d r = Vector3d.Cross(p, n);
            if (!IsFinitePositive(r.Magnitude)) return false;
            r = r * (1.0 / r.Magnitude);
            double prograde = Vector3d.Dot(injection.DeltaV, p);
            double normal = Vector3d.Dot(injection.DeltaV, n);
            double radial = Vector3d.Dot(injection.DeltaV, r);
            Vector3d reconstructed = p * prograde + n * normal + r * radial;
            if (!reconstructed.IsFinite ||
                (reconstructed - injection.DeltaV).Magnitude >
                    Math.Max(1e-6, 1e-8 * injection.DeltaVMetersPerSecond))
                return false;
            double desiredPe = plan.Correction.Encounter.RequestedPeriapsisRadiusMeters;
            if (!IsFinitePositive(desiredPe) ||
                desiredPe <= moon.RadiusMeters || desiredPe >= moon.SoiRadiusMeters)
                return false;
            packet = new ManeuverUplinkPacket {
                VesselId = source.VesselId,
                PlanId = planId,
                NodeUniversalTimeSeconds = ut,
                ProgradeDeltaVMetersPerSecond = prograde,
                NormalDeltaVMetersPerSecond = normal,
                RadialDeltaVMetersPerSecond = radial,
                DesiredPeriapsisRadiusMeters = desiredPe,
                TargetBodyName = destinationName,
                Operation = "CREATE"
            };
            return true;
        }

'@
$moonUpload = @'
        private void UploadMoonTransferNode()
        {
            if (_moonPreview == null || _moonPreviewRunning ||
                _moonPreviewPacket == null || _moonSolvedPacket == null ||
                _moonPreviewDestination == null ||
                !string.Equals(_moonPreviewDestination.Name,
                    _selectedTransferBodyName, StringComparison.OrdinalIgnoreCase))
            {
                _transferNodeActionText = "CALCULATE VERIFIED MOON PLAN FIRST";
                return;
            }
            string planId = "MAP-MOON-" +
                SanitizePlanToken(_selectedTransferBodyName) + "-" +
                Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
            if (_moonPreview.Correction.Injection.DepartureUniversalTimeSeconds <=
                    _moonPreviewPacket.UniversalTimeSeconds + 10.0 ||
                _moonPreviewPacket.ActiveOrbit == null ||
                _moonSolvedPacket.ActiveOrbit == null ||
                Math.Abs(_moonPreviewPacket.ActiveOrbit.SemiMajorAxisMeters -
                    _moonSolvedPacket.ActiveOrbit.SemiMajorAxisMeters) >
                    Math.Abs(_moonSolvedPacket.ActiveOrbit.SemiMajorAxisMeters) * 0.001 ||
                Math.Abs(_moonPreviewPacket.ActiveOrbit.InclinationDegrees -
                    _moonSolvedPacket.ActiveOrbit.InclinationDegrees) > 0.1)
            {
                _transferNodeActionText = "MOON PLAN EXPIRED / RE-CALCULATE";
                return;
            }
            ManeuverUplinkPacket packet;
            if (!OrbitMapNavigationAdapter.TryBuildMoonNodePacket(
                    _moonSolvedPacket, _moonPreview, _selectedTransferBodyName,
                    planId, out packet) ||
                !string.Equals(packet.VesselId, _currentTransferVesselId,
                    StringComparison.OrdinalIgnoreCase))
            {
                _transferNodeActionText = "MOON NODE STALE / RE-CALCULATE";
                return;
            }
            string response;
            bool sent = TransferPlannerManeuverUplink.Send(packet, out response);
            _transferNodeActionText = string.IsNullOrWhiteSpace(response)
                ? (sent ? "MOON NODE UPLINK SENT" : "MOON NODE UPLINK FAILED")
                : response;
            if (!sent) return;
            _lastTransferPlanId = planId;
            _submittedTransferDestinationName = _selectedTransferBodyName;
            _submittedTransferNodeUt = packet.NodeUniversalTimeSeconds;
            _submittedTransferProgradeDv = packet.ProgradeDeltaVMetersPerSecond;
            _submittedTransferNormalDv = packet.NormalDeltaVMetersPerSecond;
            _submittedTransferRadialDv = packet.RadialDeltaVMetersPerSecond;
            _submittedTransferUsedCoupledAuthority = false;
            _submittedTransferUsedLambertAuthority = false;
            TransferManeuverPlanBridge.RegisterAndPromote(
                packet, _selectedTransferBodyName);
        }

'@
$newTest = @'
    private static void MoonNodeFrameProjectionRegression()
    {
        // The engine's inertial burn vector must survive P/N/R projection
        // even in an inclined orbit. An invalid or stale plan must be rejected.
        const double mu = 3.5316e12;
        OrbitalElements craft = new OrbitalElements {
            ReferenceBodyName = "Kerbin", SemiMajorAxisMeters = 700000,
            Eccentricity = 0, InclinationDegrees = 12,
            LongitudeOfAscendingNodeDegrees = 40,
            ArgumentOfPeriapsisDegrees = 0,
            EpochUniversalTimeSeconds = 0, MeanAnomalyAtEpochRadians = 0
        };
        StateVector orbitState;
        Check(KeplerPropagator.TryPropagate(craft, mu, 5000, out orbitState),
            "moon PNR basis propagation rejected");
        Vector3d p = orbitState.Velocity * (1.0 / orbitState.Velocity.Magnitude);
        Vector3d n0 = Vector3d.Cross(orbitState.Position, orbitState.Velocity);
        Vector3d n = n0 * (1.0 / n0.Magnitude);
        Vector3d r = Vector3d.Cross(p, n);
        Vector3d impulse = p * 780 + n * 53 + r * (-18);
        double pp = Vector3d.Dot(impulse, p);
        double nn = Vector3d.Dot(impulse, n);
        double rr = Vector3d.Dot(impulse, r);
        Check((p * pp + n * nn + r * rr - impulse).Magnitude < 1e-8 &&
            Math.Abs(pp - 780) < 1e-8 && Math.Abs(nn - 53) < 1e-8 &&
            Math.Abs(rr + 18) < 1e-8,
            "moon PNR frame lost 3D burn components");
        ManeuverUplinkPacket rejected;
        Check(!OrbitMapNavigationAdapter.TryBuildMoonNodePacket(
            new OrbitMapPacket(), null, "Mun", "TEST", out rejected) &&
            rejected == null, "moon node adapter accepted unverified plan");
    }

'@

$a = ReplaceOnce $a '        private static bool IsFinitePositive(double value)' ($helper + '        private static bool IsFinitePositive(double value)')
$m = ReplaceOnce $m '        private OrbitMapPacket _moonPreviewPacket;' '        private OrbitMapPacket _moonPreviewPacket;`n        private OrbitMapPacket _moonSolvedPacket;'
$m = ReplaceOnce $m '                _moonPreview = null;' '                _moonPreview = null;`n                _moonSolvedPacket = null;'
$m = ReplaceOnce $m '                MoonTransferPlan plan = null;' '                MoonTransferPlan plan = null;`n                OrbitMapPacket solvedSnapshot = null;'
$m = ReplaceOnce $m '                            state = "VERIFIED PLAN AVAILABLE";' '                        {`n                            state = "VERIFIED PLAN AVAILABLE";`n                            solvedSnapshot = packet;`n                        }'
$m = ReplaceOnce $m '                    _moonPreview = plan;' '                    _moonSolvedPacket = solvedSnapshot;`n                    _moonPreview = plan;'
$m = ReplaceOnce $m '        private void UploadTransferNode()' ($moonUpload + '        private void UploadTransferNode()')
$m = ReplaceOnce $m '        private void UploadTransferNode()
        {' '        private void UploadTransferNode()
        {
            if (_moonPreview != null && _moonPreviewDestination != null &&
                MoonTransferRoute.Classify(_moonPreviewPacket.ReferenceBodyName,
                    _moonPreviewDestination.Name, _moonPreviewDestination.ParentName) ==
                    TransferRouteKind.SamePrimaryMoon)
            {
                UploadMoonTransferNode();
                return;
            }'
$m = ReplaceOnce $m '        private bool HasProductionNodeCandidate()
        {' '        private bool HasProductionNodeCandidate()
        {
            if (_moonPreview != null && !_moonPreviewRunning &&
                _moonPreviewDestination != null && _moonPreviewPacket != null &&
                MoonTransferRoute.Classify(_moonPreviewPacket.ReferenceBodyName,
                    _moonPreviewDestination.Name, _moonPreviewDestination.ParentName) ==
                    TransferRouteKind.SamePrimaryMoon)
                return true;'
$m = ReplaceOnce $m 'context.Graphics.DrawString("PREVIEW ONLY - NODE CREATION NOT ENABLED",' 'context.Graphics.DrawString(_moonPreview != null ?
                    "VERIFIED MOON PLAN - CREATE KSP NODE BELOW" :
                    "CALCULATE VERIFIED PLAN BEFORE NODE CREATION",'
$m = ReplaceOnce $m '"ROUTE  " + (relation == "SAME PARENT"
                            ? "DIRECT PLANET TRANSFER"
                            : "HIERARCHY CHANGE")' '"ROUTE  " + (MoonTransferRoute.Classify(origin, destination.Name,
                            destination.ParentName) == TransferRouteKind.SamePrimaryMoon
                            ? "SAME-PRIMARY MOON TRANSFER"
                            : (relation == "SAME PARENT"
                                ? "DIRECT PLANET TRANSFER"
                                : "HIERARCHY CHANGE"))'
# Do not reuse a plan from another selection while the key has changed.
# Periapsis request and destination are already checked by DrawMoonTransferPreview.
$t = ReplaceOnce $t '        Run("Lambert Vallado 3D reference case", LambertVallado);' '        Run("moon maneuver node PNR basis and verification guard", MoonNodeFrameProjectionRegression);
        Run("Lambert Vallado 3D reference case", LambertVallado);'
$t = ReplaceOnce $t '    private static void SchematicAngleSelectionRegression()' ($newTest + '    private static void SchematicAngleSelectionRegression()')
$backup = Join-Path $env:TEMP ('KMC_14_24_7_backup_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
New-Item -ItemType Directory -Force -Path $backup | Out-Null
Copy-Item $map (Join-Path $backup 'MapPage.cs')
Copy-Item $adapter (Join-Path $backup 'OrbitMapNavigationAdapter.cs')
Copy-Item $tests (Join-Path $backup 'Program.cs')
[IO.File]::WriteAllText($adapter,$a,$enc)
[IO.File]::WriteAllText($map,$m,$enc)
[IO.File]::WriteAllText($tests,$t,$enc)
Write-Host 'KMC 14.24.7 experimental moon node uplink patch applied.'
Write-Host "Backup: $backup"
Write-Host 'Build MissionControl and run NavigationTests. No KSP Plugin/Shared DLL replacement required.'
