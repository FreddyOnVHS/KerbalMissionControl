namespace KMC.MissionControl.Flight
{
    /// <summary>
    /// One stored ascent telemetry sample.
    ///
    /// The mutable DebugWritten flag prevents duplicate CSV rows for the
    /// same stored sample.
    /// </summary>
    public sealed class AscentHistorySample
    {
        public double MissionTime { get; set; }

        public double DownrangeMeters { get; set; }

        public double AltitudeMeters { get; set; }

        public double ApoapsisMeters { get; set; }

        public double PitchDegrees { get; set; }

        public double DynamicPressureKpa { get; set; }

        public double StageLiquidFuelAmount { get; set; }

        public double StageOxidizerAmount { get; set; }

        public double OrbitalSpeedMetersPerSecond { get; set; }

        public double VesselMassTonnes { get; set; }

        public double CurrentThrustKilonewtons { get; set; }

        public double AverageSpecificImpulseSeconds { get; set; }

        public int StageNumber { get; set; }

        public bool DebugWritten { get; set; }
    }
}
