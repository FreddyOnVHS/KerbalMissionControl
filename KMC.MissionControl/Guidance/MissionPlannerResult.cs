namespace KMC.MissionControl.Guidance
{
    public sealed class MissionPlannerResult
    {
        public double NominalPitchDegrees { get; set; }

        public double RecommendedPitchDegrees { get; set; }

        public double PitchCorrectionDegrees { get; set; }

        public double AltitudeErrorMeters { get; set; }

        public double ApoapsisErrorMeters { get; set; }

        public double RecoveryAuthorityPercent { get; set; }

        public string Command { get; set; }

        public string Status { get; set; }

        public bool IsTargetAchievable { get; set; }
    }
}
