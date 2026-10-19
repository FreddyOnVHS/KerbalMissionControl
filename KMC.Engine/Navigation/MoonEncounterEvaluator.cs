using System;
using KMC.Engine.CelestialMechanics;

namespace KMC.Engine.Navigation
{
    /// <summary>
    /// 14.24.2: patched-conic, finite-SOI encounter assessment for a same-primary
    /// moon transfer. KMC's initial Lambert seed is NOT proof of a safe flyby.
    /// Outside the moon SOI the craft follows its post-impulse primary-centered
    /// two-body conic. At inbound SOI entry the osculating moon-centered conic
    /// supplies periapsis and surface-impact diagnostics. No n-body integration,
    /// thrust arc, node uplink, or automatic correction is performed here.
    /// </summary>
    public sealed class MoonEncounterAssessment
    {
        public bool EntersSoi { get; internal set; }
        public double EntryUniversalTimeSeconds { get; internal set; }
        public double EntryDistanceMeters { get; internal set; }
        public double PredictedPeriapsisRadiusMeters { get; internal set; }
        public double PredictedPeriapsisAltitudeMeters { get; internal set; }
        public double RequestedPeriapsisRadiusMeters { get; internal set; }
        public double PeriapsisErrorMeters { get; internal set; }
        public bool SurfaceImpact { get; internal set; }
        public bool RequestedPeriapsisSatisfied { get; internal set; }
        public Vector3d RelativeEntryPosition { get; internal set; }
        public Vector3d RelativeEntryVelocity { get; internal set; }
    }

    public static class MoonEncounterEvaluator
    {
        // A sample brackets entry; bisection locates it to within a small fraction
        // of the sample interval. Samples do NOT provide a global no-encounter proof
        // for arbitrarily short crossings, so callers must not treat false as such.
        private const int Samples = 320;
        private const int BisectIterations = 52;

