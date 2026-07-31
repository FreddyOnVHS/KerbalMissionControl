using System;
using System.Globalization;

namespace KMC.Shared
{
    /// <summary>
    /// A single telemetry update sent from KSP to Mission Control.
    /// </summary>
    public sealed class TelemetryPacket
    {
        /*
         * KMC5 adds engine summary telemetry:
         *
         * EngineCount
         * IgnitedEngineCount
         * ProducingThrustEngineCount
         * FlameoutEngineCount
         * AverageSpecificImpulse
         *
         * It also retains the KMC4 stage and vessel-wide
         * fuel resource telemetry.
         */

        public const string ProtocolId = "KMC6";

        public const int TelemetryPort = 5055;

        public DateTime TimestampUtc { get; set; }

        public string VesselName { get; set; }

        public string BodyName { get; set; }

        public double MissionTime { get; set; }

        public double Altitude { get; set; }

        public double SurfaceSpeed { get; set; }

        public double VerticalSpeed { get; set; }

        public double OrbitalSpeed { get; set; }

        public double Apoapsis { get; set; }

        public double Periapsis { get; set; }

        public double Throttle { get; set; }

        public int CurrentStage { get; set; }

        public double GForce { get; set; }

        public double Pitch { get; set; }

        public double Heading { get; set; }

        public double Roll { get; set; }

        public double HorizontalSpeed { get; set; }

        public double RadarAltitude { get; set; }

        public double DynamicPressureKpa { get; set; }

        public double StaticPressureKpa { get; set; }

        public double Mach { get; set; }

        public double VesselMass { get; set; }

        public double CurrentThrust { get; set; }

        public double MaximumThrust { get; set; }

        public double ThrustToWeightRatio { get; set; }

        public int EngineCount { get; set; }

        public int IgnitedEngineCount { get; set; }

        public int ProducingThrustEngineCount { get; set; }

        public int FlameoutEngineCount { get; set; }

        public double AverageSpecificImpulse { get; set; }

        public double TimeToApoapsis { get; set; }

        /*
 * Orbital-element telemetry.
 *
 * All angular values transmitted by KMC are expressed in degrees.
 * Distances are expressed in meters and time values in seconds.
 */

        public double Eccentricity { get; set; }

        public double SemiMajorAxis { get; set; }

        public double TrueAnomalyDegrees { get; set; }

        public double ArgumentOfPeriapsisDegrees { get; set; }

        public double InclinationDegrees { get; set; }

        public double LongitudeOfAscendingNodeDegrees { get; set; }

        public double OrbitalPeriod { get; set; }

        public double TimeToPeriapsis { get; set; }

        public double StageLiquidFuelAmount { get; set; }

        public double StageLiquidFuelCapacity { get; set; }

        public double StageOxidizerAmount { get; set; }

        public double StageOxidizerCapacity { get; set; }

        public double StageMonopropellantAmount { get; set; }

        public double StageMonopropellantCapacity { get; set; }

        public double TotalLiquidFuelAmount { get; set; }

        public double TotalLiquidFuelCapacity { get; set; }

        public double TotalOxidizerAmount { get; set; }

        public double TotalOxidizerCapacity { get; set; }

        public double TotalMonopropellantAmount { get; set; }

        public double TotalMonopropellantCapacity { get; set; }

        public TelemetryPacket()
        {
            TimestampUtc = DateTime.UtcNow;
            VesselName = string.Empty;
            BodyName = string.Empty;
        }

