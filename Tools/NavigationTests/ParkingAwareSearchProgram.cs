using System;
using KMC.Engine.CelestialMechanics;
using KMC.Engine.Navigation;

internal static class ParkingAwareSearchProgram
{
    private static int passed, failed;

    private static void Main()
    {
        Run("parking-aware search returns constrained minimum-DV bootstrap", Basic);
        Run("parking-aware search preserves bootstrap when coarse constraint is unmet", BootstrapAvailable);
        Run("parking-aware search rejects invalid parking frame", InvalidFrame);
        Run("parking-aware search rejects impossible parking geometry", ImpossibleGeometry);
        Console.WriteLine("Parking-aware search: {0} passed, {1} failed", passed, failed);
        Environment.ExitCode = failed == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    private static CelestialBodyState Body(string name, double anomaly)
    {
        return new CelestialBodyState
        {
            Name = name,
            ParentName = "Primary",
            GravParameter = name == "Origin" ? 1.0 : 0.0,
            SoiRadiusMeters = name == "Origin" ? 4.0 : 0.0,
            Orbit = new OrbitalElements
            {
                ReferenceBodyName = "Primary",
                SemiMajorAxisMeters = name == "Origin" ? 10.0 : 16.0,
                Eccentricity = 0.0,
                InclinationDegrees = 0.0,
                LongitudeOfAscendingNodeDegrees = 0.0,
                ArgumentOfPeriapsisDegrees = 0.0,
                MeanAnomalyAtEpochRadians = anomaly,
                EpochUniversalTimeSeconds = 0.0
            }
        };
    }

    private static TransferSearchRequest Request()
    {
        return new TransferSearchRequest
        {
            OriginBody = Body("Origin", 0.0),
            DestinationBody = Body("Destination", 1.2),
            ParentGravParameter = 1.0,
            EarliestDepartureUniversalTimeSeconds = 0.0,
            LatestDepartureUniversalTimeSeconds = 20.0,
            MinimumTimeOfFlightSeconds = 5.0,
            MaximumTimeOfFlightSeconds = 25.0,
            DepartureSamples = 7,
            TimeOfFlightSamples = 7,
            SearchShortWay = true,
            SearchLongWay = true
        };
    }

    private static OrbitalElements Parking()
    {
        return new OrbitalElements
        {
            ReferenceBodyName = "Origin",
            SemiMajorAxisMeters = 1.0,
            Eccentricity = 0.0,
            InclinationDegrees = 0.0,
            LongitudeOfAscendingNodeDegrees = 0.0,
            ArgumentOfPeriapsisDegrees = 0.0,
            MeanAnomalyAtEpochRadians = 0.0,
            EpochUniversalTimeSeconds = 0.0
        };
    }

    private static void Basic()
    {
        ParkingOrbitAwareTransferSolution solution;
        Check(
            ParkingOrbitAwareLambertSearch.TryFindBest(
                Request(), Parking(), 0.5, out solution),
            "valid search returned no solution");

        Check(solution != null, "solution is null");
        Check(solution.Transfer != null, "transfer is null");
        Check(solution.Ejection != null, "ejection is null");
        Check(solution.EjectionScoreMetersPerSecond > 0.0, "score is invalid");

        double components =
            Math.Sqrt(
                solution.Ejection.ProgradeDeltaVMetersPerSecond *
                solution.Ejection.ProgradeDeltaVMetersPerSecond +
                solution.Ejection.NormalDeltaVMetersPerSecond *
                solution.Ejection.NormalDeltaVMetersPerSecond +
                solution.Ejection.RadialDeltaVMetersPerSecond *
                solution.Ejection.RadialDeltaVMetersPerSecond);

        Check(
            Math.Abs(components - solution.EjectionScoreMetersPerSecond) < 1e-8,
            "components do not reproduce score");
    }

    private static void BootstrapAvailable()
    {
        ParkingOrbitAwareTransferSolution solution;

        Check(
            ParkingOrbitAwareLambertSearch.TryFindBest(
                Request(),
                Parking(),
                0.5,
                out solution),
            "valid coarse search lost its optimization bootstrap");

        Check(
            solution != null &&
            solution.Ejection != null &&
            solution.FiniteSoiAssessment != null,
            "bootstrap is incomplete");

        Check(
            solution.EjectionScoreMetersPerSecond > 0.0 &&
            !double.IsNaN(solution.EjectionScoreMetersPerSecond) &&
            !double.IsInfinity(solution.EjectionScoreMetersPerSecond),
            "bootstrap departure DV is invalid");
    }

    private static void InvalidFrame()
    {
        OrbitalElements parking = Parking();
        parking.ReferenceBodyName = "Wrong body";
        ParkingOrbitAwareTransferSolution solution;
        Check(
            !ParkingOrbitAwareLambertSearch.TryFindBest(
                Request(), parking, 0.5, out solution) &&
            solution == null,
            "conflicting parking frame accepted");
    }

    private static void ImpossibleGeometry()
    {
        OrbitalElements parking = Parking();
        parking.SemiMajorAxisMeters = 0.4;
        ParkingOrbitAwareTransferSolution solution;
        Check(
            !ParkingOrbitAwareLambertSearch.TryFindBest(
                Request(), parking, 0.5, out solution) &&
            solution == null,
            "parking orbit inside body accepted");
    }
}
