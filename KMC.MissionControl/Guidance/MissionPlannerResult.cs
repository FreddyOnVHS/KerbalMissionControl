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

        public double ThrottleCommandPercent { get; set; }

        public double CircularizationDeltaV { get; set; }

        public double CircularizationBurnTimeSeconds { get; set; }

        public double CircularizationIgnitionInSeconds { get; set; }

        public double CircularizationPeriapsisErrorMeters { get; set; }

        public string FlightPhase { get; set; }

        public string Command { get; set; }

        public string ThrottleCommand { get; set; }

        public string Status { get; set; }

        public string NextEvent { get; set; }

        public bool IsTargetAchievable { get; set; }

        public bool CutoffRequired { get; set; }

        public bool CoastLockoutActive { get; set; }

        public bool CircularizationAvailable { get; set; }
    }
}
