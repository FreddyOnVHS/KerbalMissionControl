namespace KMC.MissionControl.Guidance
{
    /// <summary>
    /// Safety decision returned to MissionPlanner during circularization.
    /// </summary>
    public sealed class OrbitSafetyDecision
    {
        public bool OrbitAchieved { get; set; }

        public bool PauseBurn { get; set; }

        public bool ActualPeriapsisSafe { get; set; }

        public bool PredictedPeriapsisSafe { get; set; }

        public bool EnergySatisfied { get; set; }

        public bool DeltaVSatisfied { get; set; }

        public string Reason { get; set; }
    }
}
