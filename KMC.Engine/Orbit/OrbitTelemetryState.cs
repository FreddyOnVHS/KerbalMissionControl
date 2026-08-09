using System;

namespace KMC.Engine.Orbit
{
    /// <summary>
    /// Engine-owned copy of the current flight/orbit measurements required by
    /// later ORBIT guidance milestones.
    ///
    /// Build 10.0 deliberately copies telemetry only. It does not calculate
    /// circularization or periapsis-recovery commands.
    /// </summary>
    public sealed class OrbitTelemetryState
    {
        public bool Available { get; internal set; }

        public DateTime ReceivedUtc { get; internal set; }

        public string VesselName { get; internal set; } =
            string.Empty;

        public string BodyName { get; internal set; } =
            string.Empty;

        public double MissionTimeSeconds { get; internal set; }

        public double AltitudeMeters { get; internal set; }

        public double OrbitalSpeedMetersPerSecond { get; internal set; }

        public double HorizontalSpeedMetersPerSecond { get; internal set; }

        public double VerticalSpeedMetersPerSecond { get; internal set; }

        public double ApoapsisMeters { get; internal set; }

        public double PeriapsisMeters { get; internal set; }

        public double TimeToApoapsisSeconds { get; internal set; }

        public double TimeToPeriapsisSeconds { get; internal set; }

        public double Eccentricity { get; internal set; }

        public double SemiMajorAxisMeters { get; internal set; }

        public double TrueAnomalyDegrees { get; internal set; }

        public double ArgumentOfPeriapsisDegrees { get; internal set; }

        public double InclinationDegrees { get; internal set; }

        public double LongitudeOfAscendingNodeDegrees { get; internal set; }

        public double OrbitalPeriodSeconds { get; internal set; }

        public double VesselMassTonnes { get; internal set; }

        public double CurrentThrustKilonewtons { get; internal set; }

        public double MaximumThrustKilonewtons { get; internal set; }

        public double AverageSpecificImpulseSeconds { get; internal set; }

        public double Throttle { get; internal set; }

        public int CurrentStage { get; internal set; }

        public static OrbitTelemetryState FromPacket(
            KMC.Shared.TelemetryPacket packet,
            DateTime receivedUtc)
        {
            OrbitTelemetryState result =
                new OrbitTelemetryState();

            if (packet == null)
            {
                return result;
            }

            result.Available =
                true;

            result.ReceivedUtc =
                receivedUtc;

            result.VesselName =
                packet.VesselName ??
                string.Empty;

            result.BodyName =
                packet.BodyName ??
                string.Empty;

            result.MissionTimeSeconds =
                packet.MissionTime;

            result.AltitudeMeters =
                packet.Altitude;

            result.OrbitalSpeedMetersPerSecond =
                packet.OrbitalSpeed;

            result.HorizontalSpeedMetersPerSecond =
                packet.HorizontalSpeed;

            result.VerticalSpeedMetersPerSecond =
                packet.VerticalSpeed;

            result.ApoapsisMeters =
                packet.Apoapsis;

            result.PeriapsisMeters =
                packet.Periapsis;

            result.TimeToApoapsisSeconds =
                packet.TimeToApoapsis;

            result.TimeToPeriapsisSeconds =
                packet.TimeToPeriapsis;

            result.Eccentricity =
                packet.Eccentricity;

            result.SemiMajorAxisMeters =
                packet.SemiMajorAxis;

            result.TrueAnomalyDegrees =
                packet.TrueAnomalyDegrees;

            result.ArgumentOfPeriapsisDegrees =
                packet.ArgumentOfPeriapsisDegrees;

            result.InclinationDegrees =
                packet.InclinationDegrees;

            result.LongitudeOfAscendingNodeDegrees =
                packet.LongitudeOfAscendingNodeDegrees;

            result.OrbitalPeriodSeconds =
                packet.OrbitalPeriod;

            result.VesselMassTonnes =
                packet.VesselMass;

            result.CurrentThrustKilonewtons =
                packet.CurrentThrust;

            result.MaximumThrustKilonewtons =
                packet.MaximumThrust;

            result.AverageSpecificImpulseSeconds =
                packet.AverageSpecificImpulse;

            result.Throttle =
                packet.Throttle;

            result.CurrentStage =
                packet.CurrentStage;

            return result;
        }

        internal static OrbitTelemetryState Clone(
            OrbitTelemetryState source)
        {
            if (source == null)
            {
                return new OrbitTelemetryState();
            }

            return new OrbitTelemetryState
            {
                Available =
                    source.Available,

                ReceivedUtc =
                    source.ReceivedUtc,

                VesselName =
                    source.VesselName,

                BodyName =
                    source.BodyName,

                MissionTimeSeconds =
                    source.MissionTimeSeconds,

                AltitudeMeters =
                    source.AltitudeMeters,

                OrbitalSpeedMetersPerSecond =
                    source.OrbitalSpeedMetersPerSecond,

                HorizontalSpeedMetersPerSecond =
                    source.HorizontalSpeedMetersPerSecond,

                VerticalSpeedMetersPerSecond =
                    source.VerticalSpeedMetersPerSecond,

                ApoapsisMeters =
                    source.ApoapsisMeters,

                PeriapsisMeters =
                    source.PeriapsisMeters,

                TimeToApoapsisSeconds =
                    source.TimeToApoapsisSeconds,

                TimeToPeriapsisSeconds =
                    source.TimeToPeriapsisSeconds,

                Eccentricity =
                    source.Eccentricity,

                SemiMajorAxisMeters =
                    source.SemiMajorAxisMeters,

                TrueAnomalyDegrees =
                    source.TrueAnomalyDegrees,

                ArgumentOfPeriapsisDegrees =
                    source.ArgumentOfPeriapsisDegrees,

                InclinationDegrees =
                    source.InclinationDegrees,

                LongitudeOfAscendingNodeDegrees =
                    source.LongitudeOfAscendingNodeDegrees,

                OrbitalPeriodSeconds =
                    source.OrbitalPeriodSeconds,

                VesselMassTonnes =
                    source.VesselMassTonnes,

                CurrentThrustKilonewtons =
                    source.CurrentThrustKilonewtons,

                MaximumThrustKilonewtons =
                    source.MaximumThrustKilonewtons,

                AverageSpecificImpulseSeconds =
                    source.AverageSpecificImpulseSeconds,

                Throttle =
                    source.Throttle,

                CurrentStage =
                    source.CurrentStage
            };
        }
    }
}
