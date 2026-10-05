using System;
using KMC.Engine.CelestialMechanics;

namespace KMC.Engine.Navigation
{
    /// <summary>Compatibility planner extracted from 14.22.32. Circular/coplanar
    /// Hohmann estimate and near-circular prograde parking ejection; not Lambert.
    /// Planar longitude and Newton iteration intentionally retain baseline behavior.</summary>
    public static class HohmannTransferPlanner
    {
        public static bool TryCalculateTransferWindow(
            CelestialBodyState origin,
            CelestialBodyState destination,
            double currentUniversalTimeSeconds,
            out TransferWindowSolution solution)
        {
            solution = null;
            if (origin == null || destination == null || origin.Orbit == null || destination.Orbit == null) return false;
            if (!origin.Orbit.HasFiniteGeometry || !destination.Orbit.HasFiniteGeometry ||
                !Vector3d.Finite(currentUniversalTimeSeconds) ||
                origin.Orbit.Eccentricity < 0 || destination.Orbit.Eccentricity < 0) return false;
            if (string.IsNullOrWhiteSpace(origin.ParentName) ||
                !string.Equals(origin.ParentName, destination.ParentName, StringComparison.OrdinalIgnoreCase)) return false;
            if (origin.Orbit.Eccentricity >= 1.0 || destination.Orbit.Eccentricity >= 1.0) return false;
            if (!IsFinitePositive(origin.Orbit.SemiMajorAxisMeters) || !IsFinitePositive(destination.Orbit.SemiMajorAxisMeters) ||
                !MatchesOptionalFrame(origin.Orbit.ReferenceBodyName, origin.ParentName) ||
                !MatchesOptionalFrame(destination.Orbit.ReferenceBodyName, destination.ParentName)) return false;

            double r1 = Math.Abs(origin.Orbit.SemiMajorAxisMeters);
            double r2 = Math.Abs(destination.Orbit.SemiMajorAxisMeters);
            if (!IsFinitePositive(r1) || !IsFinitePositive(r2) || Math.Abs(r1 - r2) < 1.0) return false;

            double parentMu = DeriveParentGravParameter(origin.Orbit, r1);
            if (!IsFinitePositive(parentMu))
                parentMu = DeriveParentGravParameter(destination.Orbit, r2);
            if (!IsFinitePositive(parentMu)) return false;

            double transferSemiMajorAxis = 0.5 * (r1 + r2);
            double transferTime = Math.PI * Math.Sqrt(
                (transferSemiMajorAxis * transferSemiMajorAxis * transferSemiMajorAxis) / parentMu);
            double originMeanMotion = Math.Sqrt(parentMu / (r1 * r1 * r1));
            double destinationMeanMotion = Math.Sqrt(parentMu / (r2 * r2 * r2));

            double originLongitude = OrbitalLongitudeAtUniversalTime(origin.Orbit, currentUniversalTimeSeconds, parentMu);
            double destinationLongitude = OrbitalLongitudeAtUniversalTime(destination.Orbit, currentUniversalTimeSeconds, parentMu);
            if (double.IsNaN(originLongitude) || double.IsNaN(destinationLongitude)) return false;

            double currentPhase = NormalizeRadians(destinationLongitude - originLongitude);
            double requiredPhase = NormalizeRadians(Math.PI - (destinationMeanMotion * transferTime));
            double relativeRate = destinationMeanMotion - originMeanMotion;
            if (Math.Abs(relativeRate) < 1e-15) return false;

            double waitSeconds = relativeRate > 0.0
                ? NormalizeRadians(requiredPhase - currentPhase) / relativeRate
                : NormalizeRadians(currentPhase - requiredPhase) / (-relativeRate);

            double circularSpeed = Math.Sqrt(parentMu / r1);
            double transferSpeedAtOrigin = Math.Sqrt(parentMu * ((2.0 / r1) - (1.0 / transferSemiMajorAxis)));

            if (!IsFinitePositive(transferTime) || !Vector3d.Finite(waitSeconds) || waitSeconds < 0 ||
                !Vector3d.Finite(currentUniversalTimeSeconds + waitSeconds) ||
                !Vector3d.Finite(transferSpeedAtOrigin - circularSpeed)) return false;

            solution = new TransferWindowSolution();
            solution.ParentName = origin.ParentName;
            solution.CurrentPhaseDegrees = currentPhase * 180.0 / Math.PI;
            solution.RequiredPhaseDegrees = requiredPhase * 180.0 / Math.PI;
            solution.WaitSeconds = waitSeconds;
            solution.DepartureUniversalTimeSeconds = currentUniversalTimeSeconds + waitSeconds;
            solution.TransferTimeSeconds = transferTime;
            solution.ParentFrameDeltaVMetersPerSecond = transferSpeedAtOrigin - circularSpeed;
            return true;
        }


