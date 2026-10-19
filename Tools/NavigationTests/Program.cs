using System;
using System.Globalization;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using KMC.Engine.CelestialMechanics;
using KMC.Engine.Navigation;
using KMC.Engine.Orbit;
using KMC.MissionControl.Navigation;
using KMC.MissionControl.Rendering.OrbitMap;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Pages;
using KMC.MissionControl.Telemetry;
using KMC.Shared;
using KMC.Shared.Topology;
internal static class Program
{
    private static int passed, failed;
    private static void Main()
    {
        Run("circular quarter period and backwards UT", Circular);
        Run("elliptic periapsis and apoapsis", EllipticApsides);
        Run("hyperbolic known anomaly and time symmetry", Hyperbolic);
        Run("inclined and retrograde frame orientation", Orientation);
        Run("high eccentricity and conserved quantities", Conservation);
        Run("invalid propagation returns no state", InvalidPropagation);
        Run("renderer position agreement on supported domain", RendererAgreement);
        Run("legacy transfer and ejection numerical parity (8 fixtures)", LegacyParity);
        Run("planner domain limits and missing data", PlannerLimits);
        Run("nonfinite planner inputs never yield a candidate", InvalidPlanner);
        Run("MAP draws both tabs and constructs the preserved maneuver", MapIntegration);
        Run("planner rejects negative elliptic axes", NegativeEllipticAxes);
        Run("planner rejects conflicting reference frames", ConflictingFrames);
        Run("Lambert canonical short/long zero-revolution arcs", LambertCanonical);
        Run("same-primary moon Lambert injection bootstrap", MoonTransferFoundationRegression);
        Run("Lambert Vallado 3D reference case", LambertVallado);
        Run("Lambert rejects invalid and degenerate boundary data", LambertInvalid);
        Run("MAP adapter builds Lambert preview from body telemetry", LambertMapPreviewAdapter);
        Run("MAP adapter builds Lambert parking-ejection preview", LambertMapEjectionPreviewAdapter);
        Run("MAP adapter ranks Lambert candidates by parking ejection DV", ParkingAwareLambertAdapter);
        Run("MAP production cleanup removes temporary Lambert test path", ProductionCleanup);
        Run("finite SOI assessment returns a real boundary state", FiniteSoiBoundaryAssessment);
        Run("finite SOI local correction never worsens boundary state", FiniteSoiLocalCorrection);
        Run("target SOI shooting establishes encounter or improves miss distance", TargetSoiShooting);
        Run("target SOI shooting shapes a safe target periapsis", TargetSoiSafePeriapsis);
        Run("target SOI shooting reports rejection diagnostics", TargetSoiDiagnostics);
        Run("analytic hyperbolic SOI exit handles arbitrary 3D escape", HyperbolicSoiExit);
        Run("target SOI optimization keeps DV as constrained objective", TargetSoiOptimizationPolicyRegression);
        Run("target B-plane maps desired periapsis to impact parameter", TargetBPlaneRegression);
        Run("target B-plane bootstrap constructs incoming SOI entry", TargetBPlaneBootstrapRegression);
        Run("target terminal solve initializes from bounded B-plane bootstrap", TargetBPlaneTerminalInitializationRegression);
        Run("target terminal solve preserves feasible encounter incumbent", TargetBPlaneIncumbentRegression);
        Run("target terminal search varies B-plane azimuth and arrival epoch", TargetBPlaneConstraintSearchRegression);
        Run("3D direct shooting bootstrap survives parking-aware rejection", DirectShootingBootstrap);
        Run("same-parent planetary geometry regression matrix", PlanetGeometryRegressionMatrix);
        Run("MAP production node prefers coupled PNR, then Lambert, then legacy fallback", ProductionAuthorityPacket);
        Run("coupled production API consolidates shadow compatibility", CoupledProductionApiConsolidation);
        Run("operator periapsis override exposes production solve input", OperatorTargetPeriapsis);
        Run("maneuver uplink carries optional desired periapsis", ManeuverUplinkDesiredPeriapsis);
        Run("mission time displays use days hours minutes seconds", HumanReadableMissionTimeDisplay);
        Run("transfer execution summary presents next action", TransferExecutionSummaryRegression);
        Run("MAP transfer nodes enter retained maneuver and GUID ownership", TransferManeuverHandoffRegression);
        Run("mixed maneuver guidance uses resultant PNR vector", MixedManeuverGuidanceRegression);
        Run("OrbitMap maneuver nodes preserve KSP-authoritative burn vector", OrbitMapAuthoritativeManeuverVectorRoundTrip);
        Run("GUID selects OrbitMap authoritative vector by vessel and node UT", ManeuverVectorStoreExactMatch);
        Run("standalone maneuver-vector UDP path removed", StandaloneManeuverVectorUdpRemoved);
        Run("maneuver execution readiness is Engine-owned and explicit", ManeuverExecutionReadinessRegression);
        Run("GUID final count and ignition cues are explicit", GuidanceFinalCountCueRegression);
        Run("MNV compute and upload actions expose operator feedback", ManeuverOperatorFeedbackRegression);
        Run("manual maneuver nodes survive unavailable burn estimate", ManualManeuverBurnEstimateDecouplingRegression);
        Run("post-burn cleanup and queue handoff are explicit", PostBurnCleanupQueueHandoffRegression);
        Run("SCHEM vessel topology pipeline preserves projected part positions", VesselSchematicFoundationRegression);
        Run("SCHEM oriented geometry survives topology transport", VesselSchematicGeometryRegression);
        Run("SCHEM geometry uses native local mesh bounds", VesselSchematicNativeBoundsRegression);
        Run("SCHEM side outline survives topology transport", VesselSchematicOutlineTransportRegression);
        Run("SCHEM v5 depth-resolved raster transport", SchematicRasterTransportRegression);
        Run("SCHEM higher-resolution exterior raster presentation", SchematicResolutionRegression);
        Run("SCHEM deterministic radial viewing angle", SchematicAngleSelectionRegression);
        Run("SCHEM uses one clean side-view renderer", VesselSchematicSingleViewRegression);
        Run("SCHEM presentation prioritizes hull and engine bell", VesselSchematicPresentationPolishRegression);
        Run("SCHEM engine bell is compact and attached", VesselSchematicEngineBellScaleRegression);
        Run("SCHEM primary hull dominates visual hierarchy", VesselSchematicHullHierarchyRegression);
        Console.WriteLine("{0} passed, {1} failed", passed, failed);
        Environment.ExitCode = failed == 0 ? 0 : 1;
    }

    private static void MoonTransferFoundationRegression()
    {
        Check(MoonTransferRoute.Classify("Kerbin", "Mun", "Kerbin") ==
            TransferRouteKind.SamePrimaryMoon, "Mun route not classified");
        Check(MoonTransferRoute.Classify("Kerbin", "Duna", "Sun") ==
            TransferRouteKind.Unsupported, "planetary route incorrectly treated as moon");
        OrbitalElements craft = new OrbitalElements
        {
            ReferenceBodyName = "Kerbin", SemiMajorAxisMeters = 700000,
            Eccentricity = 0, InclinationDegrees = 0,
            LongitudeOfAscendingNodeDegrees = 0,
            ArgumentOfPeriapsisDegrees = 0,
            EpochUniversalTimeSeconds = 0, MeanAnomalyAtEpochRadians = 0
        };
        CelestialBodyState mun = new CelestialBodyState
        {
            Name = "Mun", ParentName = "Kerbin", RadiusMeters = 200000,
            SoiRadiusMeters = 2400000,
            Orbit = new OrbitalElements
            {
                ReferenceBodyName = "Kerbin", SemiMajorAxisMeters = 12000000,
                Eccentricity = 0, InclinationDegrees = 0,
                LongitudeOfAscendingNodeDegrees = 0,
                ArgumentOfPeriapsisDegrees = 0,
                EpochUniversalTimeSeconds = 0, MeanAnomalyAtEpochRadians = 2.0
            }
        };
        MoonTransferCandidate candidate;
        bool found = MoonTransferBootstrap.TryFindInjection(craft, mun,
            3.5316e12, 0, 4000, 18000, 40000, 9, 9, out candidate);
        Check(found && candidate != null, "moon Lambert bootstrap found no injection");
        Check(candidate.DeltaVMetersPerSecond > 0 &&
            candidate.DeltaV.IsFinite &&
            candidate.ArrivalUniversalTimeSeconds > candidate.DepartureUniversalTimeSeconds &&
            !candidate.EncounterVerified,
            "moon bootstrap improperly claims validated encounter");
        MoonTransferCandidate invalid;
        Check(!MoonTransferBootstrap.TryFindInjection(craft, mun,
            double.NaN, 0, 4000, 18000, 40000, 9, 9, out invalid) && invalid == null,
            "moon bootstrap accepted invalid central gravity");
    }

    private static void SchematicAngleSelectionRegression()
    {
        string builder = File.ReadAllText(Path.Combine(Environment.CurrentDirectory,
            "KMC.Plugin", "Topology", "VesselTopologyBuilder.cs"));
        Check(builder.Contains("ChooseSchematicViewingAngle(topology)") &&
              builder.Contains("VesselNodeCategory.SolidBooster") &&
              builder.Contains("RotateSchematicPoint(local, sine, cosine)") &&
              builder.Contains("const int width = 192, height = 320;") &&
              builder.Contains("RasterTriangle(triangles[3*t]"),
              "SCHEM automatic viewing-angle selection or depth rendering regressed");
    }
    private static void SchematicResolutionRegression()
    {
        string plugin = File.ReadAllText(Path.Combine(Environment.CurrentDirectory,
            "KMC.Plugin", "Topology", "VesselTopologyBuilder.cs"));
        string page = File.ReadAllText(Path.Combine(Environment.CurrentDirectory,
            "KMC.MissionControl", "Pages", "SchematicPage.cs"));
        Check(plugin.Contains("const int width = 192, height = 320;"),
            "SCHEM raster dimensions regressed");
        Check(page.Contains("InterpolationMode.HighQualityBilinear") &&
              page.Contains("DrawImage(bitmap") &&
              page.Contains("context.Graphics.InterpolationMode = previousInterpolation"),
            "SCHEM bitmap smoothing or graphics-state restoration missing");
    }

    private static void SchematicRasterTransportRegression()
    {
        VesselTopology topology = new VesselTopology();
        topology.SchematicWidth = 4;
        topology.SchematicHeight = 3;
        topology.SchematicMinH = -2;
        topology.SchematicMaxH = 2;
        topology.SchematicMinV = -3;
        topology.SchematicMaxV = 3;
        topology.SchematicPixels = new byte[] {0,0,2,0,0,1,1,2,0,0,2,0};
        byte[] packet = VesselTopologyPacketCodec.Encode(topology);
        VesselTopology decoded;
        Check(VesselTopologyPacketCodec.TryDecode(packet, out decoded), "SCHEM v5 packet rejected");
        Check(decoded.TransportVersion == 5 && decoded.SchematicWidth == 4 &&
            decoded.SchematicHeight == 3 && decoded.SchematicPixels != null &&
            decoded.SchematicPixels.Length == 12 && decoded.SchematicPixels[5] == 1 &&
            decoded.SchematicPixels[6] == 1 && decoded.SchematicPixels[10] == 2 &&
            decoded.SchematicMinH == -2 && decoded.SchematicMaxV == 3,
            "SCHEM raster was not preserved");
        // Old/empty geometry packets must still decode cleanly.
        topology.SchematicPixels = null;
        Check(VesselTopologyPacketCodec.TryDecode(VesselTopologyPacketCodec.Encode(topology), out decoded)
            && decoded.SchematicPixels == null, "SCHEM raster fallback failed");
    }

