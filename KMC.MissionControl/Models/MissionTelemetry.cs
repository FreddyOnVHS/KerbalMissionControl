namespace KMC.MissionControl.Models
{
    public sealed class MissionTelemetry
    {
        public string VesselName { get; set; } =
            string.Empty;

        public string BodyName { get; set; } =
            string.Empty;

        public double MissionTime { get; set; }

        public double Altitude { get; set; }

        public double RadarAltitude { get; set; }

        public double Apoapsis { get; set; }

        public double Periapsis { get; set; }

        public double TimeToApoapsis { get; set; }

        public double TimeToPeriapsis { get; set; }

        /*
         * Keplerian orbital elements.
         *
         * Angular values are expressed in degrees.
         * Distance values are expressed in meters.
         * Time values are expressed in seconds.
         */

        public double Eccentricity { get; set; }

        public double SemiMajorAxis { get; set; }

        public double TrueAnomalyDegrees { get; set; }

        public double ArgumentOfPeriapsisDegrees { get; set; }

        public double InclinationDegrees { get; set; }

        public double LongitudeOfAscendingNodeDegrees { get; set; }

        public double OrbitalPeriod { get; set; }

        public double SurfaceSpeed { get; set; }

        public double HorizontalSpeed { get; set; }

        public double VerticalSpeed { get; set; }

        public double OrbitalSpeed { get; set; }

        public double Throttle { get; set; }

        public int CurrentStage { get; set; }

        public double GForce { get; set; }

        public double Pitch { get; set; }

        public double Heading { get; set; }

        public double Roll { get; set; }

        public double DynamicPressureKpa { get; set; }

        public double StaticPressureKpa { get; set; }

        public double Mach { get; set; }

        public double VesselMass { get; set; }

        public double CurrentThrust { get; set; }

        public double MaximumThrust { get; set; }

        public double ThrustToWeightRatio { get; set; }

        /*
         * Engine summary telemetry.
         */

        public int EngineCount { get; set; }

        public int IgnitedEngineCount { get; set; }

        public int ProducingThrustEngineCount { get; set; }

        public int FlameoutEngineCount { get; set; }

        public double AverageSpecificImpulse { get; set; }

        /*
         * Current-stage resource telemetry.
         */

        public double StageLiquidFuelAmount { get; set; }

        public double StageLiquidFuelCapacity { get; set; }

        public double StageOxidizerAmount { get; set; }

        public double StageOxidizerCapacity { get; set; }

        public double StageMonopropellantAmount { get; set; }

        public double StageMonopropellantCapacity { get; set; }

        /*
         * Vessel-wide resource telemetry.
         */

        public double TotalLiquidFuelAmount { get; set; }

        public double TotalLiquidFuelCapacity { get; set; }

        public double TotalOxidizerAmount { get; set; }

        public double TotalOxidizerCapacity { get; set; }

        public double TotalMonopropellantAmount { get; set; }

        public double TotalMonopropellantCapacity { get; set; }
    }
}