        public static bool TryCalculateParkingOrbitEjection(
            OrbitalElements parkingOrbit,
            double referenceBodyRadiusMeters,
            CelestialBodyState originBody,
            TransferWindowSolution transfer,
            out ParkingOrbitEjectionSolution solution)
        {
            solution = null;
            if (parkingOrbit == null || originBody == null || transfer == null) return false;
            if (originBody.Orbit == null || !originBody.Orbit.HasFiniteGeometry || !parkingOrbit.HasFiniteGeometry ||
                !Vector3d.Finite(transfer.DepartureUniversalTimeSeconds)) return false;
            if (!IsFinitePositive(originBody.GravParameter) || !IsFinitePositive(referenceBodyRadiusMeters)) return false;
            if (!IsFinitePositive(originBody.Orbit.SemiMajorAxisMeters) || !IsFinitePositive(parkingOrbit.SemiMajorAxisMeters) ||
                !MatchesOptionalFrame(originBody.Orbit.ReferenceBodyName, originBody.ParentName) ||
                !MatchesOptionalFrame(parkingOrbit.ReferenceBodyName, originBody.Name) ||
                !MatchesOptionalFrame(transfer.ParentName, originBody.ParentName)) return false;

            if (parkingOrbit.Eccentricity < 0.0 || parkingOrbit.Eccentricity > 0.05) return false;

            double normalizedInclination = Math.Abs(parkingOrbit.InclinationDegrees) % 360.0;
            if (normalizedInclination > 180.0) normalizedInclination = 360.0 - normalizedInclination;
            if (normalizedInclination > 10.0) return false;

            double parkingRadius = Math.Abs(parkingOrbit.SemiMajorAxisMeters);
            if (!IsFinitePositive(parkingRadius) || parkingRadius <= referenceBodyRadiusMeters) return false;

            double mu = originBody.GravParameter;
            double vinf = Math.Abs(transfer.ParentFrameDeltaVMetersPerSecond);
            if (!IsFinitePositive(vinf)) return false;

            double parkingSpeed = Math.Sqrt(mu / parkingRadius);
            double hyperbolicPeriapsisSpeed = Math.Sqrt((vinf * vinf) + (2.0 * mu / parkingRadius));
            double ejectionDeltaV = hyperbolicPeriapsisSpeed - parkingSpeed;
            double hyperbolicEccentricity = 1.0 + ((parkingRadius * vinf * vinf) / mu);
            if (hyperbolicEccentricity <= 1.0) return false;

            double asymptoteAngle = Math.Acos(-1.0 / hyperbolicEccentricity);
            double parentMu = DeriveParentGravParameter(originBody.Orbit, Math.Abs(originBody.Orbit.SemiMajorAxisMeters));
            if (!IsFinitePositive(parentMu)) return false;

            double originLongitudeAtWindow = OrbitalLongitudeAtUniversalTime(
                originBody.Orbit,
                transfer.DepartureUniversalTimeSeconds,
                parentMu);
            double vesselLongitudeAtWindow = OrbitalLongitudeAtUniversalTime(
                parkingOrbit,
                transfer.DepartureUniversalTimeSeconds,
                mu);
            if (double.IsNaN(originLongitudeAtWindow) || double.IsNaN(vesselLongitudeAtWindow)) return false;

            double parentTangentDirection = NormalizeRadians(
                originLongitudeAtWindow +
                (transfer.ParentFrameDeltaVMetersPerSecond >= 0.0 ? Math.PI * 0.5 : -Math.PI * 0.5));
            double targetBurnRadiusDirection = NormalizeRadians(parentTangentDirection - asymptoteAngle);

            double vesselMeanMotion = Math.Sqrt(mu / (parkingRadius * parkingRadius * parkingRadius));
            double originParentRadius = Math.Abs(originBody.Orbit.SemiMajorAxisMeters);
            double originMeanMotion = Math.Sqrt(parentMu / (originParentRadius * originParentRadius * originParentRadius));
            double relativeRate = vesselMeanMotion - originMeanMotion;
            if (relativeRate <= 0.0) return false;

            double forwardAngle = NormalizeRadians(targetBurnRadiusDirection - vesselLongitudeAtWindow);
            double forwardSeconds = forwardAngle / relativeRate;
            double backwardSeconds = (forwardAngle - (2.0 * Math.PI)) / relativeRate;
            double windowOffset = Math.Abs(backwardSeconds) < Math.Abs(forwardSeconds)
                ? backwardSeconds
                : forwardSeconds;

            if (!Vector3d.Finite(windowOffset) || !Vector3d.Finite(transfer.DepartureUniversalTimeSeconds + windowOffset) ||
                !IsFinitePositive(ejectionDeltaV) || !Vector3d.Finite(asymptoteAngle)) return false;

            solution = new ParkingOrbitEjectionSolution();
            solution.ParkingAltitudeMeters = parkingRadius - referenceBodyRadiusMeters;
            solution.ParkingSpeedMetersPerSecond = parkingSpeed;
            solution.HyperbolicPeriapsisSpeedMetersPerSecond = hyperbolicPeriapsisSpeed;
            solution.HyperbolicExcessSpeedMetersPerSecond = vinf;
            solution.EjectionDeltaVMetersPerSecond = ejectionDeltaV;
            solution.AsymptoteAngleDegrees = asymptoteAngle * 180.0 / Math.PI;
            solution.BurnUniversalTimeSeconds = transfer.DepartureUniversalTimeSeconds + windowOffset;
            solution.WindowOffsetSeconds = windowOffset;
            solution.ExcessDirection = transfer.ParentFrameDeltaVMetersPerSecond >= 0.0
                ? "+PARENT TANGENT"
                : "-PARENT TANGENT";
            return true;
        }

