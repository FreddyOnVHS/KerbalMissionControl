using System;
using KMC.Engine.CelestialMechanics;

namespace KMC.Engine.Navigation
{
    // 14.24.1: same-primary, zero-revolution Lambert injection bootstrap.
    // It is NOT a verified SOI entry, safe periapsis or node uplink.
    public enum TransferRouteKind { Unsupported = 0, SamePrimaryMoon = 1 }

    public static class MoonTransferRoute
    {
        public static TransferRouteKind Classify(string activeReferenceBody,
            string destinationName, string destinationParentName)
        {
            if (string.IsNullOrWhiteSpace(activeReferenceBody) ||
                string.IsNullOrWhiteSpace(destinationName) ||
                string.IsNullOrWhiteSpace(destinationParentName) ||
                string.Equals(activeReferenceBody, destinationName, StringComparison.OrdinalIgnoreCase))
                return TransferRouteKind.Unsupported;
            return string.Equals(activeReferenceBody, destinationParentName,
                StringComparison.OrdinalIgnoreCase)
                ? TransferRouteKind.SamePrimaryMoon : TransferRouteKind.Unsupported;
        }
    }

    public sealed class MoonTransferCandidate
    {
        public double DepartureUniversalTimeSeconds { get; internal set; }
        public double ArrivalUniversalTimeSeconds { get; internal set; }
        public Vector3d DeltaV { get; internal set; }
        public double DeltaVMetersPerSecond { get { return DeltaV.Magnitude; } }
        public Vector3d ArrivalRelativeVelocity { get; internal set; }
        public LambertTransferPath Path { get; internal set; }
        public bool EncounterVerified { get { return false; } }
    }

    public static class MoonTransferBootstrap
    {
        // No arbitrary stock constants. Caller supplies a physically plausible
        // bounded domain and central-body mu from the current telemetry.
        public static bool TryFindInjection(OrbitalElements activeOrbit,
            CelestialBodyState targetMoon, double primaryMu, double earliestDepartureUt,
            double latestDepartureUt, double minimumFlightSeconds,
            double maximumFlightSeconds, int departureSamples, int flightSamples,
            out MoonTransferCandidate best)
        {
            best = null;
            if (activeOrbit == null || targetMoon == null || targetMoon.Orbit == null ||
                MoonTransferRoute.Classify(activeOrbit.ReferenceBodyName, targetMoon.Name,
                    targetMoon.ParentName) != TransferRouteKind.SamePrimaryMoon ||
                !string.IsNullOrWhiteSpace(targetMoon.Orbit.ReferenceBodyName) &&
                !string.Equals(targetMoon.Orbit.ReferenceBodyName, targetMoon.ParentName,
                    StringComparison.OrdinalIgnoreCase) ||
                !Vector3d.Finite(primaryMu) || primaryMu <= 0 ||
                !Vector3d.Finite(earliestDepartureUt) || !Vector3d.Finite(latestDepartureUt) ||
                latestDepartureUt < earliestDepartureUt ||
                !Vector3d.Finite(minimumFlightSeconds) || minimumFlightSeconds <= 0 ||
                !Vector3d.Finite(maximumFlightSeconds) || maximumFlightSeconds < minimumFlightSeconds ||
                departureSamples < 1 || departureSamples > 128 ||
                flightSamples < 1 || flightSamples > 128) return false;

            for (int d = 0; d < departureSamples; d++)
            {
                double depUt = Sample(earliestDepartureUt, latestDepartureUt, departureSamples, d);
                StateVector craft;
                if (!KeplerPropagator.TryPropagate(activeOrbit, primaryMu, depUt, out craft)) continue;
                for (int f = 0; f < flightSamples; f++)
                {
                    double flight = Sample(minimumFlightSeconds, maximumFlightSeconds, flightSamples, f);
                    double arrUt = depUt + flight;
                    StateVector moon;
                    if (!Vector3d.Finite(arrUt) ||
                        !KeplerPropagator.TryPropagate(targetMoon.Orbit, primaryMu, arrUt, out moon)) continue;
                    for (int arc = 0; arc < 2; arc++)
                    {
                        LambertTransferPath path = arc == 0 ? LambertTransferPath.ShortWay : LambertTransferPath.LongWay;
                        LambertSolution transfer;
                        if (!LambertSolver.TrySolve(craft.Position, moon.Position,
                            flight, primaryMu, path, out transfer)) continue;
                        Vector3d dv = transfer.DepartureVelocity - craft.Velocity;
                        Vector3d arrivalRelative = transfer.ArrivalVelocity - moon.Velocity;
                        if (!dv.IsFinite || !arrivalRelative.IsFinite ||
                            !Vector3d.Finite(dv.Magnitude)) continue;
                        if (best == null || dv.Magnitude < best.DeltaVMetersPerSecond)
                            best = new MoonTransferCandidate {
                                DepartureUniversalTimeSeconds = depUt,
                                ArrivalUniversalTimeSeconds = arrUt,
                                DeltaV = dv, ArrivalRelativeVelocity = arrivalRelative, Path = path
                            };
                    }
                }
            }
            return best != null;
        }

        private static double Sample(double a, double b, int n, int i)
        {
            return n == 1 ? a : a + (b - a) * i / (n - 1.0);
        }
    }
}
