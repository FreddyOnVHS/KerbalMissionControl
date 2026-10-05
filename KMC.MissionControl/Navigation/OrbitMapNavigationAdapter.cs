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
    }
}