    private static void Run(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Near(double actual, double expected, double tolerance = 1e-10)
    {
        Check(!double.IsNaN(actual) && !double.IsInfinity(actual) && Math.Abs(actual - expected) <= tolerance,
            string.Format(CultureInfo.InvariantCulture, "expected {0:R}, got {1:R}, tolerance {2:R}", expected, actual, tolerance));
    }
    private static void Vec(Vector3d value, double x, double y, double z, double tolerance = 1e-10)
    { Near(value.X, x, tolerance); Near(value.Y, y, tolerance); Near(value.Z, z, tolerance); }
    private static OrbitalElements Orbit(double a = 1, double e = 0)
    { return new OrbitalElements { ReferenceBodyName = "Synthetic primary", SemiMajorAxisMeters = a, Eccentricity = e }; }
    private static StateVector State(OrbitalElements orbit, double ut, double mu = 1)
    {
        StateVector result;
        Check(KeplerPropagator.TryPropagate(orbit, mu, ut, out result), "propagation rejected valid orbit");
        Check(result.ReferenceBodyName == orbit.ReferenceBodyName, "reference frame lost");
        Near(result.UniversalTimeSeconds, ut); Near(result.GravParameter, mu);
        return result;
    }
    private static void Circular()
    {
        StateVector s = State(Orbit(), Math.PI / 2);
        Vec(s.Position, 0, 1, 0); Vec(s.Velocity, -1, 0, 0);
        s = State(Orbit(), -Math.PI / 2);
        Vec(s.Position, 0, -1, 0); Vec(s.Velocity, 1, 0, 0);
        s = State(Orbit(), 200 * Math.PI);
        Vec(s.Position, 1, 0, 0);
    }
    private static void EllipticApsides()
    {
        OrbitalElements o = Orbit(2, .5);
        StateVector s = State(o, 0);
        Vec(s.Position, 1, 0, 0); Vec(s.Velocity, 0, Math.Sqrt(1.5), 0);
        s = State(o, Math.PI * Math.Sqrt(8));
        Vec(s.Position, -3, 0, 0); Vec(s.Velocity, 0, -Math.Sqrt(1.0 / 6), 0);
    }
    private static void Hyperbolic()
    {
        // Analytic H=1: x=|a|(e-cosh H), y=|a|sqrt(e²-1)sinh H.
        OrbitalElements o = Orbit(-2, 1.5);
        double t = Math.Sqrt(8) * (1.5 * Math.Sinh(1) - 1);
        double scale = Math.Sqrt(.5) / (1.5 * Math.Cosh(1) - 1);
        StateVector s = State(o, t);
        Vec(s.Position, 2 * (1.5 - Math.Cosh(1)), 2 * Math.Sqrt(1.25) * Math.Sinh(1), 0);
        Vec(s.Velocity, -scale * Math.Sinh(1), scale * Math.Sqrt(1.25) * Math.Cosh(1), 0);
        StateVector before = State(o, -t);
        Near(before.Position.X, s.Position.X); Near(before.Position.Y, -s.Position.Y);
        Near(before.Velocity.X, -s.Velocity.X); Near(before.Velocity.Y, s.Velocity.Y);
    }
    private static void Orientation()
    {
        OrbitalElements o = Orbit(); o.InclinationDegrees = 90;
        StateVector s = State(o, Math.PI / 2);
        Vec(s.Position, 0, 0, 1); Vec(s.Velocity, -1, 0, 0);
        o.InclinationDegrees = 180;
        Vec(State(o, Math.PI / 2).Position, 0, -1, 0);
        o.InclinationDegrees = 0; o.LongitudeOfAscendingNodeDegrees = 90;
        Vec(State(o, 0).Position, 0, 1, 0);
        o.ArgumentOfPeriapsisDegrees = 90;
        Vec(State(o, 0).Position, -1, 0, 0);
    }
    private static void Conservation()
    {
        foreach (double e in new[] { 0, .7, .99, .999999, 1.000001, 1.5, 4 })
        {
            double a = e < 1 ? 2 : -2;
            OrbitalElements o = Orbit(a, e); o.InclinationDegrees = 47; o.LongitudeOfAscendingNodeDegrees = 123; o.ArgumentOfPeriapsisDegrees = 81;
            foreach (double t in new[] { -50.0, -.2, 0.0, .2, 50.0 })
            {
                StateVector s = State(o, t);
                double energy = .5 * Vector3d.Dot(s.Velocity, s.Velocity) - 1 / s.Position.Magnitude;
                Near(energy, -1 / (2 * a), e > .99999 && e < 1.00001 ? .001 : 1e-9);
                Near(Vector3d.Cross(s.Position, s.Velocity).Magnitude, Math.Sqrt(a * (1 - e * e)), 1e-9);
            }
        }
    }
    private static void InvalidPropagation()
    {
        StateVector s;
        foreach (double bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Check(!KeplerPropagator.TryPropagate(Orbit(), 1, bad, out s) && s == null, "bad UT accepted");
            Check(!KeplerPropagator.TryPropagate(Orbit(), bad, 0, out s) && s == null, "bad mu accepted");
            OrbitalElements o = Orbit(); o.MeanAnomalyAtEpochRadians = bad;
            Check(!KeplerPropagator.TryPropagate(o, 1, 0, out s) && s == null, "bad anomaly accepted");
        }
        foreach (OrbitalElements o in new[] { null, Orbit(0), Orbit(-1), Orbit(1, 1), Orbit(1, 2), Orbit(1, -.1) })
            Check(!KeplerPropagator.TryPropagate(o, 1, 0, out s) && s == null, "invalid conic accepted");
        Check(!KeplerPropagator.TryPropagate(Orbit(), 0, 0, out s), "zero mu accepted");
    }
    private static void RendererAgreement()
    {
        foreach (double e in new[] { 0, .3, .8, 1.5 })
        {
            OrbitMapOrbit p = new OrbitMapOrbit { ReferenceBodyName = "P", SemiMajorAxisMeters = e < 1 ? 100000 : -100000,
                Eccentricity = e, InclinationDegrees = 32, LongitudeOfAscendingNodeDegrees = 75,
                ArgumentOfPeriapsisDegrees = 111, MeanAnomalyAtEpochRadians = .7, EpochUniversalTimeSeconds = 456 };
            foreach (double t in new[] { -1000.0, 0.0, 456.0, 1200.0 })
            {
                StateVector s = State(OrbitMapNavigationAdapter.ToElements(p), t, 3.5e8);
                OrbitMapVector3 old = OrbitMapConicSampler.PositionAtUniversalTime(p, t, 3.5e8);
                Vec(s.Position, old.X, old.Y, old.Z, 1e-6);
            }
        }
    }
    private static void Fixture(int i, out OrbitMapBody origin, out OrbitMapBody destination, out OrbitMapPacket packet)
    {
        origin = new OrbitMapBody { Name = "Origin", ParentName = "Primary", GravParameter = 3.5e8 };
        destination = new OrbitMapBody { Name = "Destination", ParentName = "Primary" };
        double r1 = i % 2 == 0 ? 1e7 : 1.8e7, r2 = i % 2 == 0 ? 1.8e7 : 1e7;
        foreach (OrbitMapBody b in new[] { origin, destination })
        {
            double a = b == origin ? r1 : r2;
            b.Orbit = new OrbitMapOrbit { SemiMajorAxisMeters = a, PeriodSeconds = 2 * Math.PI * Math.Sqrt(a * a * a / 1e12),
                Eccentricity = .02 * i, MeanAnomalyAtEpochRadians = .23 + .4 * i + (b == destination ? .8 : 0),
                EpochUniversalTimeSeconds = 1234, InclinationDegrees = 2 * i, LongitudeOfAscendingNodeDegrees = 17 * i,
                ArgumentOfPeriapsisDegrees = 11 * i };
        }
        packet = new OrbitMapPacket { ReferenceBodyRadiusMeters = 60000, UniversalTimeSeconds = 6000 + 1000 * i,
            ActiveOrbit = new OrbitMapOrbit { SemiMajorAxisMeters = 70000 + 1000 * i, Eccentricity = .005 * i,
                InclinationDegrees = i, MeanAnomalyAtEpochRadians = .3 * i, EpochUniversalTimeSeconds = 100,
                LongitudeOfAscendingNodeDegrees = 3 * i, ArgumentOfPeriapsisDegrees = 7 * i } };
    }
    private static void LegacyParity()
    {
        string[] rows = File.ReadAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures/legacy-14.22.32.csv"));
        string[] names = rows[0].Replace("\"", "").Split(',');
        Check(rows.Length == 9, "baseline fixture count");
        for (int i = 0; i < 8; i++)
        {
            OrbitMapBody origin, destination; OrbitMapPacket packet; Fixture(i, out origin, out destination, out packet);
            TransferWindowSolution w; ParkingOrbitEjectionSolution p;
            Check(OrbitMapNavigationAdapter.TryCalculateTransferWindow(origin, destination, packet.UniversalTimeSeconds, out w), "window rejected");
            Check(OrbitMapNavigationAdapter.TryCalculateParkingOrbitEjection(packet, origin, w, out p), "ejection rejected");
            string[] values = rows[i + 1].Replace("\"", "").Split(',');
            for (int j = 1; j < names.Length; j++)
            {
                var field = w.GetType().GetField(names[j]);
                object value = field != null ? field.GetValue(w) : p.GetType().GetField(names[j]).GetValue(p);
                if (value is double) { double expected = double.Parse(values[j], CultureInfo.InvariantCulture); Near((double)value, expected, Math.Max(1e-9, Math.Abs(expected) * 2e-13)); }
                else Check((string)value == values[j], "text result differs: " + names[j]);
            }
        }
    }
    private static void PlannerLimits()
    {
        OrbitMapBody a, b; OrbitMapPacket packet; Fixture(0, out a, out b, out packet);
        TransferWindowSolution w; ParkingOrbitEjectionSolution p;
        Check(OrbitMapNavigationAdapter.TryCalculateTransferWindow(a, b, packet.UniversalTimeSeconds, out w), "valid window");
        packet.ActiveOrbit.Eccentricity = .05; packet.ActiveOrbit.InclinationDegrees = 10;
        Check(OrbitMapNavigationAdapter.TryCalculateParkingOrbitEjection(packet, a, w, out p), "boundary rejected");
        packet.ActiveOrbit.Eccentricity = .05001;
        Check(!OrbitMapNavigationAdapter.TryCalculateParkingOrbitEjection(packet, a, w, out p), "eccentric parking accepted");
        packet.ActiveOrbit.Eccentricity = 0; packet.ActiveOrbit.InclinationDegrees = 10.001;
        Check(!OrbitMapNavigationAdapter.TryCalculateParkingOrbitEjection(packet, a, w, out p), "inclined parking accepted");
        packet.ActiveOrbit.InclinationDegrees = 0; a.Orbit = null;
        Check(!OrbitMapNavigationAdapter.TryCalculateParkingOrbitEjection(packet, a, w, out p), "missing orbit accepted");
        Fixture(0, out a, out b, out packet); b.ParentName = "Different";
        Check(!OrbitMapNavigationAdapter.TryCalculateTransferWindow(a, b, 0, out w), "hierarchy change accepted");
        b.ParentName = a.ParentName; b.Orbit.Eccentricity = 1.5;
        Check(!OrbitMapNavigationAdapter.TryCalculateTransferWindow(a, b, 0, out w), "hyperbolic transfer accepted");
        Fixture(0, out a, out b, out packet); a.Orbit.PeriodSeconds = 0;
        Check(OrbitMapNavigationAdapter.TryCalculateTransferWindow(a, b, 0, out w), "destination period fallback lost");
        Check(!OrbitMapNavigationAdapter.TryCalculateParkingOrbitEjection(packet, a, w, out p), "ejection without origin period accepted");
        b.Orbit.PeriodSeconds = 0;
        Check(!OrbitMapNavigationAdapter.TryCalculateTransferWindow(a, b, 0, out w), "no periods accepted");
    }
    private static void InvalidPlanner()
    {
        foreach (double bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            OrbitMapBody a, b; OrbitMapPacket packet; Fixture(0, out a, out b, out packet);
            TransferWindowSolution w; ParkingOrbitEjectionSolution p;
            Check(!OrbitMapNavigationAdapter.TryCalculateTransferWindow(a, b, bad, out w) && w == null, "bad UT accepted");
            Check(OrbitMapNavigationAdapter.TryCalculateTransferWindow(a, b, 0, out w), "valid window");
            packet.ActiveOrbit.InclinationDegrees = bad;
            Check(!OrbitMapNavigationAdapter.TryCalculateParkingOrbitEjection(packet, a, w, out p) && p == null, "bad inclination accepted");
            b.Orbit.Eccentricity = bad;
            Check(!OrbitMapNavigationAdapter.TryCalculateTransferWindow(a, b, 0, out w) && w == null, "bad eccentricity accepted");
        }
    }

    private static void MapIntegration()
    {
        // Exercise the actual MAP -> adapter -> engine -> maneuver packet path.
        // Do not click CREATE: this test must never send anything to a running game.
        OrbitMapBody a, b; OrbitMapPacket packet; Fixture(0, out a, out b, out packet);
        packet.ReferenceBodyName = a.Name; packet.VesselId = "TEST-VESSEL";
        packet.Bodies.Add(a); packet.Bodies.Add(b);
        a.RadiusMeters = packet.ReferenceBodyRadiusMeters;
        packet.ActiveOrbit.ReferenceBodyName = a.Name;
        a.Orbit.ReferenceBodyName = a.ParentName; b.Orbit.ReferenceBodyName = b.ParentName;
        var page = new MapPage();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            using (var canvas = new Bitmap(1400, 1000))
            using (var graphics = Graphics.FromImage(canvas))
            using (var large = new Font("Consolas", 14))
            using (var small = new Font("Consolas", 10))
            {
                var context = new MissionRenderContext(graphics, new Rectangle(0, 0, 1400, 1000),
                    large, small, Color.Lime, Color.Green);
                OrbitMapSnapshotStore.SetLatest(packet, DateTime.UtcNow);
                page.Draw(context, null);
                var tab = (Rectangle)typeof(MapPage).GetField("_transferTab", flags).GetValue(page);
                Check(page.PointerDown(new PointF(tab.Left + 1, tab.Top + 1), MouseButtons.Left), "transfer tab did not activate");
                page.Draw(context, null);
                var field = typeof(MapPage).GetField("_transferNodeCandidate", flags);
                var candidate = (ManeuverUplinkPacket)field.GetValue(page);
                Check(candidate != null, "MAP did not construct a candidate");
                Check(candidate.VesselId == packet.VesselId && candidate.TargetBodyName == b.Name, "candidate identity lost");
                Near(candidate.NodeUniversalTimeSeconds, 333347.415029378, 1e-6);
                Near(candidate.ProgradeDeltaVMetersPerSecond, 37.8837267367163, 1e-9);
                Near(candidate.NormalDeltaVMetersPerSecond, 0); Near(candidate.RadialDeltaVMetersPerSecond, 0);
                packet.UniversalTimeSeconds = double.NaN;
                OrbitMapSnapshotStore.SetLatest(packet, DateTime.UtcNow);
                page.Draw(context, null);
                Check(field.GetValue(page) == null, "invalid telemetry retained an uploadable candidate");
            }
        }
        finally { OrbitMapSnapshotStore.Clear(); }
    }

    private static void NegativeEllipticAxes()
    {
        OrbitMapBody a, b; OrbitMapPacket packet; Fixture(0, out a, out b, out packet);
        TransferWindowSolution w; ParkingOrbitEjectionSolution p;
        Check(OrbitMapNavigationAdapter.TryCalculateTransferWindow(a, b, 0, out w), "valid window");
        packet.ActiveOrbit.SemiMajorAxisMeters *= -1;
        Check(!OrbitMapNavigationAdapter.TryCalculateParkingOrbitEjection(packet, a, w, out p) && p == null, "negative parking axis accepted");
        a.Orbit.SemiMajorAxisMeters *= -1;
        Check(!OrbitMapNavigationAdapter.TryCalculateTransferWindow(a, b, 0, out w) && w == null, "negative origin axis accepted");
        a.Orbit.SemiMajorAxisMeters *= -1; b.Orbit.SemiMajorAxisMeters *= -1;
        Check(!OrbitMapNavigationAdapter.TryCalculateTransferWindow(a, b, 0, out w) && w == null, "negative destination axis accepted");
    }

