using System;
using System.Collections.Generic;

namespace KMC.Engine.Propulsion
{

    /// <summary>
    /// Build 14.12.5 exact-engine start-channel failure identity.
    /// This is synthetic spacecraft truth. Mission Control may translate the
    /// consequence into the already-validated real KSP engine-shutdown effect.
    /// </summary>
    public static class PropulsionEngineFailureTargets
    {
        public const string ExactEngineStartInhibitPrefix =
            "PROP START / PART ";

        public static string CreateExactEngineStartInhibitTarget(
            uint partId)
        {
            return
                partId == 0
                    ? string.Empty
                    : ExactEngineStartInhibitPrefix +
                      partId.ToString();
        }

        public const string ExactEngineThrustIndicationPrefix =
            "PROP THRUST IND / PART ";

        public static string CreateExactEngineThrustIndicationTarget(
            uint partId)
        {
            return
                partId == 0
                    ? string.Empty
                    : ExactEngineThrustIndicationPrefix +
                      partId.ToString();
        }

        public static bool TryParseExactEngineThrustIndicationTarget(
            string targetId,
            out uint partId)
        {
            partId = 0;

            if (string.IsNullOrWhiteSpace(targetId) ||
                !targetId.StartsWith(
                    ExactEngineThrustIndicationPrefix,
                    StringComparison.Ordinal))
            {
                return false;
            }

            return
                uint.TryParse(
                    targetId.Substring(
                        ExactEngineThrustIndicationPrefix.Length),
                    out partId) &&
                partId != 0;
        }

        public static bool TryParseExactEngineStartInhibitTarget(
            string targetId,
            out uint partId)
        {
            partId = 0;

            if (string.IsNullOrWhiteSpace(targetId) ||
                !targetId.StartsWith(
                    ExactEngineStartInhibitPrefix,
                    StringComparison.Ordinal))
            {
                return false;
            }

            return
                uint.TryParse(
                    targetId.Substring(
                        ExactEngineStartInhibitPrefix.Length),
                    out partId) &&
                partId != 0;
        }
    }

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
        FeedPressureDegraded,
        FeedFlowLost,
        EngineFeedPathDegraded,
        EngineFeedPathLost,
        EngineStartInhibited,
        EngineThrustDegraded,
        EngineThrustUnstable,
        EngineThrustIndicationFault,
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
    /// Build 14.12.1 per-engine engineering channel classification.
    ///
    /// This combines already-validated topology, live engine state and
    /// feed-analysis evidence without inventing any new KSP telemetry.
    /// </summary>
    public enum PropulsionEngineChannelCondition
    {
        Unknown = 0,
        Standby,
        FutureStage,
        Ready,
        Producing,
        Shutdown,
        Flameout,
        FeedDegraded,
        FeedLimited,
        StartInhibit,
        ThrustDegraded,
        ThrustUnstable,
        ThrustIndicationFault,
        FeedStateConflict
    }

    public sealed class PropulsionEngineChannelModel
    {
        public PropulsionEngineChannelModel()
        {
            PartTitle = string.Empty;
            Condition = PropulsionEngineChannelCondition.Unknown;
            Severity = PropulsionSeverity.Unknown;
            OperatingState = PropulsionEngineOperatingState.Unknown;
            CurrentFeedStatus = PropulsionFeedStatus.Unknown;
            NextStageFeedStatus = PropulsionFeedStatus.Unknown;
        }

        public uint PartId { get; internal set; }

        public string PartTitle { get; internal set; }

        public int ActivationStage { get; internal set; }

        public int SeparationStage { get; internal set; }

        public bool SurvivesNextStage { get; internal set; }

        public bool LiveStateKnown { get; internal set; }

        public PropulsionEngineOperatingState OperatingState
        {
            get;
            internal set;
        }

        public bool ReadyForThrust { get; internal set; }

        public bool FutureStage { get; internal set; }

        public bool FeedStateKnown { get; internal set; }

        public bool StartInhibited { get; internal set; }

        public bool ThrustDegraded { get; internal set; }

        public bool ThrustUnstable { get; internal set; }

        public bool ThrustIndicationFailed { get; internal set; }

        public PropulsionFeedStatus CurrentFeedStatus
        {
            get;
            internal set;
        }

        public PropulsionFeedStatus NextStageFeedStatus
        {
            get;
            internal set;
        }

        public bool CurrentThrustKnown { get; internal set; }

        public double CurrentThrust { get; internal set; }

        public bool MaximumThrustKnown { get; internal set; }

        public double MaximumThrust { get; internal set; }

        public PropulsionEngineChannelCondition Condition
        {
            get;
            internal set;
        }

        public PropulsionSeverity Severity
        {
            get;
            internal set;
        }
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

            EngineChannels =
                new List<PropulsionEngineChannelModel>();
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
        /// False in the current architecture. Source amount/capacity is
        /// topology-snapshot evidence, not a continuous live tank stream.
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

        public int FeedDegradedEngineCount { get; internal set; }

        public bool SyntheticFeedPressureDegraded { get; internal set; }

        public bool SyntheticFeedFlowLost { get; internal set; }

        public int ExactFeedPathDegradedEngineCount
        {
            get;
            internal set;
        }

        public int ExactFeedPathLostEngineCount
        {
            get;
            internal set;
        }

        public int StartInhibitedEngineCount { get; internal set; }

        public int ThrustDegradedEngineCount { get; internal set; }

        public int ThrustUnstableEngineCount { get; internal set; }

        public int ThrustIndicationFaultEngineCount { get; internal set; }

        public int ProducingFeedConflictCount { get; internal set; }

        public int ChannelFaultCount { get; internal set; }

        public int ChannelAdvisoryCount { get; internal set; }

        public int ChannelNormalCount { get; internal set; }

        public int ChannelUnknownCount { get; internal set; }

        public List<PropulsionEngineChannelModel> EngineChannels
        {
            get;
            private set;
        }

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