        private static double DeriveParentGravParameter(OrbitalElements orbit, double semiMajorAxisMeters)
        {
            if (orbit == null || !IsFinitePositive(orbit.PeriodSeconds) || !IsFinitePositive(semiMajorAxisMeters))
                return double.NaN;
            double twoPi = 2.0 * Math.PI;
            return (twoPi * twoPi * semiMajorAxisMeters * semiMajorAxisMeters * semiMajorAxisMeters) /
                   (orbit.PeriodSeconds * orbit.PeriodSeconds);
        }

        private static double OrbitalLongitudeAtUniversalTime(
            OrbitalElements orbit,
            double universalTimeSeconds,
            double parentGravParameter)
        {
            if (orbit == null || orbit.Eccentricity < 0.0 || orbit.Eccentricity >= 1.0) return double.NaN;
            double a = Math.Abs(orbit.SemiMajorAxisMeters);
            if (!IsFinitePositive(a) || !IsFinitePositive(parentGravParameter)) return double.NaN;

            double meanMotion = Math.Sqrt(parentGravParameter / (a * a * a));
            double meanAnomaly = NormalizeRadians(
                orbit.MeanAnomalyAtEpochRadians +
                meanMotion * (universalTimeSeconds - orbit.EpochUniversalTimeSeconds));
            double eccentricAnomaly = SolveEllipticEccentricAnomaly(meanAnomaly, orbit.Eccentricity);

            double sinHalf = Math.Sqrt(1.0 + orbit.Eccentricity) * Math.Sin(eccentricAnomaly * 0.5);
            double cosHalf = Math.Sqrt(1.0 - orbit.Eccentricity) * Math.Cos(eccentricAnomaly * 0.5);
            double trueAnomaly = 2.0 * Math.Atan2(sinHalf, cosHalf);

            double orientation =
                (orbit.LongitudeOfAscendingNodeDegrees + orbit.ArgumentOfPeriapsisDegrees) *
                Math.PI / 180.0;
            return NormalizeRadians(orientation + trueAnomaly);
        }

        private static double SolveEllipticEccentricAnomaly(double meanAnomaly, double eccentricity)
        {
            double value = eccentricity < 0.8 ? meanAnomaly : Math.PI;
            for (int i = 0; i < 16; i++)
            {
                double f = value - eccentricity * Math.Sin(value) - meanAnomaly;
                double fp = 1.0 - eccentricity * Math.Cos(value);
                if (Math.Abs(fp) < 1e-12) break;
                double delta = f / fp;
                value -= delta;
                if (Math.Abs(delta) < 1e-12) break;
            }
            return value;
        }

        private static double NormalizeRadians(double radians)
        {
            double twoPi = 2.0 * Math.PI;
            radians %= twoPi;
            if (radians < 0.0) radians += twoPi;
            return radians;
        }

        private static bool IsFinitePositive(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0.0;
        }

        private static bool MatchesOptionalFrame(string referenceName, string expectedName)
        {
            // Older/synthetic element records may omit the redundant frame name.
            // A supplied name must never silently select a different origin.
            return string.IsNullOrWhiteSpace(referenceName) ||
                string.Equals(referenceName, expectedName, StringComparison.OrdinalIgnoreCase);
        }


    }
}