    private static void ConflictingFrames()
    {
        OrbitMapBody a, b; OrbitMapPacket packet; Fixture(0, out a, out b, out packet);
        TransferWindowSolution w; ParkingOrbitEjectionSolution p;
        Check(OrbitMapNavigationAdapter.TryCalculateTransferWindow(a, b, 0, out w), "valid window");
        packet.ActiveOrbit.ReferenceBodyName = "Another origin";
        Check(!OrbitMapNavigationAdapter.TryCalculateParkingOrbitEjection(packet, a, w, out p) && p == null, "parking in wrong frame accepted");
        packet.ActiveOrbit.ReferenceBodyName = a.Name;
        w.ParentName = "Another primary";
        Check(!OrbitMapNavigationAdapter.TryCalculateParkingOrbitEjection(packet, a, w, out p), "transfer in wrong frame accepted");
        a.Orbit.ReferenceBodyName = "Another primary";
        Check(!OrbitMapNavigationAdapter.TryCalculateTransferWindow(a, b, 0, out w) && w == null, "origin in wrong frame accepted");
        a.Orbit.ReferenceBodyName = a.ParentName; b.Orbit.ReferenceBodyName = "Another primary";
        Check(!OrbitMapNavigationAdapter.TryCalculateTransferWindow(a, b, 0, out w), "destination in wrong frame accepted");
        b.Orbit.ReferenceBodyName = b.ParentName;
        Check(OrbitMapNavigationAdapter.TryCalculateTransferWindow(a, b, 0, out w), "valid named frames rejected");
        packet.ReferenceBodyName = "Another origin";
        Check(!OrbitMapNavigationAdapter.TryCalculateParkingOrbitEjection(packet, a, w, out p), "packet in wrong frame accepted");
    }
    private static void LambertCanonical()
    {
        LambertSolution solution;
        Vector3d r1 = new Vector3d(1, 0, 0);
        Vector3d r2 = new Vector3d(0, 1, 0);

        Check(LambertSolver.TrySolve(
            r1, r2, Math.PI / 2.0, 1.0,
            LambertTransferPath.ShortWay, out solution),
            "short-way circular Lambert case rejected");
        Vec(solution.DepartureVelocity, 0, 1, 0, 2e-9);
        Vec(solution.ArrivalVelocity, -1, 0, 0, 2e-9);
        Near(solution.TimeOfFlightSeconds, Math.PI / 2.0);
        Near(solution.GravParameter, 1.0);
        Check(solution.Path == LambertTransferPath.ShortWay, "short-way path identity lost");
        Check(Vector3d.Cross(r1, solution.DepartureVelocity).Z > 0,
            "short-way transfer used wrong plane sense");

        Check(LambertSolver.TrySolve(
            r1, r2, 3.0 * Math.PI / 2.0, 1.0,
            LambertTransferPath.LongWay, out solution),
            "long-way circular Lambert case rejected");
        Vec(solution.DepartureVelocity, 0, -1, 0, 2e-9);
        Vec(solution.ArrivalVelocity, 1, 0, 0, 2e-9);
        Check(solution.Path == LambertTransferPath.LongWay, "long-way path identity lost");
        Check(Vector3d.Cross(r1, solution.DepartureVelocity).Z < 0,
            "long-way transfer used wrong plane sense");
    }

    private static void LambertVallado()
    {
        // Standard 3D Lambert reference example used in orbital-mechanics texts.
        // Units are km and km^3/s^2 here; the solver itself is unit-consistent.
        // Production KMC supplies SI metres and m^3/s^2.
        LambertSolution solution;
        Check(LambertSolver.TrySolve(
            new Vector3d(5000, 10000, 2100),
            new Vector3d(-14600, 2500, 7000),
            3600.0,
            398600.0,
            LambertTransferPath.ShortWay,
            out solution),
            "Vallado reference Lambert case rejected");

        Vec(solution.DepartureVelocity,
            -5.99249463966639,
             1.92536341566345,
             3.24563652849053,
             2e-8);
        Vec(solution.ArrivalVelocity,
            -3.31246031093682,
            -4.19661730794498,
            -0.38528761712990,
             2e-8);

        // Boundary states must describe one two-body conic: specific energy
        // and angular momentum magnitude agree at both ends.
        Vector3d r1 = new Vector3d(5000, 10000, 2100);
        Vector3d r2 = new Vector3d(-14600, 2500, 7000);
        double e1 = .5 * Vector3d.Dot(solution.DepartureVelocity, solution.DepartureVelocity) - 398600.0 / r1.Magnitude;
        double e2 = .5 * Vector3d.Dot(solution.ArrivalVelocity, solution.ArrivalVelocity) - 398600.0 / r2.Magnitude;
        Near(e1, e2, 2e-9);
        Near(Vector3d.Cross(r1, solution.DepartureVelocity).Magnitude,
             Vector3d.Cross(r2, solution.ArrivalVelocity).Magnitude,
             2e-8);
    }

    private static void LambertInvalid()
    {
        LambertSolution solution;
        Vector3d x = new Vector3d(1, 0, 0);
        Vector3d y = new Vector3d(0, 1, 0);

        Check(!LambertSolver.TrySolve(x, y, 0, 1, LambertTransferPath.ShortWay, out solution) && solution == null,
            "zero time of flight accepted");
        Check(!LambertSolver.TrySolve(x, y, -1, 1, LambertTransferPath.ShortWay, out solution) && solution == null,
            "negative time of flight accepted");
        Check(!LambertSolver.TrySolve(x, y, 1, 0, LambertTransferPath.ShortWay, out solution) && solution == null,
            "zero mu accepted");
        Check(!LambertSolver.TrySolve(new Vector3d(), y, 1, 1, LambertTransferPath.ShortWay, out solution) && solution == null,
            "zero departure radius accepted");
        Check(!LambertSolver.TrySolve(x, new Vector3d(2, 0, 0), 1, 1, LambertTransferPath.ShortWay, out solution) && solution == null,
            "collinear same-direction endpoints accepted");
        Check(!LambertSolver.TrySolve(x, new Vector3d(-1, 0, 0), 1, 1, LambertTransferPath.ShortWay, out solution) && solution == null,
            "collinear opposite endpoints accepted without a plane normal");
        Check(!LambertSolver.TrySolve(new Vector3d(double.NaN, 0, 0), y, 1, 1, LambertTransferPath.ShortWay, out solution) && solution == null,
            "nonfinite endpoint accepted");
        Check(!LambertSolver.TrySolve(x, y, double.PositiveInfinity, 1, LambertTransferPath.ShortWay, out solution) && solution == null,
            "nonfinite time accepted");
    }

    private static void LambertMapPreviewAdapter()
    {
        OrbitMapBody origin, destination;
        OrbitMapPacket packet;
        Fixture(0, out origin, out destination, out packet);

        origin.Orbit.ReferenceBodyName = origin.ParentName;
        destination.Orbit.ReferenceBodyName = destination.ParentName;

        packet.Bodies.Add(origin);
        packet.Bodies.Add(destination);

        TransferWindowSolution hohmann;
        Check(
            OrbitMapNavigationAdapter.TryCalculateTransferWindow(
                origin,
                destination,
                packet.UniversalTimeSeconds,
                out hohmann),
            "Hohmann seed unavailable");

        TransferSearchSolution preview;
        string muSource;

        Check(
            OrbitMapNavigationAdapter.TryCalculateLambertPreview(
                packet,
                origin,
                destination,
                hohmann,
                out preview,
                out muSource),
            "period-fallback Lambert preview unavailable");

        Check(preview != null, "preview is null");
        Check(
            preview.DepartureUniversalTimeSeconds >=
                packet.UniversalTimeSeconds,
            "preview departure is in the past");
        Check(
            muSource == "ORBIT PERIOD",
            "period-derived parent mu source not reported");

        OrbitMapBody parent =
            new OrbitMapBody
            {
                Name = origin.ParentName,
                ParentName = string.Empty,
                RadiusMeters = 1000000.0,
                SoiRadiusMeters = 0.0,
                GravParameter = 1e12
            };

        packet.Bodies.Insert(0, parent);

        Check(
            OrbitMapNavigationAdapter.TryCalculateLambertPreview(
                packet,
                origin,
                destination,
                hohmann,
                out preview,
                out muSource),
            "parent-body Lambert preview unavailable");

        Check(
            muSource == "PARENT BODY TELEMETRY",
            "authoritative parent body mu was not preferred");
    }

    private static void LambertMapEjectionPreviewAdapter()
    {
        OrbitMapBody origin, destination;
        OrbitMapPacket packet;
        Fixture(0, out origin, out destination, out packet);

        packet.ReferenceBodyName = origin.Name;
        packet.ActiveOrbit.ReferenceBodyName = origin.Name;
        origin.RadiusMeters = packet.ReferenceBodyRadiusMeters;
        origin.SoiRadiusMeters = 500000.0;
        origin.Orbit.ReferenceBodyName = origin.ParentName;
        destination.Orbit.ReferenceBodyName = destination.ParentName;

        packet.Bodies.Add(origin);
        packet.Bodies.Add(destination);

        TransferWindowSolution hohmann;
        Check(
            OrbitMapNavigationAdapter.TryCalculateTransferWindow(
                origin,
                destination,
                packet.UniversalTimeSeconds,
                out hohmann),
            "Hohmann seed unavailable");

        TransferSearchSolution transfer;
        string muSource;

        Check(
            OrbitMapNavigationAdapter.TryCalculateLambertPreview(
                packet,
                origin,
                destination,
                hohmann,
                out transfer,
                out muSource),
            "Lambert transfer preview unavailable");

        LambertParkingOrbitEjectionSolution ejection;

        Check(
            OrbitMapNavigationAdapter.TryCalculateLambertParkingOrbitEjectionPreview(
                packet,
                origin,
                transfer,
                out ejection),
            "Lambert parking ejection preview unavailable");

        Check(ejection != null, "ejection preview is null");
        Check(
            ejection.TotalDeltaVMetersPerSecond > 0.0,
            "ejection total DV is not positive");
        Check(
            !double.IsNaN(ejection.BurnUniversalTimeSeconds) &&
            !double.IsInfinity(ejection.BurnUniversalTimeSeconds),
            "ejection burn UT is invalid");
        Check(
            ejection.GeometryResidualDegrees < 1e-6,
            "ejection geometry residual is too large");

        double componentMagnitude =
            Math.Sqrt(
                ejection.ProgradeDeltaVMetersPerSecond *
                ejection.ProgradeDeltaVMetersPerSecond +
                ejection.NormalDeltaVMetersPerSecond *
                ejection.NormalDeltaVMetersPerSecond +
                ejection.RadialDeltaVMetersPerSecond *
                ejection.RadialDeltaVMetersPerSecond);

        Near(
            componentMagnitude,
            ejection.TotalDeltaVMetersPerSecond,
            1e-6);
    }

    private static void ParkingAwareLambertAdapter()
    {
        OrbitMapBody origin, destination;
        OrbitMapPacket packet;
        Fixture(0, out origin, out destination, out packet);

        packet.ReferenceBodyName = origin.Name;
        packet.ActiveOrbit.ReferenceBodyName = origin.Name;
        origin.RadiusMeters = packet.ReferenceBodyRadiusMeters;
        origin.SoiRadiusMeters = 500000.0;
        origin.Orbit.ReferenceBodyName = origin.ParentName;
        destination.Orbit.ReferenceBodyName = destination.ParentName;

        packet.Bodies.Add(origin);
        packet.Bodies.Add(destination);

        TransferWindowSolution hohmann;
        Check(
            OrbitMapNavigationAdapter.TryCalculateTransferWindow(
                origin,
                destination,
                packet.UniversalTimeSeconds,
                out hohmann),
            "Hohmann seed unavailable");

        ParkingOrbitAwareTransferSolution solution;
        string muSource;

        Check(
            OrbitMapNavigationAdapter.TryCalculateParkingAwareLambertPreview(
                packet,
                origin,
                destination,
                hohmann,
                out solution,
                out muSource),
            "parking-aware Lambert preview unavailable");

        Check(solution != null, "parking-aware solution is null");
        Check(solution.Transfer != null, "transfer child is null");
        Check(solution.Ejection != null, "ejection child is null");
        Check(solution.FiniteSoiAssessment != null, "finite-SOI assessment is null");
        Check(
            solution.FiniteSoiAssessment.NormalizedStateError >= 0.0,
            "finite-SOI state score is invalid");
        Check(
            solution.EjectionScoreMetersPerSecond > 0.0,
            "ejection score is not positive");
        Check(
            solution.Ejection.GeometryResidualDegrees < 1e-6,
            "geometry residual too large");

        double componentMagnitude =
            Math.Sqrt(
                solution.Ejection.ProgradeDeltaVMetersPerSecond *
                solution.Ejection.ProgradeDeltaVMetersPerSecond +
                solution.Ejection.NormalDeltaVMetersPerSecond *
                solution.Ejection.NormalDeltaVMetersPerSecond +
                solution.Ejection.RadialDeltaVMetersPerSecond *
                solution.Ejection.RadialDeltaVMetersPerSecond);

        Near(
            componentMagnitude,
            solution.EjectionScoreMetersPerSecond,
            1e-6);
    }

    private static void FiniteSoiBoundaryAssessment()
    {
        OrbitMapBody origin, destination;
        OrbitMapPacket packet;
        Fixture(0, out origin, out destination, out packet);

        packet.ReferenceBodyName = origin.Name;
        packet.ActiveOrbit.ReferenceBodyName = origin.Name;
        origin.RadiusMeters = packet.ReferenceBodyRadiusMeters;
        origin.SoiRadiusMeters = 500000.0;
        origin.Orbit.ReferenceBodyName = origin.ParentName;
        destination.Orbit.ReferenceBodyName = destination.ParentName;

        packet.Bodies.Add(origin);
        packet.Bodies.Add(destination);

        TransferWindowSolution hohmann;
        Check(
            OrbitMapNavigationAdapter.TryCalculateTransferWindow(
                origin,
                destination,
                packet.UniversalTimeSeconds,
                out hohmann),
            "Hohmann seed unavailable");

        ParkingOrbitAwareTransferSolution solution;
        string muSource;

        Check(
            OrbitMapNavigationAdapter.TryCalculateParkingAwareLambertPreview(
                packet,
                origin,
                destination,
                hohmann,
                out solution,
                out muSource),
            "finite-SOI-ranked preview unavailable");

        Check(solution.FiniteSoiAssessment != null, "assessment is null");
        Check(
            solution.FiniteSoiAssessment.ExitUniversalTimeSeconds >
            solution.Ejection.BurnUniversalTimeSeconds,
            "SOI exit is not after burn");
        Check(
            solution.FiniteSoiAssessment.PositionErrorMeters >= 0.0,
            "position error invalid");
        Check(
            solution.FiniteSoiAssessment.VelocityErrorMetersPerSecond >= 0.0,
            "velocity error invalid");
    }


