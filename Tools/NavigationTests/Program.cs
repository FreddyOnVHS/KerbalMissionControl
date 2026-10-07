using System;
using System.Globalization;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using KMC.Engine.CelestialMechanics;
using KMC.Engine.Navigation;
using KMC.MissionControl.Navigation;
using KMC.MissionControl.Rendering.OrbitMap;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Pages;
using KMC.MissionControl.Telemetry;
using KMC.Shared;

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
        Console.WriteLine("{0} passed, {1} failed", passed, failed);
        Environment.ExitCode = failed == 0 ? 0 : 1;
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
        const double sample = 90061.0; // 1d 01h 01m 01s
        const string expected = "1d 01h 01m 01s";

        CheckPrivateTimeFormatter(typeof(MapPage), "FormatTransferInterval", sample, expected);
        CheckPrivateTimeFormatter(typeof(ManeuverPage), "FormatDuration", sample, expected);
        CheckPrivateTimeFormatter(typeof(GuidancePage), "FormatDuration", sample, expected);
        CheckPrivateTimeFormatter(typeof(OrbitPage), "FormatDuration", sample, expected);
        CheckPrivateTimeFormatter(
            typeof(KMC.MissionControl.Rendering.Ascent.FooterRenderer),
            "FormatMissionTime",
            sample,
            expected);
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
