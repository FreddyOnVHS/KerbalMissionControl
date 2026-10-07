namespace KMC.Engine.Navigation
{
    /// <summary>
    /// Diagnostics emitted by the coupled finite-SOI optimizer while it runs
    /// in shadow mode. Nothing in this object is maneuver authority.
    /// </summary>
    public sealed class CoupledFiniteSoiShadowResult
    {
        internal CoupledFiniteSoiShadowResult()
        {
        }

        public LambertParkingOrbitEjectionSolution BootstrapEjection { get; internal set; }
        public LambertParkingOrbitEjectionSolution FinalEjection { get; internal set; }
        public FiniteSoiDepartureAssessment SourceAssessment { get; internal set; }
        public TargetSoiShootingAssessment TargetAssessment { get; internal set; }
        public bool FeasibilityPassSucceeded { get; internal set; }
        public bool BPlaneInitializationApplied { get; internal set; }
        public double BootstrapDeltaVMetersPerSecond { get; internal set; }
        public double FinalDeltaVMetersPerSecond { get; internal set; }
        public double SourceInterfacePositionErrorMeters { get; internal set; }
        public double SourceInterfaceVelocityErrorMetersPerSecond { get; internal set; }
        public double SplitVelocityMismatchMetersPerSecond { get; internal set; }
        public double OptimizedArrivalUniversalTimeSeconds { get; internal set; }
        public double TargetInterfaceErrorMeters { get; internal set; }
        public double TargetPeriapsisErrorMeters { get; internal set; }
        public double TargetBPlaneMagnitudeErrorMeters { get; internal set; }
        public double TargetBPlaneTErrorMeters { get; internal set; }
        public double TargetBPlaneRErrorMeters { get; internal set; }
        public int TerminalJacobianColumns { get; internal set; }
        public int TerminalRejectedSteps { get; internal set; }
        public double TerminalTrustRadius { get; internal set; }
        public bool SourceOutbound { get; internal set; }
        public bool TargetInbound { get; internal set; }
        public int Iterations { get; internal set; }
        public int Evaluations { get; internal set; }
        public string Stage { get; internal set; }
    }
}
