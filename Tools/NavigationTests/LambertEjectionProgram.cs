using System;
using KMC.Engine.CelestialMechanics;
using KMC.Engine.Navigation;

internal static class LambertEjectionProgram
{
    private static int passed;
    private static int failed;

    private static void Main()
    {
        Run(
            "Lambert ejection reproduces planar prograde hyperbolic burn",
            PlanarPrograde);

        Run(
            "Lambert ejection exposes out-of-plane normal component",
            OutOfPlane);

        Run(
            "Lambert ejection preserves inclined parking-plane geometry",
            InclinedPlane);

        Run(
            "Lambert ejection supports a normal-only 3D asymptote",
            NormalOnlyAsymptote);

        Run(
            "Lambert ejection rejects impossible and invalid geometry",
            InvalidGeometry);

        Console.WriteLine(
            "Lambert ejection: {0} passed, {1} failed",
            passed,
            failed);

        Environment.ExitCode =
            failed == 0 ? 0 : 1;
    }

    private static void Run(
        string name,
        Action test)
    {
        try
        {
            test();
            passed++;
            Console.WriteLine("PASS " + name);
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine(
                "FAIL " + name + ": " + ex.Message);
        }
    }

    private static void Check(
        bool value,
        string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void Near(
        double actual,
        double expected,
        double tolerance = 2e-8)
    {
        Check(
            !double.IsNaN(actual) &&
            !double.IsInfinity(actual) &&
            Math.Abs(actual - expected) <= tolerance,
            "expected " + expected.ToString("R") +
            ", got " + actual.ToString("R"));
    }

    private static OrbitalElements CircularOrbit(
        double inclinationDegrees = 0.0,
        double nodeDegrees = 0.0)
    {
        return
            new OrbitalElements
            {
                ReferenceBodyName = "Origin",
                SemiMajorAxisMeters = 1.0,
                Eccentricity = 0.0,
                InclinationDegrees = inclinationDegrees,
                LongitudeOfAscendingNodeDegrees = nodeDegrees,
                ArgumentOfPeriapsisDegrees = 0.0,
                MeanAnomalyAtEpochRadians = 0.0,
                EpochUniversalTimeSeconds = 0.0
            };
    }

    private static CelestialBodyState Origin()
    {
        return
            new CelestialBodyState
            {
                Name = "Origin",
                ParentName = "Primary",
                RadiusMeters = 0.5,
                GravParameter = 1.0
            };
    }

    private static LambertParkingOrbitEjectionSolution Solve(
        OrbitalElements parkingOrbit,
        Vector3d vinf,
        double departureUt = 0.0)
    {
        LambertParkingOrbitEjectionSolution solution;

        Check(
            LambertParkingOrbitEjectionPlanner.TryCalculate(
                parkingOrbit,
                0.5,
                Origin(),
                vinf,
                departureUt,
                out solution),
            "valid ejection geometry rejected");

        Check(solution != null, "solution is null");
        return solution;
    }

    private static void PlanarPrograde()
    {
        LambertParkingOrbitEjectionSolution solution =
            Solve(
                CircularOrbit(),
                new Vector3d(0.0, 1.0, 0.0));

        double expectedDv =
            Math.Sqrt(3.0) - 1.0;

        Near(
            solution.HyperbolicExcessSpeedMetersPerSecond,
            1.0);

        Near(
            solution.HyperbolicEccentricity,
            2.0);

        Near(
            solution.AsymptoteAngleDegrees,
            120.0,
            1e-8);

        Near(
            solution.ProgradeDeltaVMetersPerSecond,
            expectedDv,
            2e-8);

        Near(
            solution.NormalDeltaVMetersPerSecond,
            0.0,
            2e-8);

        Near(
            solution.RadialDeltaVMetersPerSecond,
            0.0,
            2e-8);

        Near(
            solution.TotalDeltaVMetersPerSecond,
            expectedDv,
            2e-8);

        Near(
            solution.GeometryResidualDegrees,
            0.0,
            2e-8);
    }

    private static void OutOfPlane()
    {
        double y = 0.9;
        double z =
            Math.Sqrt(
                1.0 - y * y);

        LambertParkingOrbitEjectionSolution solution =
            Solve(
                CircularOrbit(),
                new Vector3d(0.0, y, z));

        Check(
            Math.Abs(
                solution.NormalDeltaVMetersPerSecond) >
                0.1,
            "out-of-plane departure produced no normal DV");

        double componentMagnitude =
            Math.Sqrt(
                solution.ProgradeDeltaVMetersPerSecond *
                solution.ProgradeDeltaVMetersPerSecond +
                solution.NormalDeltaVMetersPerSecond *
                solution.NormalDeltaVMetersPerSecond +
                solution.RadialDeltaVMetersPerSecond *
                solution.RadialDeltaVMetersPerSecond);

        Near(
            componentMagnitude,
            solution.TotalDeltaVMetersPerSecond,
            2e-8);

        /*
         * A true non-coplanar single-impulse solution may use radial
         * authority as part of the minimum-DV hyperbolic departure. Do not
         * impose the old projected-plane radial=0 assumption here.
         */
        Check(
            !double.IsNaN(
                solution.RadialDeltaVMetersPerSecond) &&
            !double.IsInfinity(
                solution.RadialDeltaVMetersPerSecond),
            "out-of-plane radial DV is nonfinite");

        Check(
            solution.GeometryResidualDegrees <
                1e-3,
            "3D asymptote geometry did not close");
    }

    private static void InclinedPlane()
    {
        double inclination =
            47.0 * Math.PI / 180.0;

        // +Q direction for a circular orbit inclined about +X.
        Vector3d inPlaneVinf =
            new Vector3d(
                0.0,
                Math.Cos(inclination),
                Math.Sin(inclination));

        LambertParkingOrbitEjectionSolution solution =
            Solve(
                CircularOrbit(47.0, 0.0),
                inPlaneVinf);

        Near(
            solution.NormalDeltaVMetersPerSecond,
            0.0,
            2e-8);

        Near(
            solution.RadialDeltaVMetersPerSecond,
            0.0,
            2e-8);

        Check(
            solution.ProgradeDeltaVMetersPerSecond >
                0.0,
            "inclined in-plane ejection was not prograde");
    }


    private static void NormalOnlyAsymptote()
    {
        LambertParkingOrbitEjectionSolution solution =
            Solve(
                CircularOrbit(),
                new Vector3d(0.0, 0.0, 1.0));

        Check(
            solution.TotalDeltaVMetersPerSecond > 0.0,
            "normal-only ejection has no impulse");

        Check(
            Math.Abs(
                solution.NormalDeltaVMetersPerSecond) >
                0.1,
            "normal-only asymptote produced no normal authority");

        double componentMagnitude =
            Math.Sqrt(
                solution.ProgradeDeltaVMetersPerSecond *
                solution.ProgradeDeltaVMetersPerSecond +
                solution.NormalDeltaVMetersPerSecond *
                solution.NormalDeltaVMetersPerSecond +
                solution.RadialDeltaVMetersPerSecond *
                solution.RadialDeltaVMetersPerSecond);

        Near(
            componentMagnitude,
            solution.TotalDeltaVMetersPerSecond,
            5e-7);

        Near(
            solution.HyperbolicExcessSpeedMetersPerSecond,
            1.0,
            5e-7);

        Check(
            solution.GeometryResidualDegrees < 1e-3,
            "normal-only 3D hyperbola does not close");
    }

    private static void InvalidGeometry()
    {
        LambertParkingOrbitEjectionSolution solution;

        OrbitalElements eccentric =
            CircularOrbit();
        eccentric.Eccentricity = 0.051;

        Check(
            !LambertParkingOrbitEjectionPlanner.TryCalculate(
                eccentric,
                0.5,
                Origin(),
                new Vector3d(0.0, 1.0, 0.0),
                0.0,
                out solution),
            "parking eccentricity beyond preview domain accepted");

        OrbitalElements wrongFrame =
            CircularOrbit();
        wrongFrame.ReferenceBodyName =
            "Another body";

        Check(
            !LambertParkingOrbitEjectionPlanner.TryCalculate(
                wrongFrame,
                0.5,
                Origin(),
                new Vector3d(0.0, 1.0, 0.0),
                0.0,
                out solution),
            "conflicting parking frame accepted");

        Check(
            !LambertParkingOrbitEjectionPlanner.TryCalculate(
                CircularOrbit(),
                0.5,
                Origin(),
                new Vector3d(),
                0.0,
                out solution),
            "zero excess velocity accepted");
    }
}
