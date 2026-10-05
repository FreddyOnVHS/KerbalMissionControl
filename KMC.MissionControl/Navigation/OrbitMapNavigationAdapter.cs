using System;
using KMC.Engine.CelestialMechanics;
using KMC.Engine.Navigation;
using KMC.Shared;

namespace KMC.MissionControl.Navigation
{
    /// <summary>Copies telemetry into engine-owned data. Packet PositionXYZ fields
    /// belong to rendering/telemetry frames and must not be treated as generic states.
    /// Elements retain their explicit degree/radian/UT units without axis swapping.</summary>
    public static class OrbitMapNavigationAdapter
    {
        public static OrbitalElements ToElements(OrbitMapOrbit orbit)
        {
            if (orbit == null) return null;
            return new OrbitalElements
            {
                ReferenceBodyName = orbit.ReferenceBodyName,
                SemiMajorAxisMeters = orbit.SemiMajorAxisMeters,
                Eccentricity = orbit.Eccentricity,
                InclinationDegrees = orbit.InclinationDegrees,
                LongitudeOfAscendingNodeDegrees = orbit.LongitudeOfAscendingNodeDegrees,
                ArgumentOfPeriapsisDegrees = orbit.ArgumentOfPeriapsisDegrees,
                EpochUniversalTimeSeconds = orbit.EpochUniversalTimeSeconds,
                MeanAnomalyAtEpochRadians = orbit.MeanAnomalyAtEpochRadians,
                PeriodSeconds = orbit.PeriodSeconds
            };
        }

        public static CelestialBodyState ToBody(OrbitMapBody body)
        {
            if (body == null) return null;
            return new CelestialBodyState
            {
                Name = body.Name, ParentName = body.ParentName,
                RadiusMeters = body.RadiusMeters, SoiRadiusMeters = body.SoiRadiusMeters,
                GravParameter = body.GravParameter, Orbit = ToElements(body.Orbit)
            };
        }

        public static bool TryCalculateTransferWindow(OrbitMapBody origin, OrbitMapBody destination,
            double currentUniversalTimeSeconds, out TransferWindowSolution solution)
        {
            return HohmannTransferPlanner.TryCalculateTransferWindow(ToBody(origin), ToBody(destination),
                currentUniversalTimeSeconds, out solution);
        }

        public static bool TryCalculateParkingOrbitEjection(OrbitMapPacket packet, OrbitMapBody origin,
            TransferWindowSolution transfer, out ParkingOrbitEjectionSolution solution)
        {
            solution = null;
            if (packet != null && origin != null && !string.IsNullOrWhiteSpace(packet.ReferenceBodyName) &&
                !string.Equals(packet.ReferenceBodyName, origin.Name, StringComparison.OrdinalIgnoreCase)) return false;
            return packet != null && HohmannTransferPlanner.TryCalculateParkingOrbitEjection(
                ToElements(packet.ActiveOrbit), packet.ReferenceBodyRadiusMeters, ToBody(origin), transfer, out solution);
        }