        public static bool TryAssess(OrbitalElements activeOrbit,
            CelestialBodyState moon, double primaryMu,
            MoonTransferCandidate candidate, double requestedPeriapsisRadiusMeters,
            out MoonEncounterAssessment assessment)
        {
            assessment = null;
            if (activeOrbit == null || moon == null || moon.Orbit == null ||
                candidate == null || !candidate.DeltaV.IsFinite ||
                MoonTransferRoute.Classify(activeOrbit.ReferenceBodyName,
                    moon.Name, moon.ParentName) != TransferRouteKind.SamePrimaryMoon ||
                !MatchesFrame(moon.Orbit.ReferenceBodyName, moon.ParentName) ||
                !Positive(primaryMu) || !Positive(moon.GravParameter) ||
                !Positive(moon.RadiusMeters) || !Positive(moon.SoiRadiusMeters) ||
                moon.SoiRadiusMeters <= moon.RadiusMeters ||
                !Positive(requestedPeriapsisRadiusMeters) ||
                requestedPeriapsisRadiusMeters <= moon.RadiusMeters ||
                requestedPeriapsisRadiusMeters >= moon.SoiRadiusMeters ||
                !Finite(candidate.DepartureUniversalTimeSeconds) ||
                !Finite(candidate.ArrivalUniversalTimeSeconds) ||
                candidate.ArrivalUniversalTimeSeconds <= candidate.DepartureUniversalTimeSeconds)
                return false;

            StateVector departure;
            if (!KeplerPropagator.TryPropagate(activeOrbit, primaryMu,
                    candidate.DepartureUniversalTimeSeconds, out departure)) return false;
            StateVector injected = new StateVector(departure.Position,
                departure.Velocity + candidate.DeltaV,
                candidate.DepartureUniversalTimeSeconds, primaryMu, moon.ParentName);
            double start = candidate.DepartureUniversalTimeSeconds;
            double end = candidate.ArrivalUniversalTimeSeconds;
            double previousUt = start;
            double previousDistance;
            if (!TryRelative(injected, moon, primaryMu, previousUt,
                    out previousDistance, out _, out _)) return false;
            if (previousDistance <= moon.SoiRadiusMeters) return false;

            for (int i = 1; i <= Samples; i++)
            {
                double ut = start + (end - start) * i / Samples;
                double distance;
                Vector3d relativePosition, relativeVelocity;
                if (!TryRelative(injected, moon, primaryMu, ut,
                        out distance, out relativePosition, out relativeVelocity)) return false;
                if (distance <= moon.SoiRadiusMeters && previousDistance > moon.SoiRadiusMeters)
                {
                    double low = previousUt;
                    double high = ut;
                    for (int j = 0; j < BisectIterations; j++)
                    {
                        double middle = low + (high - low) * 0.5;
                        double middleDistance;
                        if (!TryRelative(injected, moon, primaryMu, middle,
                                out middleDistance, out _, out _)) return false;
                        if (middleDistance > moon.SoiRadiusMeters) low = middle;
                        else high = middle;
                    }
                    double entryUt = high;
                    if (!TryRelative(injected, moon, primaryMu, entryUt,
                            out distance, out relativePosition, out relativeVelocity)) return false;
                    if (Vector3d.Dot(relativePosition, relativeVelocity) >= 0) return false;
                    double h2 = Vector3d.Dot(Vector3d.Cross(relativePosition, relativeVelocity),
                                              Vector3d.Cross(relativePosition, relativeVelocity));
                    double energy = Vector3d.Dot(relativeVelocity, relativeVelocity) * 0.5 -
                                    moon.GravParameter / distance;
                    double discriminant = 1 + 2 * energy * h2 /
                                          (moon.GravParameter * moon.GravParameter);
                    if (!Finite(discriminant) || discriminant < -1e-9) return false;
                    double e = Math.Sqrt(Math.Max(0, discriminant));
                    double periapsis = h2 / (moon.GravParameter * (1 + e));
                    if (!Finite(periapsis)) return false;
                    assessment = new MoonEncounterAssessment
                    {
                        EntersSoi = true,
                        EntryUniversalTimeSeconds = entryUt,
                        EntryDistanceMeters = distance,
                        RelativeEntryPosition = relativePosition,
                        RelativeEntryVelocity = relativeVelocity,
                        PredictedPeriapsisRadiusMeters = periapsis,
                        PredictedPeriapsisAltitudeMeters = periapsis - moon.RadiusMeters,
                        RequestedPeriapsisRadiusMeters = requestedPeriapsisRadiusMeters,
                        PeriapsisErrorMeters = periapsis - requestedPeriapsisRadiusMeters,
                        SurfaceImpact = periapsis <= moon.RadiusMeters,
                        RequestedPeriapsisSatisfied = periapsis > moon.RadiusMeters &&
                            Math.Abs(periapsis - requestedPeriapsisRadiusMeters) <= 1000.0
                    };
                    return true;
                }
                previousUt = ut;
                previousDistance = distance;
            }
            return false;
        }

        private static bool TryRelative(StateVector injected, CelestialBodyState moon,
            double primaryMu, double ut, out double distance,
            out Vector3d relativePosition, out Vector3d relativeVelocity)
        {
            distance = double.NaN;
            relativePosition = new Vector3d();
            relativeVelocity = new Vector3d();
            StateVector craft, body;
            if (!StateVectorPropagator.TryPropagate(injected, ut, out craft) ||
                !KeplerPropagator.TryPropagate(moon.Orbit, primaryMu, ut, out body)) return false;
            relativePosition = craft.Position - body.Position;
            relativeVelocity = craft.Velocity - body.Velocity;
            distance = relativePosition.Magnitude;
            return Positive(distance) && relativeVelocity.IsFinite;
        }

        private static bool MatchesFrame(string actual, string expected)
        {
            return string.IsNullOrWhiteSpace(actual) ||
                string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }
        private static bool Finite(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }
        private static bool Positive(double x) { return Finite(x) && x > 0; }
    }
}
