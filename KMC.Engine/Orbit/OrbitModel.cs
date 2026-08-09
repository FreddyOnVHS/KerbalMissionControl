namespace KMC.Engine.Orbit
{
    /// <summary>
    /// Build 10.0 Engine-owned ORBIT foundation.
    ///
    /// Guidance/prediction models are intentionally added in later milestones.
    /// </summary>
    public sealed class OrbitModel
    {
        public OrbitModel()
        {
            Current =
                new OrbitTelemetryState();
        }

        public bool Available { get; internal set; }

        public bool ResetOccurredThisUpdate { get; internal set; }

        public OrbitTelemetryState Current { get; internal set; }

        public double TargetOrbitMeters { get; internal set; }

        public bool TargetInheritedFromAscent { get; internal set; }

        public bool AscentHandoffObserved { get; internal set; }

        public bool IsAboveAtmosphere { get; internal set; }

        public bool LivePeriapsisAboveAtmosphere { get; internal set; }

        internal static OrbitModel Clone(
            OrbitModel source)
        {
            OrbitModel clone =
                new OrbitModel();

            if (source == null)
            {
                return clone;
            }

            clone.Available =
                source.Available;

            clone.ResetOccurredThisUpdate =
                source.ResetOccurredThisUpdate;

            clone.Current =
                OrbitTelemetryState.Clone(
                    source.Current);

            clone.TargetOrbitMeters =
                source.TargetOrbitMeters;

            clone.TargetInheritedFromAscent =
                source.TargetInheritedFromAscent;

            clone.AscentHandoffObserved =
                source.AscentHandoffObserved;

            clone.IsAboveAtmosphere =
                source.IsAboveAtmosphere;

            clone.LivePeriapsisAboveAtmosphere =
                source.LivePeriapsisAboveAtmosphere;

            return clone;
        }
    }
}
