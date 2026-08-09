namespace KMC.Engine.Ascent
{
    /// <summary>
    /// Engine-owned ascent cutoff trend/estimate.
    ///
    /// This is deliberately limited to the powered-ascent MECO boundary.
    /// Circularization and orbital insertion are not part of this model.
    /// </summary>
    public sealed class AscentCutoffModel
    {
        public bool Available { get; internal set; }

        public double TargetApoapsisMeters { get; internal set; }

        public double CutoffToleranceMeters { get; internal set; }

        public double CutoffThresholdMeters { get; internal set; }

        public bool ApoapsisRiseRateAvailable { get; internal set; }

        public double ApoapsisRiseRateMetersPerSecond { get; internal set; }

        public bool EstimatedMecoAvailable { get; internal set; }

        public double EstimatedMecoSeconds { get; internal set; }

        public bool CutoffReached { get; internal set; }
    }
}