        public string Serialize()
        {
            string[] fields =
            {
                ProtocolId,

                TimestampUtc.Ticks.ToString(
                    CultureInfo.InvariantCulture),

                Uri.EscapeDataString(
                    VesselName ?? string.Empty),

                Uri.EscapeDataString(
                    BodyName ?? string.Empty),

                FormatDouble(MissionTime),
                FormatDouble(Altitude),
                FormatDouble(SurfaceSpeed),
                FormatDouble(VerticalSpeed),
                FormatDouble(OrbitalSpeed),
                FormatDouble(Apoapsis),
                FormatDouble(Periapsis),
                FormatDouble(Throttle),

                CurrentStage.ToString(
                    CultureInfo.InvariantCulture),

                FormatDouble(GForce),
                FormatDouble(Pitch),
                FormatDouble(Heading),
                FormatDouble(Roll),
                FormatDouble(HorizontalSpeed),
                FormatDouble(RadarAltitude),
                FormatDouble(DynamicPressureKpa),
                FormatDouble(StaticPressureKpa),
                FormatDouble(Mach),
                FormatDouble(VesselMass),
                FormatDouble(CurrentThrust),
                FormatDouble(MaximumThrust),
                FormatDouble(ThrustToWeightRatio),

                EngineCount.ToString(
                    CultureInfo.InvariantCulture),

                IgnitedEngineCount.ToString(
                    CultureInfo.InvariantCulture),

                ProducingThrustEngineCount.ToString(
                    CultureInfo.InvariantCulture),

                FlameoutEngineCount.ToString(
                    CultureInfo.InvariantCulture),

                FormatDouble(AverageSpecificImpulse),
                FormatDouble(TimeToApoapsis),

                FormatDouble(Eccentricity),
                FormatDouble(SemiMajorAxis),
                FormatDouble(TrueAnomalyDegrees),
                FormatDouble(ArgumentOfPeriapsisDegrees),
                FormatDouble(InclinationDegrees),
                FormatDouble(LongitudeOfAscendingNodeDegrees),
                FormatDouble(OrbitalPeriod),
                FormatDouble(TimeToPeriapsis),

                FormatDouble(StageLiquidFuelAmount),
                FormatDouble(StageLiquidFuelCapacity),
                FormatDouble(StageOxidizerAmount),
                FormatDouble(StageOxidizerCapacity),
                FormatDouble(StageMonopropellantAmount),
                FormatDouble(StageMonopropellantCapacity),

                FormatDouble(TotalLiquidFuelAmount),
                FormatDouble(TotalLiquidFuelCapacity),
                FormatDouble(TotalOxidizerAmount),
                FormatDouble(TotalOxidizerCapacity),
                FormatDouble(TotalMonopropellantAmount),
                FormatDouble(TotalMonopropellantCapacity)
            };

            return string.Join(
                "|",
                fields);
        }

