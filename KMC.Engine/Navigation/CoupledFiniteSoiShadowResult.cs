namespace KMC.Engine.Navigation
{
    /// <summary>
    /// Production result emitted by the coupled finite-SOI optimizer.
    /// This object carries both the solved ejection and the diagnostics used by
    /// Mission Control to decide whether the result is allowed maneuver authority.
    /// </summary>
    public class CoupledFiniteSoiResult
    {
        internal CoupledFiniteSoiResult()
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

    /// <summary>
    /// Compatibility type retained for the pre-production shadow API. New code
    /// should consume CoupledFiniteSoiResult through CoupledFiniteSoiOptimizer.TrySolve.
    /// </summary>
    public sealed class CoupledFiniteSoiShadowResult : CoupledFiniteSoiResult
    {
        internal CoupledFiniteSoiShadowResult()
        {
        }
    }
}
