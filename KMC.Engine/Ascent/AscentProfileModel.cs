namespace KMC.Engine.Ascent
{
    public enum AscentProfileScaleSource
    {
        Unknown = 0,
        CapturedLaunchTwr,
        LiveTwrFallback,
        DefaultTwrFallback
    }

    /// <summary>
    /// Engine-owned reference-ascent profile at the vessel's current
    /// downrange position.
    ///
    /// Error sign convention:
    /// positive altitude error = vehicle is above the reference curve.
    /// positive pitch error    = vehicle is pitched above the reference.
    /// </summary>
    public sealed class AscentProfileModel
    {
        public bool Available { get; internal set; }

        public double TargetApoapsisMeters { get; internal set; }

        public bool LaunchPlanCaptured { get; internal set; }

        public bool CaptureOccurredThisUpdate { get; internal set; }

        public int InitialStage { get; internal set; }

        public bool PlanningThrustToWeightRatioKnown
        {
            get;
            internal set;
        }

        public double PlanningThrustToWeightRatio
        {
            get;
            internal set;
        }

        public double LiveThrustToWeightRatio { get; internal set; }

        public AscentProfileScaleSource ScaleSource
        {
            get;
            internal set;
        }

        public double ProfileScaleMeters { get; internal set; }

        public double DownrangeMeters { get; internal set; }

        public double TargetAltitudeMeters { get; internal set; }

        public double ActualAltitudeMeters { get; internal set; }

        public double AltitudeErrorMeters { get; internal set; }

        public double TargetPitchDegrees { get; internal set; }

        public double ActualPitchDegrees { get; internal set; }

        public double PitchErrorDegrees { get; internal set; }
    }
}