        public static bool TryParse(
            string message,
            out TelemetryPacket packet)
        {
            packet = null;

            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            string[] parts =
                message.Split('|');

            if (parts.Length != 52 ||
                parts[0] != ProtocolId)
            {
                return false;
            }

            try
            {
                int index = 1;

                long timestampTicks;

                if (!TryReadLong(
                        parts,
                        ref index,
                        out timestampTicks))
                {
                    return false;
                }

                string vesselName =
                    Uri.UnescapeDataString(
                        parts[index++]);

                string bodyName =
                    Uri.UnescapeDataString(
                        parts[index++]);

                double missionTime;
                double altitude;
                double surfaceSpeed;
                double verticalSpeed;
                double orbitalSpeed;
                double apoapsis;
                double periapsis;
                double throttle;

                int currentStage;

                double gForce;
                double pitch;
                double heading;
                double roll;
                double horizontalSpeed;
                double radarAltitude;
                double dynamicPressureKpa;
                double staticPressureKpa;
                double mach;
                double vesselMass;
                double currentThrust;
                double maximumThrust;
                double thrustToWeightRatio;

                int engineCount;
                int ignitedEngineCount;
                int producingThrustEngineCount;
                int flameoutEngineCount;

                double averageSpecificImpulse;
                double timeToApoapsis;

                double eccentricity;
                double semiMajorAxis;
                double trueAnomalyDegrees;
                double argumentOfPeriapsisDegrees;
                double inclinationDegrees;
                double longitudeOfAscendingNodeDegrees;
                double orbitalPeriod;
                double timeToPeriapsis;

                double stageLiquidFuelAmount;
                double stageLiquidFuelCapacity;
                double stageOxidizerAmount;
                double stageOxidizerCapacity;
                double stageMonopropellantAmount;
                double stageMonopropellantCapacity;

                double totalLiquidFuelAmount;
                double totalLiquidFuelCapacity;
                double totalOxidizerAmount;
                double totalOxidizerCapacity;
                double totalMonopropellantAmount;
                double totalMonopropellantCapacity;

                if (!TryReadDouble(parts, ref index, out missionTime) ||
                    !TryReadDouble(parts, ref index, out altitude) ||
                    !TryReadDouble(parts, ref index, out surfaceSpeed) ||
                    !TryReadDouble(parts, ref index, out verticalSpeed) ||
                    !TryReadDouble(parts, ref index, out orbitalSpeed) ||
                    !TryReadDouble(parts, ref index, out apoapsis) ||
                    !TryReadDouble(parts, ref index, out periapsis) ||
                    !TryReadDouble(parts, ref index, out throttle) ||
                    !TryReadInt(parts, ref index, out currentStage) ||
                    !TryReadDouble(parts, ref index, out gForce) ||
                    !TryReadDouble(parts, ref index, out pitch) ||
                    !TryReadDouble(parts, ref index, out heading) ||
                    !TryReadDouble(parts, ref index, out roll) ||
                    !TryReadDouble(parts, ref index, out horizontalSpeed) ||
                    !TryReadDouble(parts, ref index, out radarAltitude) ||
                    !TryReadDouble(parts, ref index, out dynamicPressureKpa) ||
                    !TryReadDouble(parts, ref index, out staticPressureKpa) ||
                    !TryReadDouble(parts, ref index, out mach) ||
                    !TryReadDouble(parts, ref index, out vesselMass) ||
                    !TryReadDouble(parts, ref index, out currentThrust) ||
                    !TryReadDouble(parts, ref index, out maximumThrust) ||
                    !TryReadDouble(parts, ref index, out thrustToWeightRatio) ||
                    !TryReadInt(parts, ref index, out engineCount) ||
                    !TryReadInt(parts, ref index, out ignitedEngineCount) ||
                    !TryReadInt(parts, ref index, out producingThrustEngineCount) ||
                    !TryReadInt(parts, ref index, out flameoutEngineCount) ||
                    !TryReadDouble(
    parts,
    ref index,
    out averageSpecificImpulse) ||

!TryReadDouble(
    parts,
    ref index,
    out timeToApoapsis) ||

!TryReadDouble(
    parts,
    ref index,
    out eccentricity) ||

!TryReadDouble(
    parts,
    ref index,
    out semiMajorAxis) ||

!TryReadDouble(
    parts,
    ref index,
    out trueAnomalyDegrees) ||

!TryReadDouble(
    parts,
    ref index,
    out argumentOfPeriapsisDegrees) ||

!TryReadDouble(
    parts,
    ref index,
    out inclinationDegrees) ||

!TryReadDouble(
    parts,
    ref index,
    out longitudeOfAscendingNodeDegrees) ||

!TryReadDouble(
    parts,
    ref index,
    out orbitalPeriod) ||

!TryReadDouble(
    parts,
    ref index,
    out timeToPeriapsis) ||

!TryReadDouble(
    parts,
    ref index,
    out stageLiquidFuelAmount) ||
                    !TryReadDouble(parts, ref index, out stageLiquidFuelCapacity) ||
                    !TryReadDouble(parts, ref index, out stageOxidizerAmount) ||
                    !TryReadDouble(parts, ref index, out stageOxidizerCapacity) ||
                    !TryReadDouble(parts, ref index, out stageMonopropellantAmount) ||
                    !TryReadDouble(parts, ref index, out stageMonopropellantCapacity) ||
                    !TryReadDouble(parts, ref index, out totalLiquidFuelAmount) ||
                    !TryReadDouble(parts, ref index, out totalLiquidFuelCapacity) ||
                    !TryReadDouble(parts, ref index, out totalOxidizerAmount) ||
                    !TryReadDouble(parts, ref index, out totalOxidizerCapacity) ||
                    !TryReadDouble(parts, ref index, out totalMonopropellantAmount) ||
                    !TryReadDouble(parts, ref index, out totalMonopropellantCapacity))
                {
                    return false;
                }

                if (index != parts.Length)
                {
                    return false;
                }

                packet =
                    new TelemetryPacket
                    {
                        TimestampUtc =
                            new DateTime(
                                timestampTicks,
                                DateTimeKind.Utc),

                        VesselName =
                            vesselName,

                        BodyName =
                            bodyName,

                        MissionTime =
                            missionTime,

                        Altitude =
                            altitude,

                        SurfaceSpeed =
                            surfaceSpeed,

                        VerticalSpeed =
                            verticalSpeed,

                        OrbitalSpeed =
                            orbitalSpeed,

                        Apoapsis =
                            apoapsis,

                        Periapsis =
                            periapsis,

                        Throttle =
                            throttle,

                        CurrentStage =
                            currentStage,

                        GForce =
                            gForce,

                        Pitch =
                            pitch,

                        Heading =
                            heading,

                        Roll =
                            roll,

                        HorizontalSpeed =
                            horizontalSpeed,

                        RadarAltitude =
                            radarAltitude,

                        DynamicPressureKpa =
                            dynamicPressureKpa,

                        StaticPressureKpa =
                            staticPressureKpa,

                        Mach =
                            mach,

                        VesselMass =
                            vesselMass,

                        CurrentThrust =
                            currentThrust,

                        MaximumThrust =
                            maximumThrust,

                        ThrustToWeightRatio =
                            thrustToWeightRatio,

                        EngineCount =
                            engineCount,

                        IgnitedEngineCount =
                            ignitedEngineCount,

                        ProducingThrustEngineCount =
                            producingThrustEngineCount,

                        FlameoutEngineCount =
                            flameoutEngineCount,

                        AverageSpecificImpulse =
                            averageSpecificImpulse,

                        TimeToApoapsis =
                            timeToApoapsis,

                        Eccentricity =
                            eccentricity,

                        SemiMajorAxis =
                            semiMajorAxis,

                        TrueAnomalyDegrees =
                            trueAnomalyDegrees,

                        ArgumentOfPeriapsisDegrees =
                            argumentOfPeriapsisDegrees,

                        InclinationDegrees =
                            inclinationDegrees,

                        LongitudeOfAscendingNodeDegrees =
                            longitudeOfAscendingNodeDegrees,

                        OrbitalPeriod =
                            orbitalPeriod,

                        TimeToPeriapsis =
                            timeToPeriapsis,

                        StageLiquidFuelAmount =
                            stageLiquidFuelAmount,

                        StageLiquidFuelCapacity =
                            stageLiquidFuelCapacity,

                        StageOxidizerAmount =
                            stageOxidizerAmount,

                        StageOxidizerCapacity =
                            stageOxidizerCapacity,

                        StageMonopropellantAmount =
                            stageMonopropellantAmount,

                        StageMonopropellantCapacity =
                            stageMonopropellantCapacity,

                        TotalLiquidFuelAmount =
                            totalLiquidFuelAmount,

                        TotalLiquidFuelCapacity =
                            totalLiquidFuelCapacity,

                        TotalOxidizerAmount =
                            totalOxidizerAmount,

                        TotalOxidizerCapacity =
                            totalOxidizerCapacity,

                        TotalMonopropellantAmount =
                            totalMonopropellantAmount,

                        TotalMonopropellantCapacity =
                            totalMonopropellantCapacity
                    };

                return true;
            }
            catch
            {
                packet = null;
                return false;
            }
        }

        private static string FormatDouble(
            double value)
        {
            return value.ToString(
                "R",
                CultureInfo.InvariantCulture);
        }

        private static bool TryReadLong(
            string[] parts,
            ref int index,
            out long result)
        {
            result = 0L;

            if (index >= parts.Length)
            {
                return false;
            }

            bool success =
                long.TryParse(
                    parts[index],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out result);

            index++;
            return success;
        }

        private static bool TryReadInt(
            string[] parts,
            ref int index,
            out int result)
        {
            result = 0;

            if (index >= parts.Length)
            {
                return false;
            }

            bool success =
                int.TryParse(
                    parts[index],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out result);

            index++;
            return success;
        }

        private static bool TryReadDouble(
            string[] parts,
            ref int index,
            out double result)
        {
            result = 0.0;

            if (index >= parts.Length)
            {
                return false;
            }

            bool success =
                double.TryParse(
                    parts[index],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out result);

            index++;
            return success;
        }
    }
}