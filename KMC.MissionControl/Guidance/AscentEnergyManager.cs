using System;
using KMC.MissionControl.Models;

namespace KMC.MissionControl.Guidance
{
    public sealed class AscentEnergyManager
    {
        private const double KerbinRadiusMeters = 600000.0;
        private const double Mu = 3.5316e12;

        public double CalculateTargetEnergyError(
            MissionTelemetry telemetry,
            double targetAltitudeMeters)
        {
            if (telemetry == null)
            {
                return double.NaN;
            }

            double radius =
                KerbinRadiusMeters +
                Math.Max(0.0, telemetry.Altitude);

            double speed =
                Math.Max(0.0, telemetry.OrbitalSpeed);

            double currentEnergy =
                speed * speed / 2.0 -
                Mu / radius;

            double targetRadius =
                KerbinRadiusMeters +
                targetAltitudeMeters;

            double targetEnergy =
                -Mu /
                (2.0 * targetRadius);

            return
                targetEnergy -
                currentEnergy;
        }
    }
}
