using System;
using KMC.Engine.CelestialMechanics;
using KMC.Engine.Navigation;

internal static class TransferSearchProgram
{
    private static int passed;
    private static int failed;

    private static void Main()
    {
        Run(
            "Lambert search finds zero-excess circular rendezvous",
            CircularRendezvous);

        Run(
            "Lambert search preserves 3D inclined state geometry",
            InclinedRendezvous);

        Run(
            "Lambert search rejects invalid domains and frames",
            InvalidRequests);

        Console.WriteLine(
            "Transfer search: {0} passed, {1} failed",
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
        if (double.IsNaN(actual) ||
            double.IsInfinity(actual) ||
            Math.Abs(actual - expected) > tolerance)
        {
            throw new Exception(
                "expected " +
                expected.ToString("R") +
                ", got " +
                actual.ToString("R"));
        }
    }

    private static CelestialBodyState Body(
        string name,
        double inclinationDegrees = 0.0,
        double nodeDegrees = 0.0)
    {
        return
            new CelestialBodyState
            {
                Name = name,
                ParentName = "Synthetic primary",
                Orbit =
                    new OrbitalElements
                    {
                        ReferenceBodyName =
                            "Synthetic primary",
                        SemiMajorAxisMeters = 1.0,
                        Eccentricity = 0.0,
                        InclinationDegrees =
                            inclinationDegrees,
                        LongitudeOfAscendingNodeDegrees =
                            nodeDegrees,
                        ArgumentOfPeriapsisDegrees = 0.0,
                        MeanAnomalyAtEpochRadians = 0.0,
                        EpochUniversalTimeSeconds = 0.0
                    }
            };
    }

    private static TransferSearchRequest Request(
        CelestialBodyState origin,
        CelestialBodyState destination)
    {
        return
            new TransferSearchRequest
            {
                OriginBody = origin,
                DestinationBody = destination,
                ParentGravParameter = 1.0,
                EarliestDepartureUniversalTimeSeconds = 0.0,
                LatestDepartureUniversalTimeSeconds = Math.PI,
                MinimumTimeOfFlightSeconds = Math.PI / 2.0,
                MaximumTimeOfFlightSeconds = Math.PI / 2.0,
                DepartureSamples = 3,
                TimeOfFlightSamples = 1,
                SearchShortWay = true,
                SearchLongWay = true
            };
    }

    private static void CircularRendezvous()
    {
        TransferSearchSolution solution;
        TransferSearchRequest request =
            Request(
                Body("Origin"),
                Body("Destination"));

        Check(
            LambertTransferSearch.TryFindBest(
                request,
                out solution),
            "valid search returned no solution");

        Check(
            solution != null,
            "solution is null");

        Check(
            solution.Path ==
                LambertTransferPath.ShortWay,
            "zero-cost short-way solution was not selected");

        // All three departure samples are equivalent for these
        // co-orbital synthetic bodies. Deterministic tie-breaking
        // must keep the earliest departure.
        Near(
            solution.DepartureUniversalTimeSeconds,
            0.0);

        Near(
            solution.TimeOfFlightSeconds,
            Math.PI / 2.0);

        Near(
            solution.ArrivalUniversalTimeSeconds,
            Math.PI / 2.0);

        Near(
            solution.DepartureExcessSpeedMetersPerSecond,
            0.0);

        Near(
            solution.ArrivalExcessSpeedMetersPerSecond,
            0.0);

        Near(
            solution.CombinedExcessSpeedMetersPerSecond,
            0.0);

        Check(
            solution.OriginName == "Origin" &&
            solution.DestinationName == "Destination" &&
            solution.ParentName == "Synthetic primary",
            "body/frame identity lost");
    }

    private static void InclinedRendezvous()
    {
        TransferSearchSolution solution;

        TransferSearchRequest request =
            Request(
                Body("Origin", 47.0, 123.0),
                Body("Destination", 47.0, 123.0));

        request.EarliestDepartureUniversalTimeSeconds =
            0.0;
        request.LatestDepartureUniversalTimeSeconds =
            0.0;
        request.DepartureSamples = 1;

        Check(
            LambertTransferSearch.TryFindBest(
                request,
                out solution),
            "inclined search returned no solution");

        Near(
            solution.DepartureExcessSpeedMetersPerSecond,
            0.0);

        Near(
            solution.ArrivalExcessSpeedMetersPerSecond,
            0.0);

        // The propagated endpoint must actually be out of the XY
        // plane, proving the search is using the 3D state path.
        Check(
            Math.Abs(
                solution.DestinationArrivalState.Position.Z) >
                0.1,
            "inclined destination collapsed into planar geometry");
    }

    private static void InvalidRequests()
    {
        TransferSearchSolution solution;
        CelestialBodyState origin = Body("Origin");
        CelestialBodyState destination =
            Body("Destination");

        TransferSearchRequest request =
            Request(origin, destination);

        request.ParentGravParameter =
            double.NaN;

        Check(
            !LambertTransferSearch.TryFindBest(
                request,
                out solution) &&
            solution == null,
            "nonfinite parent mu accepted");

        request =
            Request(origin, destination);
        request.SearchShortWay = false;
        request.SearchLongWay = false;

        Check(
            !LambertTransferSearch.TryFindBest(
                request,
                out solution),
            "search with no transfer paths accepted");

        request =
            Request(origin, destination);
        request.DepartureSamples = 0;

        Check(
            !LambertTransferSearch.TryFindBest(
                request,
                out solution),
            "zero departure samples accepted");

        request =
            Request(origin, destination);
        request.MinimumTimeOfFlightSeconds = 2.0;
        request.MaximumTimeOfFlightSeconds = 1.0;

        Check(
            !LambertTransferSearch.TryFindBest(
                request,
                out solution),
            "reversed flight-time range accepted");

        destination.ParentName = "Other primary";
        request =
            Request(origin, destination);

        Check(
            !LambertTransferSearch.TryFindBest(
                request,
                out solution),
            "hierarchy change accepted by same-parent search");

        destination.ParentName = "Synthetic primary";
        destination.Orbit.ReferenceBodyName =
            "Wrong frame";
        request =
            Request(origin, destination);

        Check(
            !LambertTransferSearch.TryFindBest(
                request,
                out solution),
            "conflicting destination frame accepted");
    }
}
