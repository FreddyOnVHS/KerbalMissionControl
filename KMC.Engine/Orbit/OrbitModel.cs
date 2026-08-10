namespace KMC.Engine.Orbit
{
    /// <summary>
    /// Engine-owned ORBIT state.
    /// </summary>
    public sealed class OrbitModel
    {
        public OrbitModel()
        {
            Current =
                new OrbitTelemetryState();

            CircularizationPrediction =
                new CircularizationPredictionModel();

            VelocityVector =
                new VelocityVectorTelemetryModel();

            Safety =
                new OrbitSafetyModel();
        }

        public bool Available { get; internal set; }

        public bool ResetOccurredThisUpdate { get; internal set; }

        public OrbitTelemetryState Current { get; internal set; }

        public double TargetOrbitMeters { get; internal set; }

        public bool TargetInheritedFromAscent { get; internal set; }

        public bool AscentHandoffObserved { get; internal set; }

        public bool IsAboveAtmosphere { get; internal set; }

        public bool LivePeriapsisAboveAtmosphere { get; internal set; }

        public CircularizationPredictionModel CircularizationPrediction
        {
            get;
            internal set;
        }

        public VelocityVectorTelemetryModel VelocityVector
        {
            get;
            internal set;
        }

        public OrbitSafetyModel Safety
        {
            get;
            internal set;
        }

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

            clone.CircularizationPrediction =
                CircularizationPredictionModel.Clone(
                    source.CircularizationPrediction);

            clone.VelocityVector =
                VelocityVectorTelemetryModel.Clone(
                    source.VelocityVector);

            clone.Safety =
                OrbitSafetyModel.Clone(
                    source.Safety);

            return clone;
        }
    }
}
