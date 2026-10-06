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
        Run("MAP Lambert test packet preserves full PNR vector", LambertTestPacket);
        Run("finite SOI assessment returns a real boundary state", FiniteSoiBoundaryAssessment);
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

    private static void LambertTestPacket()
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

        ParkingOrbitAwareTransferSolution parkingAware;
        string muSource;
        Check(
            OrbitMapNavigationAdapter.TryCalculateParkingAwareLambertPreview(
                packet,
                origin,
                destination,
                hohmann,
                out parkingAware,
                out muSource),
            "parking-aware Lambert preview unavailable");

        var method =
            typeof(MapPage).GetMethod(
                "BuildLambertTestNodePacket",
                BindingFlags.Static | BindingFlags.NonPublic);

        Check(method != null, "Lambert test packet builder missing");

        ManeuverUplinkPacket uplink =
            (ManeuverUplinkPacket)method.Invoke(
                null,
                new object[]
                {
                    "TEST-VESSEL",
                    destination.Name,
                    parkingAware.Ejection,
                    "MAP-LAMBERT-TEST-UNIT"
                });

        Check(uplink != null, "Lambert test packet is null");
        Check(uplink.VesselId == "TEST-VESSEL", "vessel ID lost");
        Check(uplink.TargetBodyName == destination.Name, "target lost");
        Check(uplink.PlanId == "MAP-LAMBERT-TEST-UNIT", "plan ID lost");
        Check(uplink.Operation == "CREATE", "operation is not CREATE");

        Near(
            uplink.NodeUniversalTimeSeconds,
            parkingAware.Ejection.BurnUniversalTimeSeconds,
            1e-9);
        Near(
            uplink.ProgradeDeltaVMetersPerSecond,
            parkingAware.Ejection.ProgradeDeltaVMetersPerSecond,
            1e-9);
        Near(
            uplink.NormalDeltaVMetersPerSecond,
            parkingAware.Ejection.NormalDeltaVMetersPerSecond,
            1e-9);
        Near(
            uplink.RadialDeltaVMetersPerSecond,
            parkingAware.Ejection.RadialDeltaVMetersPerSecond,
            1e-9);
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

}
