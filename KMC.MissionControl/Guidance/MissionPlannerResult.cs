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

        public bool PoweredGuidanceAvailable { get; set; }

        public double PoweredGuidancePitchDegrees { get; set; }

        public double PoweredPredictedApoapsisMeters { get; set; }

        public double PoweredPredictedPeriapsisMeters { get; set; }

        public double PoweredOrbitErrorMeters { get; set; }

        public double PoweredGuidanceConfidencePercent { get; set; }

        public double PoweredGuidanceBurnSeconds { get; set; }

        public double PoweredGuidanceCoastSeconds { get; set; }

        public double PoweredPredictionConvergenceMeters { get; set; }

        public bool PoweredTargetCutoffReached { get; set; }

        public string PoweredGuidanceMode { get; set; }

        public double ThrottleCommandPercent { get; set; }

        /*
         * CircularizationDeltaV now represents remaining delta-v.
         */
        public double CircularizationDeltaV { get; set; }

        public double CircularizationBurnTimeSeconds { get; set; }

        public double CircularizationIgnitionInSeconds { get; set; }

        public double CircularizationPeriapsisErrorMeters { get; set; }

        public double CurrentSpecificOrbitalEnergy { get; set; }

        public double TargetSpecificOrbitalEnergy { get; set; }

        public double OrbitalEnergyError { get; set; }

        public string OrbitSafetyReason { get; set; }

        public bool OrbitSafetyAchieved { get; set; }

        public bool OrbitSafetyPauseBurn { get; set; }

        public bool ActualPeriapsisSafe { get; set; }

        public bool PredictedPeriapsisSafe { get; set; }

        public bool OrbitEnergySatisfied { get; set; }

        public bool OrbitDeltaVSatisfied { get; set; }

        public double OrbitSafetyDecisionTime { get; set; }

        public bool PeriapsisRecoveryActive { get; set; }

        public double PeriapsisRecoveryErrorMeters { get; set; }

        public double PeriapsisRecoveryThrottlePercent { get; set; }

        public double PeriapsisRecoveryDesiredThrottlePercent { get; set; }

        public double PeriapsisRecoveryCommandAgeSeconds { get; set; }

        public bool PeriapsisRecoveryCommandHeld { get; set; }

        public bool PeriapsisRecoveryCutoff { get; set; }

        public string PeriapsisRecoveryReason { get; set; }

        public double InitialCircularizationDeltaV { get; set; }

        public double BurnCompletionPercent { get; set; }

        public double PredictedShutdownApoapsisMeters { get; set; }

        public double PredictedShutdownPeriapsisMeters { get; set; }

        public double PredictedOrbitErrorMeters { get; set; }

        public double CircularizationPitchDegrees { get; set; }

        public int MecoCountdownSeconds { get; set; }

        public bool FlashAlert { get; set; }

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