    private static void FiniteSoiLocalCorrection()
    {
        OrbitMapBody origin, destination;
        OrbitMapPacket packet;
        Fixture(0, out origin, out destination, out packet);

        packet.ReferenceBodyName = origin.Name;
        packet.ActiveOrbit.ReferenceBodyName = origin.Name;
        origin.RadiusMeters = packet.ReferenceBodyRadiusMeters;
        origin.SoiRadiusMeters = 500000.0;
        origin.Orbit.ReferenceBodyName = origin.ParentName;
        destination.Orbit.ReferenceBodyName = destination.ParentName;

        packet.Bodies.Add(origin);
        packet.Bodies.Add(destination);

        TransferWindowSolution hohmann;

        Check(
            OrbitMapNavigationAdapter.TryCalculateTransferWindow(
                origin,
                destination,
                packet.UniversalTimeSeconds,
                out hohmann),
            "Hohmann seed unavailable");

        ParkingOrbitAwareTransferSolution coarse;
        string muSource;

        Check(
            OrbitMapNavigationAdapter.TryCalculateParkingAwareLambertPreview(
                packet,
                origin,
                destination,
                hohmann,
                out coarse,
                out muSource),
            "coarse finite-SOI solution unavailable");

        FiniteSoiDepartureCorrectionResult correction;

        Check(
            FiniteSoiDepartureOptimizer.TryOptimize(
                coarse.Transfer,
                coarse.Ejection,
                OrbitMapNavigationAdapter.ToElements(
                    packet.ActiveOrbit),
                OrbitMapNavigationAdapter.ToBody(
                    origin),
                1e12,
                out correction),
            "local correction failed");

        Check(correction != null, "correction result is null");
        Check(correction.InitialAssessment != null, "initial assessment missing");
        Check(correction.CorrectedAssessment != null, "corrected assessment missing");
        Check(correction.CorrectedEjection != null, "corrected ejection missing");
        Check(correction.Evaluations > 0, "no correction evaluations");

        Check(
            correction.CorrectedAssessment.NormalizedStateError <=
            correction.InitialAssessment.NormalizedStateError + 1e-12,
            "local correction worsened state score");

        double total =
            Math.Sqrt(
                correction.CorrectedEjection.ProgradeDeltaVMetersPerSecond *
                correction.CorrectedEjection.ProgradeDeltaVMetersPerSecond +
                correction.CorrectedEjection.NormalDeltaVMetersPerSecond *
                correction.CorrectedEjection.NormalDeltaVMetersPerSecond +
                correction.CorrectedEjection.RadialDeltaVMetersPerSecond *
                correction.CorrectedEjection.RadialDeltaVMetersPerSecond);

        Near(
            total,
            correction.CorrectedEjection.TotalDeltaVMetersPerSecond,
            1e-8);
    }


    private static void TargetSoiShooting()
    {
        OrbitMapBody origin, destination;
        OrbitMapPacket packet;
        Fixture(0, out origin, out destination, out packet);

        packet.ReferenceBodyName = origin.Name;
        packet.ActiveOrbit.ReferenceBodyName = origin.Name;
        origin.RadiusMeters = packet.ReferenceBodyRadiusMeters;
        origin.SoiRadiusMeters = 500000.0;
        destination.SoiRadiusMeters = 900000.0;
        origin.Orbit.ReferenceBodyName = origin.ParentName;
        destination.Orbit.ReferenceBodyName = destination.ParentName;

        packet.Bodies.Add(origin);
        packet.Bodies.Add(destination);

        TransferWindowSolution hohmann;

        Check(
            OrbitMapNavigationAdapter.TryCalculateTransferWindow(
                origin,
                destination,
                packet.UniversalTimeSeconds,
                out hohmann),
            "Hohmann seed unavailable");

        ParkingOrbitAwareTransferSolution coarse;
        string muSource;

        Check(
            OrbitMapNavigationAdapter.TryCalculateParkingAwareLambertPreview(
                packet,
                origin,
                destination,
                hohmann,
                out coarse,
                out muSource),
            "parking-aware seed unavailable");

        TargetSoiShootingResult shooting;

        Check(
            TargetSoiShootingSolver.TrySolve(
                coarse.Transfer,
                coarse.Ejection,
                OrbitMapNavigationAdapter.ToElements(
                    packet.ActiveOrbit),
                OrbitMapNavigationAdapter.ToBody(
                    origin),
                OrbitMapNavigationAdapter.ToBody(
                    destination),
                1e12,
                out shooting),
            "target SOI shooting failed");

        Check(shooting != null, "shooting result is null");
        Check(shooting.InitialAssessment != null, "initial shooting assessment missing");
        Check(shooting.CorrectedAssessment != null, "corrected shooting assessment missing");
        Check(shooting.CorrectedEjection != null, "corrected shooting ejection missing");
        Check(shooting.Evaluations > 0, "shooting made no evaluations");

        Check(
            shooting.CorrectedAssessment.PredictedEncounter ||
            shooting.CorrectedAssessment.MissDistanceMeters <=
                shooting.InitialAssessment.MissDistanceMeters + 1e-6,
            "target shooting neither established encounter nor improved miss distance");

        Check(
            shooting.CorrectedAssessment.MissFractionOfTargetSoi >= 0.0,
            "invalid target SOI miss fraction");
    }


    private static void TargetSoiSafePeriapsis()
    {
        OrbitMapBody origin, destination;
        OrbitMapPacket packet;
        Fixture(0, out origin, out destination, out packet);

        packet.ReferenceBodyName = origin.Name;
        packet.ActiveOrbit.ReferenceBodyName = origin.Name;

        origin.RadiusMeters = packet.ReferenceBodyRadiusMeters;
        origin.SoiRadiusMeters = 500000.0;

        destination.RadiusMeters = 100000.0;
        destination.SoiRadiusMeters = 5000000.0;
        destination.GravParameter = 2.5e8;

        origin.Orbit.ReferenceBodyName = origin.ParentName;
        destination.Orbit.ReferenceBodyName = destination.ParentName;

        packet.Bodies.Add(origin);
        packet.Bodies.Add(destination);

        TransferWindowSolution hohmann;

        Check(
            OrbitMapNavigationAdapter.TryCalculateTransferWindow(
                origin,
                destination,
                packet.UniversalTimeSeconds,
                out hohmann),
            "Hohmann seed unavailable");

        ParkingOrbitAwareTransferSolution coarse;
        string muSource;

        Check(
            OrbitMapNavigationAdapter.TryCalculateParkingAwareLambertPreview(
                packet,
                origin,
                destination,
                hohmann,
                out coarse,
                out muSource),
            "parking-aware seed unavailable");

        TargetSoiShootingResult shooting;

        Check(
            TargetSoiShootingSolver.TrySolve(
                coarse.Transfer,
                coarse.Ejection,
                OrbitMapNavigationAdapter.ToElements(
                    packet.ActiveOrbit),
                OrbitMapNavigationAdapter.ToBody(
                    origin),
                OrbitMapNavigationAdapter.ToBody(
                    destination),
                1e12,
                out shooting),
            "safe-periapsis shooting failed");

        Check(shooting != null, "safe-periapsis result is null");

        TargetSoiShootingAssessment shot =
            shooting.CorrectedAssessment;

        Check(shot != null, "safe-periapsis assessment missing");

        if (shot.PredictedEncounter)
        {
            Check(
                shot.DesiredPeriapsisRadiusMeters >
                    destination.RadiusMeters,
                "desired periapsis is inside body");

            Check(
                shot.DesiredPeriapsisRadiusMeters <
                    destination.SoiRadiusMeters,
                "desired periapsis is outside SOI");

            Check(
                shot.TargetPeriapsisRadiusMeters > 0.0,
                "predicted periapsis invalid");

            Check(
                !shot.PredictedCollision,
                "corrected shooting still predicts collision");
        }
    }


    private static void TargetSoiOptimizationPolicyRegression()
    {
        double seedDv = 3000.0;

        Near(
            TargetSoiOptimizationPolicy.
                ComputeMaximumDepartureDeltaV(
                    seedDv),
            4500.0,
            1e-9);

        Check(
            TargetSoiOptimizationPolicy.
                IsWithinDepartureTrustRegion(
                    seedDv,
                    4499.0),
            "valid constrained departure rejected");

        Check(
            !TargetSoiOptimizationPolicy.
                IsWithinDepartureTrustRegion(
                    seedDv,
                    18000.0),
            "runaway encounter DV accepted");

        /*
         * Do not construct TargetSoiShootingAssessment here. Its result
         * properties are intentionally engine-owned/read-only outside the
         * KMC.Engine assembly. This regression verifies the externally
         * observable DV trust-region policy; periapsis feasibility is covered
         * through the live shooting tests that create real assessments.
         */
        Check(
            TargetSoiOptimizationPolicy.
                ComputeMaximumDepartureDeltaV(
                    seedDv) >
                seedDv,
            "departure trust region did not leave correction authority");
    }

    private static void TargetBPlaneRegression()
    {
        double actualB;
        double desiredB;
        double error;

        /*
         * Hyperbolic entry at r=10 with mu=1 and speed=1.
         * The target relationship must return a finite desired impact
         * parameter for rp=1 and a nonnegative geometry error.
         */
        Check(
            TargetBPlanePlanner.TryCalculate(
                new Vector3d(-9.0, 4.358898943540674, 0.0),
                new Vector3d(1.0, 0.0, 0.0),
                1.0,
                10.0,
                1.0,
                out actualB,
                out desiredB,
                out error),
            "B-plane calculation rejected valid hyperbolic entry");

        double expectedVInfinity = Math.Sqrt(0.8);
        double expectedAngularMomentum = 4.358898943540674;
        double expectedActualB =
            expectedAngularMomentum / expectedVInfinity;

        Near(
            actualB,
            expectedActualB,
            2e-12);

        Near(
            desiredB,
            Math.Sqrt(3.5),
            2e-12);

        Check(
            error >= 0.0 &&
            !double.IsNaN(error) &&
            !double.IsInfinity(error),
            "B-plane error is invalid");
    }

    private static void TargetBPlaneBootstrapRegression()
    {
        Vector3d entry;
        double desiredB;

        Check(
            TargetBPlaneBootstrapPlanner.TryBuildEntryOffset(
                new Vector3d(1.0, 0.0, 0.0),
                1.0,
                10.0,
                1.0,
                0.0,
                out entry,
                out desiredB),
            "B-plane entry bootstrap rejected valid geometry");

        Near(
            entry.Magnitude,
            10.0,
            2e-10);

        Near(
            desiredB,
            Math.Sqrt(3.0),
            2e-10);

        Check(
            entry.X < 0.0,
            "B-plane entry is not upstream of incoming v-infinity");

        Near(
            Math.Sqrt(
                entry.Y * entry.Y +
                entry.Z * entry.Z),
            desiredB,
            2e-10);
    }

    private static void TargetBPlaneTerminalInitializationRegression()
    {
        string solverText =
            System.IO.File.ReadAllText(
                System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "KMC.Engine",
                    "Navigation",
                    "TargetSoiShootingSolver.cs"));

        int searchIndex =
            solverText.IndexOf(
                "RunBPlaneTerminalSearch",
                StringComparison.Ordinal);

        Check(
            searchIndex >= 0,
            "B-plane terminal search is missing");

        int terminalIndex =
            solverText.IndexOf(
                "PASS 2 -- TERMINAL GEOMETRY",
                searchIndex,
                StringComparison.Ordinal);

        Check(
            terminalIndex > searchIndex,
            "B-plane terminal search does not initialize terminal refinement");

