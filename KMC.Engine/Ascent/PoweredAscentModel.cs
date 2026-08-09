namespace KMC.Engine.Ascent
{
    public enum AscentPoweredThrustEvidence
    {
        Unknown = 0,

        /// <summary>
        /// Current and available thrust are taken from the Engine-owned
        /// propulsion live-state model after freshness and topology-coverage
        /// checks pass.
        /// </summary>
        VerifiedPropulsionLiveState,

        /// <summary>
        /// PROP live-state evidence was incomplete or unavailable, so the
        /// legacy flight-packet CurrentThrust / MaximumThrust values are used.
        /// </summary>
        FlightPacketFallback
    }

    /// <summary>
    /// Engine-owned physics-based powered-ascent guidance/prediction result.
    ///
    /// This remains advisory only. It does not command the vehicle.
    /// </summary>
    public sealed class PoweredAscentModel
    {
        public bool Available { get; internal set; }

        public string Mode { get; internal set; } =
            "INACTIVE";

        public string InactiveReason { get; internal set; } =
            string.Empty;

        public double ReferencePitchDegrees { get; internal set; }

        public double RecommendedPitchDegrees { get; internal set; }

        public double PitchErrorDegrees { get; internal set; }

        public double PredictedApoapsisMeters { get; internal set; }

        public double PredictedPeriapsisMeters { get; internal set; }

        public double OrbitErrorMeters { get; internal set; }

        public double ConfidencePercent { get; internal set; }

        public double PoweredFlightSeconds { get; internal set; }

        public double CoastFlightSeconds { get; internal set; }

        public bool PredictionConvergenceKnown { get; internal set; }

        public double PredictionConvergenceMeters { get; internal set; }

        public bool TargetCutoffReached { get; internal set; }

        public AscentPoweredThrustEvidence ThrustEvidence
        {
            get;
            internal set;
        }

        public bool PropulsionTelemetryFresh { get; internal set; }

        public bool PropulsionCoverageComplete { get; internal set; }

        public bool CurrentThrustKnown { get; internal set; }

        public double CurrentThrustKilonewtons { get; internal set; }

        public bool AvailableThrustKnown { get; internal set; }

        public double AvailableThrustKilonewtons { get; internal set; }

        public double ThrottleCommand { get; internal set; }

        public double VesselMassTonnes { get; internal set; }

        public double SpecificImpulseSeconds { get; internal set; }
    }

    internal sealed class PoweredAscentThrustInput
    {
        public AscentPoweredThrustEvidence Evidence { get; set; }

        public bool PropulsionTelemetryFresh { get; set; }

        public bool PropulsionCoverageComplete { get; set; }

        public bool CurrentThrustKnown { get; set; }

        public double CurrentThrustKilonewtons { get; set; }

        public bool AvailableThrustKnown { get; set; }

        public double AvailableThrustKilonewtons { get; set; }

        public double ThrottleCommand { get; set; }
    }
}
