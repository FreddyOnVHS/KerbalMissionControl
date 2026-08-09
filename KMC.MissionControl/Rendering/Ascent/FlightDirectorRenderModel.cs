namespace KMC.MissionControl.Rendering.Ascent
{
    /// <summary>
    /// Render-only projection of the Engine-owned ASCENT Flight Director.
    /// </summary>
    public sealed class FlightDirectorRenderModel
    {
        public bool Available { get; set; }
        public double MissionTimeSeconds { get; set; }
        public string FlightPhase { get; set; }
        public double TargetApoapsisMeters { get; set; }
        public double DownrangeMeters { get; set; }
        public double TargetAltitudeMeters { get; set; }
        public double ActualAltitudeMeters { get; set; }
        public double ActualPitchDegrees { get; set; }
        public double DynamicPressureKpa { get; set; }
        public double NominalPitchDegrees { get; set; }
        public double RecommendedPitchDegrees { get; set; }
        public double PitchCorrectionDegrees { get; set; }
        public double AltitudeErrorMeters { get; set; }
        public double ApoapsisErrorMeters { get; set; }
        public double RecoveryAuthorityPercent { get; set; }
        public bool IsTargetAchievable { get; set; }
        public string Command { get; set; }
        public string ThrottleCommand { get; set; }
        public string Status { get; set; }
        public string NextEvent { get; set; }
        public int MecoCountdownSeconds { get; set; }
        public bool CutoffRequired { get; set; }
        public bool CoastLockoutActive { get; set; }
        public bool OrbitHandoffRequired { get; set; }
        public bool FlashAlert { get; set; }
        public bool PredictiveGuidanceBlended { get; set; }
        public double PredictiveBlendFraction { get; set; }
    }
}