        Check(
            solverText.IndexOf(
                "IsWithinDepartureTrustRegion",
                searchIndex,
                StringComparison.Ordinal) >= 0,
            "B-plane terminal search is not bounded by departure DV");
    }

    private static void TargetBPlaneIncumbentRegression()
    {
        string solverText =
            System.IO.File.ReadAllText(
                System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "KMC.Engine",
                    "Navigation",
                    "TargetSoiShootingSolver.cs"));

        Check(
            solverText.IndexOf(
                "encounterIncumbentEjection",
                StringComparison.Ordinal) >= 0 &&
            solverText.IndexOf(
                "encounterIncumbentAssessment",
                StringComparison.Ordinal) >= 0,
            "terminal solve does not preserve a pre-B-plane incumbent");

        int search =
            solverText.IndexOf(
                "RunBPlaneTerminalSearch",
                StringComparison.Ordinal);

        Check(
            search >= 0,
            "B-plane terminal search is missing");

        Check(
            solverText.IndexOf(
                "trialAssessment.PredictedEncounter",
                search,
                StringComparison.Ordinal) >= 0,
            "B-plane terminal search can promote non-encounter geometry");

        int fallback =
            solverText.IndexOf(
                "The B-plane state is an optimizer initialization",
                StringComparison.Ordinal);

        Check(
            fallback >= 0 &&
            solverText.IndexOf(
                "encounterIncumbentEjection",
                fallback,
                StringComparison.Ordinal) >= 0,
            "terminal result has no incumbent fallback");
    }

    private static void TargetBPlaneConstraintSearchRegression()
    {
        string solverText =
            System.IO.File.ReadAllText(
                System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "KMC.Engine",
                    "Navigation",
                    "TargetSoiShootingSolver.cs"));

        int search =
            solverText.IndexOf(
                "RunBPlaneTerminalSearch",
                StringComparison.Ordinal);

        Check(
            search >= 0,
            "B-plane terminal constraint search is missing");

        Check(
            solverText.IndexOf(
                "const int azimuthSamples = 16",
                search,
                StringComparison.Ordinal) >= 0,
            "B-plane azimuth is not searched");

        Check(
            solverText.IndexOf(
                "const int arrivalSamples = 7",
                search,
                StringComparison.Ordinal) >= 0,
            "B-plane arrival epoch is not searched");

        Check(
            solverText.IndexOf(
                "TryCreateEjectionAtEntry",
                search,
                StringComparison.Ordinal) >= 0,
            "terminal search does not solve Lambert to finite SOI entry");

        Check(
            solverText.IndexOf(
                "trialAssessment.PredictedEncounter",
                search,
                StringComparison.Ordinal) >= 0,
            "terminal search can promote non-encounter geometry");
    }

    private static void HyperbolicSoiExit()
    {
        double mu = 1.0;

        StateVector initial =
            new StateVector(
                new Vector3d(1.0, 0.0, 0.0),
                new Vector3d(0.35, 1.55, 0.85),
                1234.0,
                mu,
                "Origin");

        double exitUt;
        StateVector exitState;

        Check(
            HyperbolicSoiExitSolver.TrySolve(
                initial,
                25.0,
                out exitUt,
                out exitState),
            "analytic 3D hyperbolic SOI crossing rejected");

        Check(
            exitState != null,
            "analytic SOI exit state missing");

        Near(
            exitState.Position.Magnitude,
            25.0,
            2e-8);

        Check(
            exitUt > initial.UniversalTimeSeconds,
            "analytic SOI exit is not in the future");

        double initialEnergy =
            0.5 *
                Vector3d.Dot(
                    initial.Velocity,
                    initial.Velocity) -
            mu /
                initial.Position.Magnitude;

        double exitEnergy =
            0.5 *
                Vector3d.Dot(
                    exitState.Velocity,
                    exitState.Velocity) -
            mu /
                exitState.Position.Magnitude;

        Near(
            exitEnergy,
            initialEnergy,
            2e-10);

        Vector3d initialH =
            Vector3d.Cross(
                initial.Position,
                initial.Velocity);

        Vector3d exitH =
            Vector3d.Cross(
                exitState.Position,
                exitState.Velocity);

        Near(
            exitH.Magnitude,
            initialH.Magnitude,
            2e-10);
    }

    private static void TargetSoiDiagnostics()
    {
        TargetSoiShootingResult result;
        string failureReason;

        Check(
            !TargetSoiShootingSolver.TrySolve(
                null,
                null,
                null,
                null,
                null,
                double.NaN,
                out result,
                out failureReason),
            "invalid shooting input unexpectedly succeeded");

        Check(result == null, "invalid shooting returned a result");
        Check(
            !string.IsNullOrWhiteSpace(failureReason),
            "shooting rejection did not report a diagnostic");
        Check(
            failureReason == "INVALID INPUT / MISSING BODY DATA",
            "unexpected shooting diagnostic: " + failureReason);
    }


    private static void DirectShootingBootstrap()
    {
        OrbitMapBody origin, destination;
        OrbitMapPacket packet;
        Fixture(0, out origin, out destination, out packet);

        packet.ReferenceBodyName = origin.Name;
        packet.ActiveOrbit.ReferenceBodyName = origin.Name;

        origin.RadiusMeters = packet.ReferenceBodyRadiusMeters;
        origin.SoiRadiusMeters = 500000.0;

        destination.RadiusMeters = 100000.0;
        destination.SoiRadiusMeters = 5000000.0;
        destination.GravParameter = 2.5e8;

        // Deliberately stress inclination/eccentricity beyond the old Duna/Eve cases.
        destination.Orbit.InclinationDegrees = 12.0;
        destination.Orbit.Eccentricity = 0.18;
        destination.Orbit.ArgumentOfPeriapsisDegrees = 75.0;
        destination.Orbit.LongitudeOfAscendingNodeDegrees = 110.0;

        origin.Orbit.ReferenceBodyName = origin.ParentName;
        destination.Orbit.ReferenceBodyName = destination.ParentName;

        packet.Bodies.Add(origin);
        packet.Bodies.Add(destination);

        TransferWindowSolution hohmann;

        Check(
            OrbitMapNavigationAdapter.TryCalculateTransferWindow(
                origin,
                destination,
                packet.UniversalTimeSeconds,
                out hohmann),
            "3D bootstrap Hohmann seed unavailable");

        TransferSearchSolution coarse;
        string muSource;

        Check(
            OrbitMapNavigationAdapter.TryCalculateLambertPreview(
                packet,
                origin,
                destination,
                hohmann,
                out coarse,
                out muSource),
            "3D bootstrap coarse Lambert unavailable");

        LambertParkingOrbitEjectionSolution ejection;

        Check(
            OrbitMapNavigationAdapter.TryCalculateLambertParkingOrbitEjectionPreview(
                packet,
                origin,
                coarse,
                out ejection),
            "3D bootstrap local ejection unavailable");

        TargetSoiShootingResult shooting;

        Check(
            TargetSoiShootingSolver.TrySolve(
                coarse,
                ejection,
                OrbitMapNavigationAdapter.ToElements(
                    packet.ActiveOrbit),
                OrbitMapNavigationAdapter.ToBody(
                    origin),
                OrbitMapNavigationAdapter.ToBody(
                    destination),
                1e12,
                out shooting),
            "direct 3D target shooting failed");

        Check(shooting != null, "direct 3D shooting result missing");
        Check(shooting.CorrectedEjection != null, "direct 3D corrected ejection missing");
        Check(shooting.CorrectedAssessment != null, "direct 3D assessment missing");
    }

    private static void PlanetGeometryRegressionMatrix()
    {
        // These are geometry classes intentionally modeled after the stock
        // same-parent planetary extremes. Runtime code remains body-agnostic.
        double[,] cases =
        {
            // eccentricity, inclination deg, SMA scale, SOI scale
            { 0.20, 7.0, 0.45, 0.35 },   // inner / high-inclination class (Moho-like)
            { 0.15, 5.0, 2.10, 0.20 },   // eccentric inclined outer class (Dres-like)
            { 0.05, 1.3, 3.80, 4.50 },   // giant-planet / huge-SOI class (Jool-like)
            { 0.26, 6.0, 5.80, 0.75 }    // distant eccentric outer class (Eeloo-like)
        };

        for (int i = 0; i < cases.GetLength(0); i++)
        {
            OrbitMapBody origin, destination;
            OrbitMapPacket packet;
            Fixture(0, out origin, out destination, out packet);

            packet.ReferenceBodyName = origin.Name;
            packet.ActiveOrbit.ReferenceBodyName = origin.Name;

            origin.RadiusMeters = packet.ReferenceBodyRadiusMeters;
            origin.SoiRadiusMeters = 500000.0;

            destination.RadiusMeters = 100000.0;
            destination.GravParameter = 2.5e8;
            destination.SoiRadiusMeters =
                5000000.0 * cases[i, 3];

            destination.Orbit.SemiMajorAxisMeters =
                origin.Orbit.SemiMajorAxisMeters * cases[i, 2];

            destination.Orbit.Eccentricity =
                cases[i, 0];

            destination.Orbit.InclinationDegrees =
                cases[i, 1];

            destination.Orbit.LongitudeOfAscendingNodeDegrees =
                25.0 + 31.0 * i;

            destination.Orbit.ArgumentOfPeriapsisDegrees =
                40.0 + 37.0 * i;

            destination.Orbit.PeriodSeconds =
                2.0 * Math.PI *
                Math.Sqrt(
                    destination.Orbit.SemiMajorAxisMeters *
                    destination.Orbit.SemiMajorAxisMeters *
                    destination.Orbit.SemiMajorAxisMeters /
                    1e12);

            origin.Orbit.ReferenceBodyName = origin.ParentName;
            destination.Orbit.ReferenceBodyName = destination.ParentName;

            packet.Bodies.Add(origin);
            packet.Bodies.Add(destination);

            TransferWindowSolution hohmann;

            Check(
                OrbitMapNavigationAdapter.TryCalculateTransferWindow(
                    origin,
                    destination,
                    packet.UniversalTimeSeconds,
                    out hohmann),
                "matrix Hohmann seed unavailable at case " + i);

            TransferSearchSolution coarse;
            string muSource;

            Check(
                OrbitMapNavigationAdapter.TryCalculateLambertPreview(
                    packet,
                    origin,
                    destination,
                    hohmann,
                    out coarse,
                    out muSource),
                "matrix Lambert unavailable at case " + i);

            LambertParkingOrbitEjectionSolution ejection;

            Check(
                OrbitMapNavigationAdapter.TryCalculateLambertParkingOrbitEjectionPreview(
                    packet,
                    origin,
                    coarse,
                    out ejection),
                "matrix 3D ejection unavailable at case " + i);

            TargetSoiShootingResult shooting;

            Check(
                TargetSoiShootingSolver.TrySolve(
                    coarse,
                    ejection,
                    OrbitMapNavigationAdapter.ToElements(
                        packet.ActiveOrbit),
                    OrbitMapNavigationAdapter.ToBody(
                        origin),
                    OrbitMapNavigationAdapter.ToBody(
                        destination),
                    1e12,
                    out shooting),
                "matrix target shooting failed at case " + i);

            Check(
                shooting != null &&
                shooting.CorrectedAssessment != null,
                "matrix shooting result missing at case " + i);

            // A regression case must at minimum remain finite and improve or
            // establish the target encounter. We do not hardcode body-specific outcomes.
            Check(
                shooting.CorrectedAssessment.PredictedEncounter ||
                shooting.CorrectedAssessment.MissDistanceMeters <=
                    shooting.InitialAssessment.MissDistanceMeters + 1e-6,
                "matrix shooting regressed at case " + i);
        }
    }

    private static LambertParkingOrbitEjectionSolution SyntheticProductionEjection(
        double burnUt,
        double prograde,
        double normal,
        double radial)
    {
        LambertParkingOrbitEjectionSolution ejection =
            new LambertParkingOrbitEjectionSolution();

        SetInternalProperty(ejection, "BurnUniversalTimeSeconds", burnUt);
        SetInternalProperty(ejection, "ProgradeDeltaVMetersPerSecond", prograde);
        SetInternalProperty(ejection, "NormalDeltaVMetersPerSecond", normal);
        SetInternalProperty(ejection, "RadialDeltaVMetersPerSecond", radial);
        SetInternalProperty(
            ejection,
            "TotalDeltaVMetersPerSecond",
            Math.Sqrt(
                prograde * prograde +
                normal * normal +
                radial * radial));

        return ejection;
    }

    private static void SetInternalProperty(
        object target,
        string propertyName,
        object value)
    {
        PropertyInfo property =
            target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);

        Check(property != null, "missing property " + propertyName);

        MethodInfo setter = property.GetSetMethod(true);
        Check(setter != null, "missing setter " + propertyName);
        setter.Invoke(target, new object[] { value });
    }

    private static void ProductionAuthorityPacket()
    {
        const string destinationName = "Destination";

        // Packet-authority selection is intentionally tested with explicit
        // P/N/R candidates. Do not make this regression depend on transfer
        // search, parking geometry, or any other planner merely to manufacture
        // an ejection object: those systems have their own dedicated tests.
        LambertParkingOrbitEjectionSolution coupledEjection =
            SyntheticProductionEjection(
                22222.0,
                901.25,
                -37.5,
                18.75);

        LambertParkingOrbitEjectionSolution lambertEjection =
            SyntheticProductionEjection(
                33333.0,
                812.5,
                24.0,
                -11.5);

        ManeuverUplinkPacket legacy =
            new ManeuverUplinkPacket
            {
                VesselId = "TEST-VESSEL",
                NodeUniversalTimeSeconds = 12345.0,
                ProgradeDeltaVMetersPerSecond = 321.0,
                NormalDeltaVMetersPerSecond = 0.0,
                RadialDeltaVMetersPerSecond = 0.0,
                TargetBodyName = destinationName,
                Operation = "CREATE"
            };

        var method =
            typeof(MapPage).GetMethod(
                "BuildProductionNodePacket",
                BindingFlags.Static | BindingFlags.NonPublic);

        Check(method != null, "production packet builder missing");

        // 14.22.64: coupled authority must win when supplied, and its full
        // P/N/R impulse must be preserved rather than using Lambert or legacy.
        object[] coupledArgs =
            new object[]
            {
                "TEST-VESSEL",
                destinationName,
                coupledEjection,
                lambertEjection,
                legacy,
                "MAP-XFER-COUPLED",
                false,
                false
            };

        ManeuverUplinkPacket coupled =
            (ManeuverUplinkPacket)method.Invoke(
                null,
                coupledArgs);

        Check(coupled != null, "production coupled packet is null");
        Check((bool)coupledArgs[6], "coupled authority flag not set");
        Check(!(bool)coupledArgs[7], "coupled packet incorrectly marked Lambert");
        Near(
            coupled.NodeUniversalTimeSeconds,
            coupledEjection.BurnUniversalTimeSeconds,
            1e-9);
        Near(
            coupled.ProgradeDeltaVMetersPerSecond,
            coupledEjection.ProgradeDeltaVMetersPerSecond,
            1e-9);
        Near(
            coupled.NormalDeltaVMetersPerSecond,
            coupledEjection.NormalDeltaVMetersPerSecond,
            1e-9);
        Near(
            coupled.RadialDeltaVMetersPerSecond,
            coupledEjection.RadialDeltaVMetersPerSecond,
            1e-9);

        // If coupled authority is unavailable, retain the previous Lambert
        // P/N/R authority path unchanged.
        object[] lambertArgs =
            new object[]
            {
                "TEST-VESSEL",
                destinationName,
                null,
                lambertEjection,
                legacy,
                "MAP-XFER-LAMBERT",
                true,
                false
            };

        ManeuverUplinkPacket lambert =
            (ManeuverUplinkPacket)method.Invoke(
                null,
                lambertArgs);

        Check(lambert != null, "production Lambert packet is null");
        Check(!(bool)lambertArgs[6], "Lambert packet incorrectly marked coupled");
        Check((bool)lambertArgs[7], "Lambert authority flag not set");
        Near(
            lambert.NodeUniversalTimeSeconds,
            lambertEjection.BurnUniversalTimeSeconds,
            1e-9);
        Near(
            lambert.ProgradeDeltaVMetersPerSecond,
            lambertEjection.ProgradeDeltaVMetersPerSecond,
            1e-9);
        Near(
            lambert.NormalDeltaVMetersPerSecond,
            lambertEjection.NormalDeltaVMetersPerSecond,
            1e-9);
        Near(
            lambert.RadialDeltaVMetersPerSecond,
            lambertEjection.RadialDeltaVMetersPerSecond,
            1e-9);

        // With neither coupled nor Lambert authority, the old prograde-only
        // candidate remains the final safety fallback.
        object[] fallbackArgs =
            new object[]
            {
                "TEST-VESSEL",
                destinationName,
                null,
                null,
                legacy,
                "MAP-XFER-FALLBACK",
                true,
                true
            };

        ManeuverUplinkPacket fallback =
            (ManeuverUplinkPacket)method.Invoke(
                null,
                fallbackArgs);

        Check(fallback != null, "legacy fallback packet is null");
        Check(!(bool)fallbackArgs[6], "fallback incorrectly marked coupled");
        Check(!(bool)fallbackArgs[7], "fallback incorrectly marked Lambert");
        Near(
            fallback.NodeUniversalTimeSeconds,
            legacy.NodeUniversalTimeSeconds,
            1e-9);
        Near(
            fallback.ProgradeDeltaVMetersPerSecond,
            legacy.ProgradeDeltaVMetersPerSecond,
            1e-9);
        Near(fallback.NormalDeltaVMetersPerSecond, 0.0, 1e-12);
        Near(fallback.RadialDeltaVMetersPerSecond, 0.0, 1e-12);
    }



    private static void OperatorTargetPeriapsis()
    {
        MethodInfo[] methods =
            typeof(CoupledFiniteSoiOptimizer).GetMethods(
                BindingFlags.Public | BindingFlags.Static);

        bool foundOverride = false;
        for (int i = 0; i < methods.Length; i++)
        {
            if (!string.Equals(methods[i].Name, "TrySolve", StringComparison.Ordinal))
                continue;

            ParameterInfo[] parameters = methods[i].GetParameters();
            if (parameters.Length == 9 &&
                parameters[6].ParameterType == typeof(double))
            {
                foundOverride = true;
                break;
            }
        }

        Check(foundOverride,
            "coupled production solve does not expose desired periapsis input");

        MethodInfo autoMethod =
            typeof(MapPage).GetMethod(
                "ComputeAutoTargetPeriapsisRadius",
                BindingFlags.Static | BindingFlags.NonPublic);

        Check(autoMethod != null,
            "MAP automatic periapsis helper missing");

        OrbitMapBody body = new OrbitMapBody
        {
            RadiusMeters = 320000.0,
            SoiRadiusMeters = 47921949.0
        };

        double automatic =
            (double)autoMethod.Invoke(null, new object[] { body });

        Near(automatic, 640000.0, 1e-6);
    }


    private static void HumanReadableMissionTimeDisplay()
    {
        /*
         * KSP Kerbin-time convention: 6-hour days.
         * 90061 s = 4d 01h 01m 01s.
         */
        OrbitMapSnapshotStore.Clear();

        OrbitMapPacket kerbin =
            new OrbitMapPacket
            {
                CalendarDayLengthSeconds = 21600.0
            };

        OrbitMapSnapshotStore.SetLatest(
            kerbin,
            DateTime.UtcNow);

        const double sample = 90061.0;
        const string kerbinExpected = "4d 01h 01m 01s";

        CheckPrivateTimeFormatter(typeof(MapPage), "FormatTransferInterval", sample, kerbinExpected);
        CheckPrivateTimeFormatter(typeof(ManeuverPage), "FormatDuration", sample, kerbinExpected);
        CheckPrivateTimeFormatter(typeof(GuidancePage), "FormatDuration", sample, kerbinExpected);
        CheckPrivateTimeFormatter(typeof(OrbitPage), "FormatDuration", sample, kerbinExpected);
        CheckPrivateTimeFormatter(
            typeof(KMC.MissionControl.Rendering.Ascent.FooterRenderer),
            "FormatMissionTime",
            sample,
            kerbinExpected);

        OrbitMapPacket earth =
            new OrbitMapPacket
            {
                CalendarDayLengthSeconds = 86400.0
            };

        OrbitMapSnapshotStore.SetLatest(
            earth,
            DateTime.UtcNow);

        Check(
            string.Equals(
                KMC.MissionControl.Telemetry.KspTimeDisplay.FormatDuration(sample),
                "1d 01h 01m 01s",
                StringComparison.Ordinal),
            "Earth-style KSP day length not honored");

        OrbitMapSnapshotStore.Clear();
    }

    private static void CheckPrivateTimeFormatter(
        Type type,
        string methodName,
        double seconds,
        string expected)
    {
        MethodInfo method =
            type.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);

        Check(method != null, type.Name + "." + methodName + " missing");

        string actual =
            method.Invoke(null, new object[] { seconds }) as string;

        Check(
            string.Equals(actual, expected, StringComparison.Ordinal),
            type.Name + "." + methodName +
            " expected " + expected + " got " + actual);
    }

    private static void TransferExecutionSummaryRegression()
    {
        string sourcePath =
            Path.Combine(
                Environment.CurrentDirectory,
                "KMC.MissionControl",
                "Pages",
                "MapPage.cs");

        if (!File.Exists(sourcePath))
        {
            sourcePath =
                Path.GetFullPath(
                    Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "..",
                        "..",
                        "..",
                        "..",
                        "KMC.MissionControl",
                        "Pages",
                        "MapPage.cs"));
        }

        Check(
            File.Exists(sourcePath),
            "MapPage source unavailable for execution-summary regression");

        string source =
            File.ReadAllText(sourcePath);

        Check(
            source.IndexOf(
                "TRANSFER EXECUTION",
                StringComparison.Ordinal) >= 0,
            "transfer execution heading missing");

        Check(
            source.IndexOf(
                "NEXT ACTION",
                StringComparison.Ordinal) >= 0,
            "next-action handoff missing");

        Check(
            source.IndexOf(
                "EXECUTE MANEUVER NODE",
                StringComparison.Ordinal) >= 0,
            "maneuver execution instruction missing");

        Check(
            source.IndexOf(
                "KSP PE ALT",
                StringComparison.Ordinal) >= 0,
            "KSP periapsis altitude summary missing");

        Check(
            source.IndexOf(
                "REFERENCE WINDOW / HOHMANN GUIDE",
                StringComparison.Ordinal) >= 0,
            "reference-window clarification missing");

        Check(
            source.IndexOf(
                "STATUS NODE CREATED / READY FOR GUIDANCE",
                StringComparison.Ordinal) >= 0,
            "upper transfer status does not reflect created node");
    }

    private static void TransferManeuverHandoffRegression()
    {
        string sourcePath =
            Path.Combine(
                Environment.CurrentDirectory,
                "KMC.MissionControl",
                "Pages",
                "MapPage.cs");

        if (!File.Exists(sourcePath))
        {
            sourcePath =
                Path.GetFullPath(
                    Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "..",
                        "..",
                        "..",
                        "..",
                        "KMC.MissionControl",
                        "Pages",
                        "MapPage.cs"));
        }

        Check(
            File.Exists(sourcePath),
            "MapPage source unavailable for transfer-handoff regression");

        string source =
            File.ReadAllText(sourcePath);

        Check(
            source.IndexOf(
                "TransferManeuverPlanBridge.RegisterAndPromote",
                StringComparison.Ordinal) >= 0,
            "successful MAP uplink does not register transfer maneuver");

        Check(
            source.IndexOf(
                "KmcManeuverPlanStore.Capture",
                StringComparison.Ordinal) >= 0 &&
            source.IndexOf(
                "KmcManeuverPlanStore.Select",
                StringComparison.Ordinal) >= 0,
            "MAP transfer is not retained/selected as a KMC maneuver");

        Check(
            source.IndexOf(
                "ManeuverPlanPromotionStore.Publish",
                StringComparison.Ordinal) >= 0,
            "MAP transfer is not promoted into Engine maneuver ownership");

        Check(
            source.IndexOf(
                "ManeuverPlanModel.CreateFromPromotionRequest",
                StringComparison.Ordinal) >= 0,
            "MAP transfer bypasses Engine-owned maneuver-plan construction");

        Check(
            source.IndexOf(
                "ManeuverUplinkStatusStore.RepublishGuidanceForPlan",
                StringComparison.Ordinal) >= 0,
            "MAP transfer does not republish node verification to GUID");
    }

    private static void MixedManeuverGuidanceRegression()
    {
        string sourcePath =
            Path.Combine(
                Environment.CurrentDirectory,
                "KMC.Engine",
                "Guidance",
                "GuidanceSystem.cs");

        if (!File.Exists(sourcePath))
        {
            sourcePath =
                Path.GetFullPath(
                    Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "..",
                        "..",
                        "..",
                        "..",
                        "KMC.Engine",
                        "Guidance",
                        "GuidanceSystem.cs"));
        }

        Check(
            File.Exists(sourcePath),
            "GuidanceSystem source unavailable for mixed-vector regression");

        string source =
            File.ReadAllText(sourcePath);

        Check(
            source.IndexOf(
                "IsMixedManeuver(plan)",
                StringComparison.Ordinal) >= 0,
            "mixed P/N/R maneuver detection missing");

        Check(
            source.IndexOf(
                "TRUE COMBINED MANEUVER VECTOR",
                StringComparison.Ordinal) >= 0,
            "combined maneuver attitude reference missing");

        Check(
            source.IndexOf(
                "p *",
                StringComparison.Ordinal) >= 0 &&
            source.IndexOf(
                "n *",
                StringComparison.Ordinal) >= 0 &&
            source.IndexOf(
                "r *",
                StringComparison.Ordinal) >= 0,
            "combined maneuver does not use all P/N/R coefficients");

        Check(
            source.IndexOf(
                "right /= resultantMagnitude",
                StringComparison.Ordinal) >= 0 &&
            source.IndexOf(
                "nose /= resultantMagnitude",
                StringComparison.Ordinal) >= 0 &&
            source.IndexOf(
                "forward /= resultantMagnitude",
                StringComparison.Ordinal) >= 0,
            "combined maneuver resultant is not normalized");

        Check(
            source.IndexOf(
                "_burnPlanSnapshot",
                StringComparison.Ordinal) >= 0 &&
            source.IndexOf(
                "ManeuverPlanModel.Clone",
                StringComparison.Ordinal) >= 0,
            "mixed maneuver vector is not preserved through burn execution");
    }

    private static void OrbitMapAuthoritativeManeuverVectorRoundTrip()
    {
        OrbitMapPacket packet =
            new OrbitMapPacket
            {
                Sequence = 1,
                TimestampUtc = DateTime.UtcNow,
                VesselId = "VESSEL",
                VesselName = "TEST",
                UniversalTimeSeconds = 1000.0,
                ReferenceBodyName = "Kerbin",
                ReferenceBodyRadiusMeters = 600000.0,
                ReferenceBodySoiRadiusMeters = 84159286.0,
                ActiveOrbit =
                    new OrbitMapOrbit
                    {
                        ReferenceBodyName = "Kerbin",
                        SemiMajorAxisMeters = 700000.0,
                        Eccentricity = 0.0,
                        InclinationDegrees = 0.0,
                        LongitudeOfAscendingNodeDegrees = 0.0,
                        ArgumentOfPeriapsisDegrees = 0.0,
                        EpochUniversalTimeSeconds = 0.0,
                        MeanAnomalyAtEpochRadians = 0.0,
                        ApoapsisMeters = 100000.0,
                        PeriapsisMeters = 100000.0,
                        PeriodSeconds = 2000.0,
                        PositionX = 700000.0,
                        PositionY = 0.0,
                        PositionZ = 0.0
                    }
            };

        packet.ManeuverNodes.Add(
            new OrbitMapManeuverNode
            {
                Index = 0,
                UniversalTimeSeconds = 5000.0,
                ProgradeDeltaVMetersPerSecond = 1000.0,
                NormalDeltaVMetersPerSecond = -100.0,
                RadialDeltaVMetersPerSecond = 20.0,
                TotalDeltaVMetersPerSecond =
                    Math.Sqrt(
                        1000.0 * 1000.0 +
                        100.0 * 100.0 +
                        20.0 * 20.0),
                PositionX = 1.0,
                PositionY = 2.0,
                PositionZ = 3.0,
                PatchIndex = 0,
                AuthoritativeBurnVectorAvailable = true,
                AuthoritativeBurnRight = 0.25,
                AuthoritativeBurnNose = 0.75,
                AuthoritativeBurnReferenceForward =
                    Math.Sqrt(0.375)
            });

        string serialized =
            packet.Serialize();

        OrbitMapPacket parsed;

        Check(
            OrbitMapPacket.TryParse(
                serialized,
                out parsed),
            "extended OrbitMap packet did not parse");

        Check(
            parsed != null &&
            parsed.ManeuverNodes.Count == 1,
            "extended OrbitMap maneuver node missing");

        OrbitMapManeuverNode node =
            parsed.ManeuverNodes[0];

        Check(
            node.AuthoritativeBurnVectorAvailable,
            "authoritative burn-vector flag lost");

        Near(
            node.AuthoritativeBurnRight,
            0.25,
            1e-12);

        Near(
            node.AuthoritativeBurnNose,
            0.75,
            1e-12);

        Near(
            node.AuthoritativeBurnReferenceForward,
            Math.Sqrt(0.375),
            1e-12);

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
    }

    private static void ManeuverVectorStoreExactMatch()
    {
        DateTime now =
            DateTime.UtcNow;

        ManeuverVectorTelemetryStore.Clear();

        ManeuverVectorTelemetryStore.Publish(
            new ManeuverVectorTelemetryModel
            {
                TelemetryAvailable = true,
                SourceTimestampUtc = now,
                ReceivedUtc = now,
                VesselId = "VESSEL-A",
                NodeUniversalTimeSeconds = 1000.0,
                RightComponent = 0.0,
                NoseComponent = 1.0,
                ReferenceForwardComponent = 0.0,
                BurnMagnitudeMetersPerSecond = 100.0
            });

        ManeuverVectorTelemetryStore.Publish(
            new ManeuverVectorTelemetryModel
            {
                TelemetryAvailable = true,
                SourceTimestampUtc = now,
                ReceivedUtc = now,
                VesselId = "VESSEL-A",
                NodeUniversalTimeSeconds = 2000.0,
                RightComponent = 1.0,
                NoseComponent = 0.0,
                ReferenceForwardComponent = 0.0,
                BurnMagnitudeMetersPerSecond = 200.0
            });

        ManeuverVectorTelemetryModel first =
            ManeuverVectorTelemetryStore.FindForPlan(
                "VESSEL-A",
                1000.0,
                now);

        Check(
            first != null &&
            first.TelemetryAvailable,
            "first authoritative maneuver-vector sample not found");

        Near(
            first.RightComponent,
            0.0,
            1e-12);

        Near(
            first.NoseComponent,
            1.0,
            1e-12);

        ManeuverVectorTelemetryModel second =
            ManeuverVectorTelemetryStore.FindForPlan(
                "VESSEL-A",
                2000.0,
                now);

        Check(
            second != null &&
            second.TelemetryAvailable,
            "second authoritative maneuver-vector sample not found");

        Near(
            second.RightComponent,
            1.0,
            1e-12);

        Near(
            second.NoseComponent,
            0.0,
            1e-12);

        ManeuverVectorTelemetryModel wrongVessel =
            ManeuverVectorTelemetryStore.FindForPlan(
                "VESSEL-B",
                1000.0,
                now);

        Check(
            wrongVessel != null &&
            !wrongVessel.TelemetryAvailable,
            "authoritative vector crossed vessel identity");

        ManeuverVectorTelemetryModel wrongNode =
            ManeuverVectorTelemetryStore.FindForPlan(
                "VESSEL-A",
                3000.0,
                now);

        Check(
            wrongNode != null &&
            !wrongNode.TelemetryAvailable,
            "authoritative vector crossed node identity");

        ManeuverVectorTelemetryStore.Clear();
    }

    private static void StandaloneManeuverVectorUdpRemoved()
    {
        string pluginPath =
            Path.Combine(
                Environment.CurrentDirectory,
                "KMC.Plugin",
                "VelocityVectorTelemetrySender.cs");

        string transportPath =
            Path.Combine(
                Environment.CurrentDirectory,
                "KMC.MissionControl",
                "Transport",
                "VelocityVectorTelemetrySample.cs");

        Check(
            File.Exists(pluginPath),
            "VelocityVectorTelemetrySender source unavailable");

        Check(
            File.Exists(transportPath),
            "VelocityVectorTelemetrySample source unavailable");

        string plugin =
            File.ReadAllText(
                pluginPath);

        string transport =
            File.ReadAllText(
                transportPath);

        Check(
            plugin.IndexOf(
                "KMC-MNVV1",
                StringComparison.Ordinal) < 0 &&
            plugin.IndexOf(
                "ManeuverVectorTelemetryPort",
                StringComparison.Ordinal) < 0,
            "standalone plugin maneuver-vector UDP sender still present");

        Check(
            transport.IndexOf(
                "ManeuverVectorTelemetryReceiver",
                StringComparison.Ordinal) < 0,
            "standalone Mission Control maneuver-vector UDP receiver still present");
    }

    private static void ManeuverExecutionReadinessRegression()
    {
        string enginePath =
            Path.Combine(
                Environment.CurrentDirectory,
                "KMC.Engine",
                "Guidance",
                "GuidanceSystem.cs");

        string modelPath =
            Path.Combine(
                Environment.CurrentDirectory,
                "KMC.Engine",
                "Guidance",
                "GuidanceSolutionModel.cs");

        string guidancePagePath =
            Path.Combine(
                Environment.CurrentDirectory,
                "KMC.MissionControl",
                "Pages",
                "GuidancePage.cs");

        string mapPagePath =
            Path.Combine(
                Environment.CurrentDirectory,
                "KMC.MissionControl",
                "Pages",
                "MapPage.cs");

        string maneuverPagePath =
            Path.Combine(
                Environment.CurrentDirectory,
                "KMC.MissionControl",
                "Pages",
                "ManeuverPage.cs");

        Check(
            File.Exists(enginePath) &&
            File.Exists(modelPath) &&
            File.Exists(guidancePagePath) &&
            File.Exists(mapPagePath) &&
            File.Exists(maneuverPagePath),
            "readiness regression source unavailable");

        string engine =
            File.ReadAllText(enginePath);

        string model =
            File.ReadAllText(modelPath);

        string guidancePage =
            File.ReadAllText(guidancePagePath);

        string mapPage =
            File.ReadAllText(mapPagePath);

        string maneuverPage =
            File.ReadAllText(maneuverPagePath);

        Check(
            model.IndexOf(
                "public string BurnReadiness",
                StringComparison.Ordinal) >= 0 &&
            engine.IndexOf(
                "PopulateBurnReadiness(_latest);",
                StringComparison.Ordinal) >= 0,
            "burn readiness is not Engine-owned");

        Check(
            engine.IndexOf(
                "!solution.NodeVerified",
                StringComparison.Ordinal) >= 0 &&
            engine.IndexOf(
                "!solution.ExecutionAuthorized",
                StringComparison.Ordinal) >= 0 &&
            engine.IndexOf(
                "!solution.ManeuverVectorAvailable",
                StringComparison.Ordinal) >= 0 &&
            engine.IndexOf(
                "AlignmentHoldToleranceDegrees",
                StringComparison.Ordinal) >= 0 &&
            engine.IndexOf(
                "TimeToIgnitionSeconds",
                StringComparison.Ordinal) >= 0,
            "readiness does not use verified node/vector/alignment/timing state");

        Check(
            engine.IndexOf(
                "\"ALIGN REQUIRED\"",
                StringComparison.Ordinal) >= 0 &&
            engine.IndexOf(
                "\"READY\"",
                StringComparison.Ordinal) >= 0 &&
            engine.IndexOf(
                "\"FINAL COUNT\"",
                StringComparison.Ordinal) >= 0 &&
            engine.IndexOf(
                "\"IGNITION DUE\"",
                StringComparison.Ordinal) >= 0,
            "crew readiness states incomplete");

        Check(
            guidancePage.IndexOf(
                "\"BURN STATUS\"",
                StringComparison.Ordinal) >= 0 &&
            guidancePage.IndexOf(
                "guidance.BurnReadiness",
                StringComparison.Ordinal) >= 0,
            "GUID does not expose explicit burn status");

        Check(
            mapPage.IndexOf(
                "STATUS NODE CREATED / READY FOR GUIDANCE",
                StringComparison.Ordinal) >= 0,
            "MAP still claims burn readiness before GUID");

        Check(
            maneuverPage.IndexOf(
                "NODE VERIFIED / READY FOR GUIDANCE",
                StringComparison.Ordinal) >= 0,
            "MNV handoff does not identify guidance readiness");
    }

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

    private static void ManeuverOperatorFeedbackRegression()
    {
        string mainPath =
            Path.Combine(
                Environment.CurrentDirectory,
                "KMC.MissionControl",
                "MainForm.cs");

        string pagePath =
            Path.Combine(
                Environment.CurrentDirectory,
                "KMC.MissionControl",
                "Pages",
                "ManeuverPage.cs");

        string storePath =
            Path.Combine(
                Environment.CurrentDirectory,
                "KMC.MissionControl",
                "Engineering",
                "ManeuverOperatorFeedbackStore.cs");

        Check(
            File.Exists(mainPath) &&
            File.Exists(pagePath) &&
            File.Exists(storePath),
            "MNV operator-feedback source unavailable");

        string main =
            File.ReadAllText(
                mainPath);

        string page =
            File.ReadAllText(
                pagePath);

        string store =
            File.ReadAllText(
                storePath);

        Check(
            main.IndexOf(
                "\"COMPUTE REQUESTED - WAITING FOR ENGINE PLAN\"",
                StringComparison.Ordinal) >= 0,
            "COMPUTE action does not publish immediate feedback");

        Check(
            main.IndexOf(
                "bool uploadAccepted =",
                StringComparison.Ordinal) >= 0 &&
            main.IndexOf(
                "ManeuverOperatorFeedbackStore.Publish",
                StringComparison.Ordinal) >= 0,
            "UPLOAD MNV still discards receiver result");

        Check(
            page.IndexOf(
                "ManeuverOperatorFeedbackStore.TryGetLatest",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "operatorFeedback.Text",
                StringComparison.Ordinal) >= 0,
            "MNV page does not surface action feedback");

        Check(
            store.IndexOf(
                "TimeSpan.FromSeconds(8.0)",
                StringComparison.Ordinal) >= 0,
            "operator feedback is not bounded / short-lived");
    }

    private static void ManualManeuverBurnEstimateDecouplingRegression()
    {
        string plannerPath =
            Path.Combine(
                Environment.CurrentDirectory,
                "KMC.Engine",
                "Maneuver",
                "ApsisManeuverPlanner.cs");

        Check(
            File.Exists(plannerPath),
            "manual maneuver planner source unavailable");

        string planner =
            File.ReadAllText(
                plannerPath);

        int manualStart =
            planner.IndexOf(
                "private ManeuverPlanModel CalculateManualProgradeRetrograde(",
                StringComparison.Ordinal);

        int manualEnd =
            planner.IndexOf(
                "private static bool ValidateTargetPeriapsis(",
                manualStart,
                StringComparison.Ordinal);

        Check(
            manualStart >= 0 &&
            manualEnd > manualStart,
            "manual maneuver planner section unavailable");

        string manualSection =
            planner.Substring(
                manualStart,
                manualEnd - manualStart);

        Check(
            manualSection.IndexOf(
                "plan.Status = \"BURN ESTIMATE UNAVAILABLE\"",
                StringComparison.Ordinal) < 0,
            "manual maneuver still hard-rejects unavailable burn estimate");

        int evidenceCount =
            System.Text.RegularExpressions.Regex.Matches(
                manualSection,
                System.Text.RegularExpressions.Regex.Escape(
                    "Manual node remains valid even when burn-duration telemetry is unavailable.")).Count;

        Check(
            evidenceCount == 3,
            "all three manual maneuver axes must preserve valid node without burn estimate");

        Check(
            manualSection.IndexOf(
                "? burnDuration / 2.0",
                StringComparison.Ordinal) >= 0 &&
            manualSection.IndexOf(
                ": double.NaN",
                StringComparison.Ordinal) >= 0,
            "manual maneuver does not preserve unknown burn timing explicitly");
    }

    private static void PostBurnCleanupQueueHandoffRegression()
    {
        string main = File.ReadAllText(
            Path.Combine(Environment.CurrentDirectory, "KMC.MissionControl", "MainForm.cs"));
        string mnv = File.ReadAllText(
            Path.Combine(Environment.CurrentDirectory, "KMC.MissionControl", "Pages", "ManeuverPage.cs"));
        string guid = File.ReadAllText(
            Path.Combine(Environment.CurrentDirectory, "KMC.MissionControl", "Pages", "GuidancePage.cs"));

        Check(
            guid.IndexOf("\"NEXT ACTION\"", StringComparison.Ordinal) >= 0 &&
            guid.IndexOf("\"REMOVE COMPLETED NODE\"", StringComparison.Ordinal) >= 0 &&
            guid.IndexOf("\"CLEANUP COMPLETE\"", StringComparison.Ordinal) >= 0,
            "GUID post-burn next action missing");

        Check(
            main.IndexOf("SelectCompletedActiveNodeForCleanup", StringComparison.Ordinal) >= 0 &&
            main.IndexOf("\"REMOVE COMPLETE NODE\"", StringComparison.Ordinal) >= 0,
            "MNV completed-node cleanup focus missing");

        Check(
            main.IndexOf("\"REMOVE COMMAND SENT - AWAITING NODE REMOVED\"", StringComparison.Ordinal) >= 0,
            "completed-node removal feedback missing");

        Check(
            mnv.IndexOf("\"CLEANUP COMPLETE - ACTIVATE KMC #\"", StringComparison.Ordinal) >= 0 &&
            mnv.IndexOf("\"MANEUVER COMPLETE - REMOVE COMPLETED NODE\"", StringComparison.Ordinal) >= 0,
            "MNV cleanup/next-plan handoff language missing");

        Check(
            main.IndexOf("ManeuverPlanPromotionStore.Publish", StringComparison.Ordinal) >= 0 &&
            main.IndexOf("SendDelete", StringComparison.Ordinal) >= 0,
            "crew-controlled queue actions unexpectedly removed");
    }

    private static void VesselSchematicFoundationRegression()
    {
        VesselTopology topology =
            new VesselTopology
            {
                VesselId = "SCHEM-TEST",
                VesselName = "Schematic Test",
                RootPartId = 1U,
                HasRootPart = true,
                PartCount = 2,
                CurrentStage = 3,
                Revision = 7
            };

        topology.Nodes.Add(
            new VesselTopologyNode
            {
                PartId = 1U,
                PartTitle = "Root",
                VesselX = 1.25,
                VesselY = 2.5,
                VesselZ = -3.75
            });

        topology.Nodes.Add(
            new VesselTopologyNode
            {
                PartId = 2U,
                ParentPartId = 1U,
                HasParent = true,
                PartTitle = "Child",
                VesselX = -4.5,
                VesselY = 5.75,
                VesselZ = 6.25
            });

        byte[] encoded =
            VesselTopologyPacketCodec.Encode(
                topology);

        VesselTopology decoded;

        Check(
            VesselTopologyPacketCodec.TryDecode(
                encoded,
                out decoded),
            "SCHEM topology round trip failed");

        Check(
            decoded != null &&
            decoded.Nodes.Count == 2,
            "SCHEM topology node count lost");

        Near(
            decoded.Nodes[0].VesselX,
            1.25);

        Near(
            decoded.Nodes[0].VesselY,
            2.5);

        Near(
            decoded.Nodes[0].VesselZ,
            -3.75);

        Near(
            decoded.Nodes[1].VesselX,
            -4.5);

        Near(
            decoded.Nodes[1].VesselY,
            5.75);

        Near(
            decoded.Nodes[1].VesselZ,
            6.25);

        string main =
            File.ReadAllText(
                Path.Combine(
                    Environment.CurrentDirectory,
                    "KMC.MissionControl",
                    "MainForm.cs"));

        string page =
            File.ReadAllText(
                Path.Combine(
                    Environment.CurrentDirectory,
                    "KMC.MissionControl",
                    "Pages",
                    "SchematicPage.cs"));

        string receiver =
            File.ReadAllText(
                Path.Combine(
                    Environment.CurrentDirectory,
                    "KMC.MissionControl",
                    "Telemetry",
                    "VesselSchematicTelemetry.cs"));

        Check(
            main.IndexOf(
                "\"SCHEM\"",
                StringComparison.Ordinal) >= 0 &&
            main.IndexOf(
                "new SchematicPage()",
                StringComparison.Ordinal) >= 0,
            "SCHEM navigation tab missing");

        Check(
            receiver.IndexOf(
                "VesselTopologyPacketCodec.TopologyPort",
                StringComparison.Ordinal) >= 0 &&
            receiver.IndexOf(
                "VesselTopologyPacketCodec.TryDecode",
                StringComparison.Ordinal) >= 0,
            "SCHEM does not consume existing KMC topology transport");

        Check(
            page.IndexOf(
                "\"ACTIVE VESSEL / SIDE\"",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "\"SINGLE SIDE VIEW\"",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "\"FRONT\"",
                StringComparison.Ordinal) < 0 &&
            page.IndexOf(
                "\"TOP\"",
                StringComparison.Ordinal) < 0,
            "SCHEM single side-view presentation missing");

        Check(
            page.IndexOf(
                "SideOutlinePoints",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "DrawPartOutline",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "node.VesselZ",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "node.VesselY",
                StringComparison.Ordinal) >= 0,
            "SCHEM side-outline/fallback projection path incomplete");
    }

    private static void VesselSchematicGeometryRegression()
    {
        VesselTopology topology =
            new VesselTopology
            {
                VesselId = "SCHEM-GEOMETRY",
                VesselName = "Geometry Test",
                RootPartId = 10U,
                HasRootPart = true,
                PartCount = 1,
                CurrentStage = 2,
                Revision = 9
            };

        VesselTopologyNode node =
            new VesselTopologyNode
            {
                PartId = 10U,
                PartTitle = "Oriented Part",
                VesselX = 1.0,
                VesselY = 2.0,
                VesselZ = 3.0,
                GeometryAvailable = true,
                BoundsCenterX = 0.1,
                BoundsCenterY = -0.2,
                BoundsCenterZ = 0.3,
                BoundsSizeX = 1.25,
                BoundsSizeY = 2.5,
                BoundsSizeZ = 3.75,
                AxisRightX = 0.0,
                AxisRightY = 1.0,
                AxisRightZ = 0.0,
                AxisUpX = -1.0,
                AxisUpY = 0.0,
                AxisUpZ = 0.0,
                AxisForwardX = 0.0,
                AxisForwardY = 0.0,
                AxisForwardZ = 1.0
            };

        topology.Nodes.Add(
            node);

        byte[] encoded =
            VesselTopologyPacketCodec.Encode(
                topology);

        VesselTopology decoded;

        Check(
            VesselTopologyPacketCodec.TryDecode(
                encoded,
                out decoded),
            "SCHEM geometry topology decode failed");

        Check(
            decoded != null &&
            decoded.TransportVersion >= 3 &&
            decoded.Nodes.Count == 1 &&
            decoded.Nodes[0].GeometryAvailable,
            "SCHEM geometry availability/version lost");

        VesselTopologyNode actual =
            decoded.Nodes[0];

        Near(actual.BoundsCenterX, 0.1);
        Near(actual.BoundsCenterY, -0.2);
        Near(actual.BoundsCenterZ, 0.3);
        Near(actual.BoundsSizeX, 1.25);
        Near(actual.BoundsSizeY, 2.5);
        Near(actual.BoundsSizeZ, 3.75);
        Near(actual.AxisRightY, 1.0);
        Near(actual.AxisUpX, -1.0);
        Near(actual.AxisForwardZ, 1.0);

        string builder =
            File.ReadAllText(
                Path.Combine(
                    Environment.CurrentDirectory,
                    "KMC.Plugin",
                    "Topology",
                    "VesselTopologyBuilder.cs"));

        string page =
            File.ReadAllText(
                Path.Combine(
                    Environment.CurrentDirectory,
                    "KMC.MissionControl",
                    "Pages",
                    "SchematicPage.cs"));

        Check(
            builder.IndexOf(
                "GetComponentsInChildren<Renderer>",
                StringComparison.Ordinal) >= 0 &&
            builder.IndexOf(
                "InverseTransformDirection",
                StringComparison.Ordinal) >= 0 &&
            builder.IndexOf(
                "BoundsSizeX",
                StringComparison.Ordinal) >= 0,
            "KSP SCHEM geometry capture missing");

        Check(
            page.IndexOf(
                "SideOutlinePoints",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "DrawPartOutline",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "FillPolygon",
                StringComparison.Ordinal) >= 0,
            "Mission Control mesh-outline renderer missing");
    }

    private static void VesselSchematicNativeBoundsRegression()
    {
        string builder =
            File.ReadAllText(
                Path.Combine(
                    Environment.CurrentDirectory,
                    "KMC.Plugin",
                    "Topology",
                    "VesselTopologyBuilder.cs"));

        Check(
            builder.IndexOf(
                "TryGetRendererLocalGeometryBounds",
                StringComparison.Ordinal) >= 0 &&
            builder.IndexOf(
                "AccumulateRendererLocalBounds",
                StringComparison.Ordinal) >= 0,
            "SCHEM native-local geometry helpers missing");

        Check(
            builder.IndexOf(
                "GetComponent(" +
                Environment.NewLine +
                "                        \"MeshFilter\")",
                StringComparison.Ordinal) >= 0 ||
            builder.IndexOf(
                "GetComponent(" +
                "\r\n" +
                "                        \"MeshFilter\")",
                StringComparison.Ordinal) >= 0 ||
            builder.IndexOf(
                "\"MeshFilter\"",
                StringComparison.Ordinal) >= 0,
            "SCHEM MeshFilter local-bounds path missing");

        Check(
            builder.IndexOf(
                "\"localBounds\"",
                StringComparison.Ordinal) >= 0 &&
            builder.IndexOf(
                "\"sharedMesh\"",
                StringComparison.Ordinal) >= 0,
            "SCHEM skinned/mesh local bounds lookup missing");

        Check(
            builder.IndexOf(
                "Bounds bounds =" +
                Environment.NewLine +
                "                        renderer.bounds",
                StringComparison.Ordinal) < 0 &&
            builder.IndexOf(
                "Bounds bounds =" +
                "\r\n" +
                "                        renderer.bounds",
                StringComparison.Ordinal) < 0,
            "world-space renderer.bounds AABB path still present");
    }

    private static void VesselSchematicOutlineTransportRegression()
    {
        VesselTopology topology =
            new VesselTopology
            {
                VesselId = "SCHEM-OUTLINE",
                VesselName = "Outline Test",
                RootPartId = 1U,
                HasRootPart = true,
                PartCount = 1,
                Revision = 11
            };

        VesselTopologyNode node =
            new VesselTopologyNode
            {
                PartId = 1U,
                PartTitle = "Hull"
            };

        node.SideOutlinePoints.Add(
            new VesselOutlinePoint
            {
                Horizontal = -2.0,
                Vertical = -1.0
            });

        node.SideOutlinePoints.Add(
            new VesselOutlinePoint
            {
                Horizontal = 2.0,
                Vertical = -1.0
            });

        node.SideOutlinePoints.Add(
            new VesselOutlinePoint
            {
                Horizontal = 0.0,
                Vertical = 3.0
            });

        topology.Nodes.Add(
            node);

        byte[] encoded =
            VesselTopologyPacketCodec.Encode(
                topology);

        VesselTopology decoded;

        Check(
            VesselTopologyPacketCodec.TryDecode(
                encoded,
                out decoded),
            "SCHEM outline transport decode failed");

        Check(
            decoded != null &&
            decoded.TransportVersion >= 4 &&
            decoded.Nodes.Count == 1 &&
            decoded.Nodes[0].SideOutlinePoints.Count == 3,
            "SCHEM outline collection lost");

        Near(
            decoded.Nodes[0]
                .SideOutlinePoints[0]
                .Horizontal,
            -2.0);

        Near(
            decoded.Nodes[0]
                .SideOutlinePoints[2]
                .Vertical,
            3.0);
    }

    private static void VesselSchematicSingleViewRegression()
    {
        string builder =
            File.ReadAllText(
                Path.Combine(
                    Environment.CurrentDirectory,
                    "KMC.Plugin",
                    "Topology",
                    "VesselTopologyBuilder.cs"));

        string page =
            File.ReadAllText(
                Path.Combine(
                    Environment.CurrentDirectory,
                    "KMC.MissionControl",
                    "Pages",
                    "SchematicPage.cs"));

        Check(
            builder.IndexOf(
                "BuildConvexHull",
                StringComparison.Ordinal) >= 0 &&
            builder.IndexOf(
                "SideOutlinePoints.Add",
                StringComparison.Ordinal) >= 0 &&
            builder.IndexOf(
                "vertices",
                StringComparison.Ordinal) >= 0,
            "SCHEM mesh-outline generation missing");

        Check(
            page.IndexOf(
                "\"ACTIVE VESSEL / SIDE\"",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "\"SINGLE SIDE VIEW\"",
                StringComparison.Ordinal) >= 0,
            "SCHEM single side view missing");

        Check(
            page.IndexOf(
                "DrawPolygon",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "FillPolygon",
                StringComparison.Ordinal) >= 0,
            "SCHEM clean polygon renderer missing");

        Check(
            page.IndexOf(
                "PointerDown",
                StringComparison.Ordinal) < 0 &&
            page.IndexOf(
                "\"FRONT\"",
                StringComparison.Ordinal) < 0 &&
            page.IndexOf(
                "\"TOP\"",
                StringComparison.Ordinal) < 0,
            "SCHEM legacy multi-view controls remain");
    }

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
                "bounds.Width < 11.0f",
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
                "bool primaryHull",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "? 38",
                StringComparison.Ordinal) >= 0,
            "SCHEM primary hull fill emphasis missing");

        Check(
            page.IndexOf(
                "bool primaryHull",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "? 245",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "? 2.0f",
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

    private static void ManeuverUplinkDesiredPeriapsis()
    {
        ManeuverUplinkPacket packet = new ManeuverUplinkPacket
        {
            VesselId = "VESSEL",
            PlanId = "PLAN",
            NodeUniversalTimeSeconds = 1234.5,
            ProgradeDeltaVMetersPerSecond = 100.0,
            NormalDeltaVMetersPerSecond = -2.0,
            RadialDeltaVMetersPerSecond = 3.0,
            TargetBodyName = "Duna",
            Operation = "CREATE",
            DesiredPeriapsisRadiusMeters = 640000.0
        };

        ManeuverUplinkPacket parsed;
        Check(ManeuverUplinkPacket.TryParse(packet.Serialize(), out parsed),
            "extended maneuver packet did not parse");
        Near(parsed.DesiredPeriapsisRadiusMeters, 640000.0, 1e-9);

        string legacy = string.Join("|", new[]
        {
            ManeuverUplinkPacket.ProtocolId,
            Uri.EscapeDataString("VESSEL"),
            Uri.EscapeDataString("PLAN"),
            "1234.5", "100", "-2", "3",
            Uri.EscapeDataString("Duna"),
            "CREATE"
        });
        Check(ManeuverUplinkPacket.TryParse(legacy, out parsed),
            "legacy 9-field maneuver packet no longer parses");
        Check(double.IsNaN(parsed.DesiredPeriapsisRadiusMeters),
            "legacy packet should not invent a desired periapsis");
    }

    private static void CoupledProductionApiConsolidation()
    {
        MethodInfo productionSolve =
            typeof(CoupledFiniteSoiOptimizer).GetMethod(
                "TrySolve",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new Type[]
                {
                    typeof(TransferSearchSolution),
                    typeof(LambertParkingOrbitEjectionSolution),
                    typeof(OrbitalElements),
                    typeof(CelestialBodyState),
                    typeof(CelestialBodyState),
                    typeof(double),
                    typeof(CoupledFiniteSoiResult).MakeByRefType(),
                    typeof(string).MakeByRefType()
                },
                null);

        MethodInfo compatibilitySolve =
            typeof(CoupledFiniteSoiOptimizer).GetMethod(
                "TrySolveShadow",
                BindingFlags.Static | BindingFlags.Public);

        Check(productionSolve != null, "coupled production TrySolve API missing");
        Check(compatibilitySolve != null, "shadow compatibility API missing");
        Check(
            typeof(CoupledFiniteSoiShadowResult).BaseType ==
                typeof(CoupledFiniteSoiResult),
            "shadow result is not a compatibility subtype of production result");

        FieldInfo productionField =
            typeof(MapPage).GetField(
                "_coupledFiniteSoiResult",
                BindingFlags.Instance | BindingFlags.NonPublic);

        FieldInfo staleShadowField =
            typeof(MapPage).GetField(
                "_coupledFiniteSoiShadow",
                BindingFlags.Instance | BindingFlags.NonPublic);

        Check(productionField != null, "MAP production coupled-result field missing");
        Check(staleShadowField == null, "MAP still uses the stale shadow-result field");
    }


    private static void ProductionCleanup()
    {
        Type mapPage = typeof(MapPage);

        Check(
            mapPage.GetMethod(
                "UploadLambertTestNode",
                BindingFlags.Instance | BindingFlags.NonPublic) == null,
            "temporary Lambert test upload method still present");

        Check(
            mapPage.GetMethod(
                "BuildLambertTestNodePacket",
                BindingFlags.Static | BindingFlags.NonPublic) == null,
            "temporary Lambert test packet builder still present");

        Check(
            mapPage.GetMethod(
                "BuildProductionNodePacket",
                BindingFlags.Static | BindingFlags.NonPublic) != null,
            "production packet builder missing");
    }

}
