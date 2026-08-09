using System;

namespace KMC.Engine.Ascent
{
    internal sealed class AscentEnergyManager
    {
        private const double KerbinRadiusMeters =
            600000.0;

        private const double Mu =
            3.5316e12;

        public double CalculateTargetEnergyError(
            AscentTelemetryState telemetry,
            double targetAltitudeMeters)
        {
            if (telemetry == null)
            {
                return double.NaN;
            }

            double radius =
                KerbinRadiusMeters +
                Math.Max(
                    0.0,
                    telemetry.AltitudeMeters);

            double speed =
                Math.Max(
                    0.0,
                    telemetry.OrbitalSpeedMetersPerSecond);

            double currentEnergy =
                speed *
                speed /
                2.0 -
                Mu /
                radius;

            double targetRadius =
                KerbinRadiusMeters +
                targetAltitudeMeters;

            double targetEnergy =
                -Mu /
                (2.0 *
                 targetRadius);

            return
                targetEnergy -
                currentEnergy;
        }
    }
}