        /// <summary>
        /// Creates a coarse Lambert comparison from the real OrbitMap celestial
        /// catalog. Preview only: no maneuver packet is created here.
        /// </summary>
        public static bool TryCalculateLambertPreview(
            OrbitMapPacket packet,
            OrbitMapBody origin,
            OrbitMapBody destination,
            TransferWindowSolution hohmann,
            out TransferSearchSolution solution,
            out string parentMuSource)
        {
            solution = null;
            parentMuSource = string.Empty;

            if (packet == null || origin == null || destination == null ||
                hohmann == null || origin.Orbit == null || destination.Orbit == null)
                return false;

            if (!string.Equals(
                    origin.ParentName,
                    destination.ParentName,
                    StringComparison.OrdinalIgnoreCase))
                return false;

            double parentMu;
            if (!TryResolveParentGravParameter(
                    packet,
                    origin,
                    destination,
                    out parentMu,
                    out parentMuSource))
                return false;

            double synodicSeconds =
                CalculateSynodicPeriodSeconds(
                    origin.Orbit.PeriodSeconds,
                    destination.Orbit.PeriodSeconds);

            double departureHalfSpan =
                IsFinitePositive(synodicSeconds)
                    ? synodicSeconds * 0.08
                    : hohmann.TransferTimeSeconds * 0.35;

            if (!IsFinitePositive(departureHalfSpan))
                return false;

            double earliestDeparture =
                Math.Max(
                    packet.UniversalTimeSeconds,
                    hohmann.DepartureUniversalTimeSeconds -
                    departureHalfSpan);

            double latestDeparture =
                hohmann.DepartureUniversalTimeSeconds +
                departureHalfSpan;

            double minimumFlightTime =
                hohmann.TransferTimeSeconds * 0.70;

            double maximumFlightTime =
                hohmann.TransferTimeSeconds * 1.30;

            if (!IsFinitePositive(minimumFlightTime) ||
                !IsFinitePositive(maximumFlightTime) ||
                latestDeparture < earliestDeparture)
                return false;

            TransferSearchRequest request =
                new TransferSearchRequest
                {
                    OriginBody = ToBody(origin),
                    DestinationBody = ToBody(destination),
                    ParentGravParameter = parentMu,
                    EarliestDepartureUniversalTimeSeconds = earliestDeparture,
                    LatestDepartureUniversalTimeSeconds = latestDeparture,
                    MinimumTimeOfFlightSeconds = minimumFlightTime,
                    MaximumTimeOfFlightSeconds = maximumFlightTime,
                    DepartureSamples = 9,
                    TimeOfFlightSamples = 9,
                    SearchShortWay = true,
                    SearchLongWay = true
                };

            return LambertTransferSearch.TryFindBest(
                request,
                out solution);
        }

        private static bool TryResolveParentGravParameter(
            OrbitMapPacket packet,
            OrbitMapBody origin,
            OrbitMapBody destination,
            out double parentMu,
            out string source)
        {
            parentMu = double.NaN;
            source = string.Empty;

            if (packet == null || origin == null || destination == null ||
                string.IsNullOrWhiteSpace(origin.ParentName))
                return false;

            for (int i = 0; i < packet.Bodies.Count; i++)
            {
                OrbitMapBody body = packet.Bodies[i];
                if (body == null) continue;

                if (string.Equals(
                        body.Name,
                        origin.ParentName,
                        StringComparison.OrdinalIgnoreCase) &&
                    IsFinitePositive(body.GravParameter))
                {
                    parentMu = body.GravParameter;
                    source = "PARENT BODY TELEMETRY";
                    return true;
                }
            }

            parentMu =
                DeriveGravParameterFromOrbit(
                    origin.Orbit);

            if (!IsFinitePositive(parentMu))
            {
                parentMu =
                    DeriveGravParameterFromOrbit(
                        destination.Orbit);
            }

            if (!IsFinitePositive(parentMu))
                return false;

            source = "ORBIT PERIOD";
            return true;
        }

        private static double DeriveGravParameterFromOrbit(
            OrbitMapOrbit orbit)
        {
            if (orbit == null ||
                !IsFinitePositive(orbit.SemiMajorAxisMeters) ||
                !IsFinitePositive(orbit.PeriodSeconds))
                return double.NaN;

            double a = orbit.SemiMajorAxisMeters;
            double twoPi = 2.0 * Math.PI;

            return
                (twoPi * twoPi * a * a * a) /
                (orbit.PeriodSeconds * orbit.PeriodSeconds);
        }

        private static double CalculateSynodicPeriodSeconds(
            double firstPeriodSeconds,
            double secondPeriodSeconds)
        {
            if (!IsFinitePositive(firstPeriodSeconds) ||
                !IsFinitePositive(secondPeriodSeconds))
                return double.NaN;

            double relativeFrequency =
                Math.Abs(
                    (1.0 / firstPeriodSeconds) -
                    (1.0 / secondPeriodSeconds));

            if (relativeFrequency <= 1e-15)
                return double.NaN;

            return 1.0 / relativeFrequency;
        }

        private static bool IsFinitePositive(double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value > 0.0;
        }
    }
}
