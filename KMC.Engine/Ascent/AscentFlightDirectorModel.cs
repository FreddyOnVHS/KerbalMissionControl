namespace KMC.Engine.Ascent
{
    /// <summary>
    /// Engine-owned advisory flight-director result for powered ascent.
    ///
    /// This is guidance information only. It never commands the vehicle.
    /// </summary>
    public sealed class AscentFlightDirectorModel
    {
        public bool Available { get; internal set; }

        public string FlightPhase { get; internal set; } =
            "UNKNOWN";

        public double NominalPitchDegrees { get; internal set; }

        public double RecommendedPitchDegrees { get; internal set; }

        public double PitchCorrectionDegrees { get; internal set; }

        public double AltitudeErrorMeters { get; internal set; }

        public double ApoapsisErrorMeters { get; internal set; }

        public double RecoveryAuthorityPercent { get; internal set; }

        public bool IsTargetAchievable { get; internal set; }

        public double ThrottleCommandPercent { get; internal set; }

        public string Command { get; internal set; } =
            "HOLD ATTITUDE";

        public string ThrottleCommand { get; internal set; } =
            "THROTTLE HOLD";

        public string Status { get; internal set; } =
            "GUIDANCE WAITING";

        public string NextEvent { get; internal set; } =
            "---";

        public int MecoCountdownSeconds { get; internal set; }

        public bool CutoffRequired { get; internal set; }

        public bool CoastLockoutActive { get; internal set; }

        public bool OrbitHandoffRequired { get; internal set; }

        public bool FlashAlert { get; internal set; }

        public bool PredictiveGuidanceBlended { get; internal set; }

        public double PredictiveBlendFraction { get; internal set; }
    }
}
