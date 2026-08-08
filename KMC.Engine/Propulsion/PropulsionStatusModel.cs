namespace KMC.Engine.Propulsion
{
    public enum PropulsionSeverity
    {
        Unknown = 0,
        Normal,
        Advisory,
        Warning,
        Critical
    }

    public enum PropulsionCondition
    {
        Unknown = 0,
        Unavailable,
        NoPropulsion,
        DataIncomplete,
        Standby,
        Nominal,
        FeedEvidenceLimited,
        FeedStateConflict,
        EngineFlameout,
        PropulsionLost,
        NextStageEngineSeparation,
        NextStagePropulsionTerminated,
        NextStageFeedRisk
    }

    public enum PropulsionFeedObservability
    {
        Unknown = 0,

        /// <summary>
        /// Feed connectivity and source quantities/flow flags come from the
        /// latest vessel topology snapshot. They are not continuous live
        /// propellant telemetry.
        /// </summary>
        TopologySnapshot
    }

    /// <summary>
    /// Controller-level propulsion interpretation built from validated
    /// topology, live engine state, and feed/stage analysis.
    ///
    /// It intentionally distinguishes direct live engine evidence from
    /// topology-snapshot propellant evidence.
    /// </summary>
    public sealed class PropulsionStatusModel
    {
        public PropulsionStatusModel()
        {
            Summary =
                string.Empty;

            StageSummary =
                string.Empty;
        }

        public bool Available { get; internal set; }

        public PropulsionSeverity Severity { get; internal set; }

        public PropulsionCondition Condition { get; internal set; }

        public string Summary { get; internal set; }

        public string StageSummary { get; internal set; }

        public PropulsionFeedObservability FeedObservability
        {
            get;
            internal set;
        }

        /// <summary>
        /// False in Build 8.15. Source amount/capacity is topology-snapshot
        /// evidence, not a continuous live tank-quantity stream.
        /// </summary>
        public bool LivePropellantQuantityAvailable
        {
            get;
            internal set;
        }

        public bool LiveEngineTelemetryAvailable { get; internal set; }

        public bool LiveEngineCoverageComplete { get; internal set; }

        public int InstalledEngineCount { get; internal set; }

        public int ReadyEngineCount { get; internal set; }

        public int ProducingEngineCount { get; internal set; }

        public int FlameoutEngineCount { get; internal set; }

        public int ReadyFeedLimitedEngineCount { get; internal set; }

        public int FeedLimitedEngineCount { get; internal set; }

        public int ProducingFeedConflictCount { get; internal set; }

        public bool CurrentThrustKnown { get; internal set; }

        public double CurrentThrust { get; internal set; }

        public bool AvailableThrustKnown { get; internal set; }

        public double AvailableThrust { get; internal set; }

        public int LiveCurrentStage { get; internal set; }

        public int NextStage { get; internal set; }

        public int NextStageEngineLossCount { get; internal set; }

        public int NextStageActiveEngineLossCount { get; internal set; }

        public int NextStageRetainedEngineCount { get; internal set; }

        public int NextStageRetainedFeedAvailableCount
        {
            get;
            internal set;
        }

        public int NextStageRetainedFeedLimitedCount
        {
            get;
            internal set;
        }

        public bool NextStageEndsPropulsion { get; internal set; }

        public bool NextStageHasFeedRisk { get; internal set; }

        public bool ActionRequired
        {
            get
            {
                return
                    Severity ==
                        PropulsionSeverity.Warning ||
                    Severity ==
                        PropulsionSeverity.Critical;
            }
        }
    }
}
