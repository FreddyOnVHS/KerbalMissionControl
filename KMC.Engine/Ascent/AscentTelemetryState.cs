namespace KMC.Engine.Ascent
{
    /// <summary>
    /// Engine-owned snapshot of flight values relevant to ascent analysis.
    /// All values are copied from the shared KMC6 flight packet. No guidance
    /// interpretation is performed in Build 9.0.
    /// </summary>
    public sealed class AscentTelemetryState
    {
        public bool Available { get; internal set; }

        public string VesselName { get; internal set; } =
            string.Empty;

        public string BodyName { get; internal set; } =
            string.Empty;

        public double MissionTimeSeconds { get; internal set; }

        public int CurrentStage { get; internal set; }

        public double AltitudeMeters { get; internal set; }

        public double RadarAltitudeMeters { get; internal set; }

        public double VerticalSpeedMetersPerSecond { get; internal set; }

        public double HorizontalSpeedMetersPerSecond { get; internal set; }

        public double OrbitalSpeedMetersPerSecond { get; internal set; }

        public double PitchDegrees { get; internal set; }

        public double HeadingDegrees { get; internal set; }

        public double RollDegrees { get; internal set; }

        public double DynamicPressureKpa { get; internal set; }

        public double StaticPressureKpa { get; internal set; }

        public double Mach { get; internal set; }

        public double GForce { get; internal set; }

        public double ApoapsisMeters { get; internal set; }

        public double PeriapsisMeters { get; internal set; }

        public double TimeToApoapsisSeconds { get; internal set; }

        public double VesselMassTonnes { get; internal set; }

        public double CurrentThrustKilonewtons { get; internal set; }

        public double MaximumThrustKilonewtons { get; internal set; }

        public double ThrustToWeightRatio { get; internal set; }

        public double ThrottleCommand { get; internal set; }

        public double AverageSpecificImpulseSeconds { get; internal set; }

        public double StageLiquidFuelAmount { get; internal set; }

        public double StageLiquidFuelCapacity { get; internal set; }

        public double StageOxidizerAmount { get; internal set; }

        public double StageOxidizerCapacity { get; internal set; }
    }
}
