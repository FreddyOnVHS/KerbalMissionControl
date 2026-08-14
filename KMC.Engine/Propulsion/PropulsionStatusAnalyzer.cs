using System;
using KMC.Engine.SpacecraftSystems;

namespace KMC.Engine.Propulsion
{
    internal static class PropulsionStatusAnalyzer
    {
        public static PropulsionStatusModel Analyze(
            PropulsionTopologyModel topology,
            PropulsionLiveStateModel live,
            PropulsionFeedModel feed)
        {
            return
                Analyze(
                    topology,
                    live,
                    feed,
                    null);
        }

        public static PropulsionStatusModel Analyze(
            PropulsionTopologyModel topology,
            PropulsionLiveStateModel live,
            PropulsionFeedModel feed,
            FailureSimulationSnapshot failures)
        {
            PropulsionStatusModel status =
                new PropulsionStatusModel
                {
                    FeedObservability =
                        PropulsionFeedObservability.TopologySnapshot,

                    LivePropellantQuantityAvailable =
                        false
                };

            if (topology == null ||
                !topology.Available)
            {
                status.Severity =
                    PropulsionSeverity.Unknown;

                status.Condition =
                    PropulsionCondition.Unavailable;

                status.Summary =
                    "Propulsion topology is unavailable.";

                status.StageSummary =
                    "Next-stage propulsion consequence is unavailable.";

                return status;
            }

            status.Available =
                true;

            status.InstalledEngineCount =
                topology.EngineCount;

            if (live != null)
            {
                status.LiveEngineTelemetryAvailable =
                    live.TelemetryFresh &&
                    live.MatchedEngineCount > 0;

                status.LiveEngineCoverageComplete =
                    live.CoverageComplete;

                status.ReadyEngineCount =
                    live.ReadyEngineCount;

                status.ProducingEngineCount =
                    live.ProducingEngineCount;

                status.FlameoutEngineCount =
                    live.FlameoutEngineCount;

                status.CurrentThrustKnown =
                    live.CurrentThrustKnown;

                status.CurrentThrust =
                    live.CurrentThrust;

                status.AvailableThrustKnown =
                    live.AvailableThrustKnown;

                status.AvailableThrust =
                    live.AvailableThrust;

                status.LiveCurrentStage =
                    live.FlightSummaryAvailable
                        ? live.LiveCurrentStage
                        : topology.TopologyCurrentStage;
            }
            else
            {
                status.LiveCurrentStage =
                    topology.TopologyCurrentStage;
            }

            status.NextStage =
                topology.TopologyNextStage;

            if (feed != null &&
                feed.Available)
            {
                status.ReadyFeedLimitedEngineCount =
                    feed.ReadyEngineFeedLimitedCount;

                status.FeedLimitedEngineCount =
                    feed.CurrentFeedLimitedEngineCount;

                status.FeedDegradedEngineCount =
                    feed.CurrentFeedDegradedEngineCount;

                status.SyntheticFeedPressureDegraded =
                    feed.SyntheticPumpPressureDegraded;

                status.SyntheticFeedFlowLost =
                    feed.SyntheticPumpFlowLost;

                status.ExactFeedPathDegradedEngineCount =
                    feed.SyntheticExactFeedPathDegradedEngineCount;

                status.ExactFeedPathLostEngineCount =
                    feed.SyntheticExactFeedPathLostEngineCount;

                status.NextStageEngineLossCount =
                    feed.NextStageLostEngineCount;

                status.NextStageRetainedEngineCount =
                    feed.NextStageRetainedEngineCount;

                status.NextStageRetainedFeedAvailableCount =
                    feed.NextStageRetainedFeedAvailableCount;

                status.NextStageRetainedFeedLimitedCount =
                    feed.NextStageRetainedFeedLimitedCount;

                status.NextStageHasFeedRisk =
                    feed.NextStageRetainedFeedLimitedCount > 0 ||
                    feed.NextStageRetainedFeedDegradedCount > 0;

                status.NextStageEndsPropulsion =
                    topology.EngineCount > 0 &&
                    feed.NextStageRetainedEngineCount == 0;

                CountProducingFeedConflicts(
                    feed,
                    status);
            }

            status.NextStageActiveEngineLossCount =
                CountActiveEngineLosses(
                    feed);

            BuildEngineChannels(
                topology,
                live,
                feed,
                failures,
                status);

            SelectPrimaryCondition(
                topology,
                live,
                feed,
                status);

            BuildStageSummary(
                status);

            return status;
        }

        private static void BuildEngineChannels(
            PropulsionTopologyModel topology,
            PropulsionLiveStateModel live,
            PropulsionFeedModel feed,
            FailureSimulationSnapshot failures,
            PropulsionStatusModel status)
        {
            if (topology == null ||
                status == null)
            {
                return;
            }

            for (int index = 0;
                 index < topology.Engines.Count;
                 index++)
            {
                PropulsionEngineModel topologyEngine =
                    topology.Engines[index];

                if (topologyEngine == null)
                {
                    continue;
                }

                PropulsionEngineLiveStateModel liveEngine =
                    FindLiveEngine(
                        live,
                        topologyEngine.PartId);

                PropulsionEngineFeedModel feedEngine =
                    FindFeedEngine(
                        feed,
                        topologyEngine.PartId);

                PropulsionEngineChannelModel channel =
                    new PropulsionEngineChannelModel
                    {
                        PartId =
                            topologyEngine.PartId,

                        PartTitle =
                            topologyEngine.PartTitle ??
                            string.Empty,

                        ActivationStage =
                            topologyEngine.ActivationStage,

                        SeparationStage =
                            topologyEngine.SeparationStage,

                        SurvivesNextStage =
                            topologyEngine.SurvivesNextStage,

                        StartInhibited =
                            IsExactStartInhibitActive(
                                failures,
                                topologyEngine.PartId),

                        ThrustDegraded =
                            IsExactThrustFailureActive(
                                failures,
                                topologyEngine.PartId,
                                SyntheticFailureKind.Degrading),

                        ThrustUnstable =
                            IsExactThrustFailureActive(
                                failures,
                                topologyEngine.PartId,
                                SyntheticFailureKind.Intermittent)
                    };

                if (liveEngine != null)
                {
                    channel.LiveStateKnown =
                        live != null &&
                        live.TelemetryFresh &&
                        liveEngine.TelemetryMatched;

                    channel.OperatingState =
                        channel.LiveStateKnown
                            ? liveEngine.OperatingState
                            : PropulsionEngineOperatingState.Unknown;

                    channel.ReadyForThrust =
                        channel.LiveStateKnown &&
                        liveEngine.ReadyForThrust;

                    channel.FutureStage =
                        channel.LiveStateKnown &&
                        liveEngine.IsFutureStage;

                    channel.CurrentThrustKnown =
                        channel.LiveStateKnown;

                    channel.CurrentThrust =
                        channel.CurrentThrustKnown
                            ? liveEngine.CurrentThrust
                            : 0.0;

                    channel.MaximumThrustKnown =
                        channel.LiveStateKnown;

                    channel.MaximumThrust =
                        channel.MaximumThrustKnown
                            ? liveEngine.MaximumThrust
                            : 0.0;
                }

                if (feedEngine != null &&
                    feed != null &&
                    feed.Available)
                {
                    channel.FeedStateKnown =
                        true;

                    channel.CurrentFeedStatus =
                        feedEngine.CurrentFeedStatus;

                    channel.NextStageFeedStatus =
                        feedEngine.NextStageFeedStatus;
                }

                ClassifyChannel(
                    channel);

                status.EngineChannels.Add(
                    channel);

                if (channel.StartInhibited)
                {
                    status.StartInhibitedEngineCount++;
                }

                if (channel.ThrustDegraded)
                {
                    status.ThrustDegradedEngineCount++;
                }

                if (channel.ThrustUnstable)
                {
                    status.ThrustUnstableEngineCount++;
                }

                if (channel.Severity ==
                        PropulsionSeverity.Warning ||
                    channel.Severity ==
                        PropulsionSeverity.Critical)
                {
                    status.ChannelFaultCount++;
                }
                else if (channel.Severity ==
                         PropulsionSeverity.Advisory)
                {
                    status.ChannelAdvisoryCount++;
                }
                else if (channel.Severity ==
                         PropulsionSeverity.Normal)
                {
                    status.ChannelNormalCount++;
                }
                else
                {
                    status.ChannelUnknownCount++;
                }
            }
        }

        private static void ClassifyChannel(
            PropulsionEngineChannelModel channel)
        {
            if (channel == null)
            {
                return;
            }

            if (!channel.LiveStateKnown)
            {
                channel.Condition =
                    PropulsionEngineChannelCondition.Unknown;

                channel.Severity =
                    PropulsionSeverity.Unknown;

                return;
            }

            if (channel.StartInhibited)
            {
                channel.Condition =
                    PropulsionEngineChannelCondition.StartInhibit;

                channel.Severity =
                    PropulsionSeverity.Advisory;

                return;
            }

            if (channel.ThrustUnstable)
            {
                channel.Condition =
                    PropulsionEngineChannelCondition.ThrustUnstable;

                channel.Severity =
                    PropulsionSeverity.Advisory;

                return;
            }

            if (channel.ThrustDegraded)
            {
                channel.Condition =
                    PropulsionEngineChannelCondition.ThrustDegraded;

                channel.Severity =
                    PropulsionSeverity.Advisory;

                return;
            }

            if (channel.OperatingState ==
                    PropulsionEngineOperatingState.Flameout)
            {
                channel.Condition =
                    PropulsionEngineChannelCondition.Flameout;

                channel.Severity =
                    PropulsionSeverity.Warning;

                return;
            }

            if (channel.OperatingState ==
                    PropulsionEngineOperatingState.Producing)
            {
                if (channel.FeedStateKnown &&
                    channel.CurrentFeedStatus ==
                        PropulsionFeedStatus.PressureLow)
                {
                    channel.Condition =
                        PropulsionEngineChannelCondition.FeedDegraded;

                    channel.Severity =
                        PropulsionSeverity.Advisory;
                }
                else if (channel.FeedStateKnown &&
                         channel.CurrentFeedStatus !=
                            PropulsionFeedStatus.Available)
                {
                    /*
                     * Direct live thrust is stronger current evidence than the
                     * topology-snapshot feed state. Report the conflict rather
                     * than declaring the producing engine starved.
                     */
                    channel.Condition =
                        PropulsionEngineChannelCondition.FeedStateConflict;

                    channel.Severity =
                        PropulsionSeverity.Advisory;
                }
                else
                {
                    channel.Condition =
                        PropulsionEngineChannelCondition.Producing;

                    channel.Severity =
                        PropulsionSeverity.Normal;
                }

                return;
            }

            if (channel.ReadyForThrust)
            {
                if (channel.FeedStateKnown &&
                    channel.CurrentFeedStatus ==
                        PropulsionFeedStatus.PressureLow)
                {
                    channel.Condition =
                        PropulsionEngineChannelCondition.FeedDegraded;

                    channel.Severity =
                        PropulsionSeverity.Advisory;
                }
                else if (channel.FeedStateKnown &&
                         channel.CurrentFeedStatus !=
                            PropulsionFeedStatus.Available)
                {
                    channel.Condition =
                        PropulsionEngineChannelCondition.FeedLimited;

                    channel.Severity =
                        PropulsionSeverity.Advisory;
                }
                else
                {
                    channel.Condition =
                        PropulsionEngineChannelCondition.Ready;

                    channel.Severity =
                        PropulsionSeverity.Normal;
                }

                return;
            }

            if (channel.FutureStage)
            {
                channel.Condition =
                    PropulsionEngineChannelCondition.FutureStage;

                channel.Severity =
                    PropulsionSeverity.Normal;

                return;
            }

            if (channel.OperatingState ==
                    PropulsionEngineOperatingState.Shutdown)
            {
                channel.Condition =
                    PropulsionEngineChannelCondition.Shutdown;

                channel.Severity =
                    PropulsionSeverity.Normal;

                return;
            }

            channel.Condition =
                PropulsionEngineChannelCondition.Standby;

            channel.Severity =
                PropulsionSeverity.Normal;
        }

        private static bool IsExactThrustFailureActive(
            FailureSimulationSnapshot failures,
            uint partId,
            SyntheticFailureKind kind)
        {
            if (failures == null ||
                failures.Failures == null ||
                partId == 0)
            {
                return false;
            }

            for (int index = 0;
                 index < failures.Failures.Count;
                 index++)
            {
                SyntheticFailureRecord failure =
                    failures.Failures[index];

                if (failure == null ||
                    !failure.EffectiveNow ||
                    failure.TargetKind !=
                        SyntheticFailureTargetKind.PropulsionEffect ||
                    failure.Kind !=
                        kind)
                {
                    continue;
                }

                uint targetPartId;
                bool shutdown;

                if (SyntheticFailureTargets.TryParsePropulsionTarget(
                        failure.TargetId,
                        out targetPartId,
                        out shutdown) &&
                    !shutdown &&
                    targetPartId == partId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsExactStartInhibitActive(
            FailureSimulationSnapshot failures,
            uint partId)
        {
            if (failures == null ||
                failures.Failures == null ||
                partId == 0)
            {
                return false;
            }

            for (int index = 0;
                 index < failures.Failures.Count;
                 index++)
            {
                SyntheticFailureRecord failure =
                    failures.Failures[index];

                if (failure == null ||
                    !failure.EffectiveNow ||
                    failure.TargetKind !=
                        SyntheticFailureTargetKind.Component)
                {
                    continue;
                }

                uint targetPartId;

                if (PropulsionEngineFailureTargets
                        .TryParseExactEngineStartInhibitTarget(
                            failure.TargetId,
                            out targetPartId) &&
                    targetPartId == partId)
                {
                    return true;
                }
            }

            return false;
        }

        private static PropulsionEngineLiveStateModel
            FindLiveEngine(
                PropulsionLiveStateModel live,
                uint partId)
        {
            if (live == null)
            {
                return null;
            }

            for (int index = 0;
                 index < live.Engines.Count;
                 index++)
            {
                PropulsionEngineLiveStateModel engine =
                    live.Engines[index];

                if (engine != null &&
                    engine.PartId == partId)
                {
                    return engine;
                }
            }

            return null;
        }

        private static PropulsionEngineFeedModel
            FindFeedEngine(
                PropulsionFeedModel feed,
                uint partId)
        {
            if (feed == null)
            {
                return null;
            }

            for (int index = 0;
                 index < feed.Engines.Count;
                 index++)
            {
                PropulsionEngineFeedModel engine =
                    feed.Engines[index];

                if (engine != null &&
                    engine.PartId == partId)
                {
                    return engine;
                }
            }

            return null;
        }

        private static void SelectPrimaryCondition(
            PropulsionTopologyModel topology,
            PropulsionLiveStateModel live,
            PropulsionFeedModel feed,
            PropulsionStatusModel status)
        {
            if (topology.EngineCount == 0)
            {
                status.Severity =
                    PropulsionSeverity.Normal;

                status.Condition =
                    PropulsionCondition.NoPropulsion;

                status.Summary =
                    "No propulsion engines are installed on the current vessel.";

                return;
            }

            if (live == null ||
                !live.TelemetryFresh ||
                live.MatchedEngineCount <= 0)
            {
                status.Severity =
                    PropulsionSeverity.Advisory;

                status.Condition =
                    PropulsionCondition.DataIncomplete;

                status.Summary =
                    "Propulsion structure is known, but live engine-state telemetry is unavailable.";

                return;
            }

            if (!live.CoverageComplete)
            {
                status.Severity =
                    PropulsionSeverity.Advisory;

                status.Condition =
                    PropulsionCondition.DataIncomplete;

                status.Summary =
                    "Live engine telemetry does not fully match the current propulsion topology.";

                return;
            }

            if (status.FlameoutEngineCount > 0)
            {
                bool allCurrentCapabilityLost =
                    status.ProducingEngineCount == 0 &&
                    status.ReadyEngineCount == 0;

                status.Severity =
                    allCurrentCapabilityLost
                        ? PropulsionSeverity.Critical
                        : PropulsionSeverity.Warning;

                status.Condition =
                    allCurrentCapabilityLost
                        ? PropulsionCondition.PropulsionLost
                        : PropulsionCondition.EngineFlameout;

                status.Summary =
                    allCurrentCapabilityLost
                        ? "Current propulsion capability is lost following engine flameout."
                        : status.FlameoutEngineCount +
                          " engine(s) report flameout.";

                return;
            }

            if (status.StartInhibitedEngineCount > 0)
            {
                status.Severity =
                    PropulsionSeverity.Advisory;

                status.Condition =
                    PropulsionCondition.EngineStartInhibited;

                status.Summary =
                    status.StartInhibitedEngineCount.ToString() +
                    " exact engine start channel(s) are inhibited.";

                return;
            }

            if (status.ThrustUnstableEngineCount > 0)
            {
                status.Severity =
                    PropulsionSeverity.Advisory;

                status.Condition =
                    PropulsionCondition.EngineThrustUnstable;

                status.Summary =
                    status.ThrustUnstableEngineCount.ToString() +
                    " exact engine thrust channel(s) are unstable.";

                return;
            }

            if (status.ThrustDegradedEngineCount > 0)
            {
                status.Severity =
                    PropulsionSeverity.Advisory;

                status.Condition =
                    PropulsionCondition.EngineThrustDegraded;

                status.Summary =
                    status.ThrustDegradedEngineCount.ToString() +
                    " exact engine thrust channel(s) are degraded.";

                return;
            }

            if (status.SyntheticFeedFlowLost)
            {
                status.Severity =
                    PropulsionSeverity.Warning;

                status.Condition =
                    PropulsionCondition.FeedFlowLost;

                status.Summary =
                    "Synthetic liquid-feed pressure is unavailable; pump-fed engine feed paths are not available.";

                return;
            }

            if (status.SyntheticFeedPressureDegraded)
            {
                status.Severity =
                    PropulsionSeverity.Advisory;

                status.Condition =
                    PropulsionCondition.FeedPressureDegraded;

                status.Summary =
                    "Synthetic liquid-feed redundancy is degraded; pump-fed engine pressure is low.";

                return;
            }

            if (status.ExactFeedPathLostEngineCount > 0)
            {
                status.Severity =
                    PropulsionSeverity.Advisory;

                status.Condition =
                    PropulsionCondition.EngineFeedPathLost;

                status.Summary =
                    status.ExactFeedPathLostEngineCount.ToString() +
                    " exact engine feed path(s) are unavailable.";

                return;
            }

            if (status.ExactFeedPathDegradedEngineCount > 0)
            {
                status.Severity =
                    PropulsionSeverity.Advisory;

                status.Condition =
                    PropulsionCondition.EngineFeedPathDegraded;

                status.Summary =
                    status.ExactFeedPathDegradedEngineCount.ToString() +
                    " exact engine feed path(s) report low pressure.";

                return;
            }

            /*
             * A producing engine together with snapshot feed evidence saying
             * its feed is unavailable is contradictory evidence. The live
             * engine state is stronger current evidence; call the mismatch out
             * instead of declaring the engine starved.
             */
            if (status.ProducingFeedConflictCount > 0)
            {
                status.Severity =
                    PropulsionSeverity.Advisory;

                status.Condition =
                    PropulsionCondition.FeedStateConflict;

                status.Summary =
                    "Live thrust is present while topology-snapshot feed evidence reports a limitation.";

                return;
            }

            /*
             * Feed amount / FlowEnabled state is topology-snapshot evidence.
             * Do not promote a snapshot-only limitation to a live engine
             * failure without direct engine evidence.
             */
            if (status.ReadyFeedLimitedEngineCount > 0 ||
                status.FeedLimitedEngineCount > 0)
            {
                status.Severity =
                    PropulsionSeverity.Advisory;

                status.Condition =
                    PropulsionCondition.FeedEvidenceLimited;

                status.Summary =
                    "Topology-snapshot propellant feed evidence reports one or more limited engine feeds.";

                return;
            }

            if (status.NextStageHasFeedRisk)
            {
                status.Severity =
                    PropulsionSeverity.Warning;

                status.Condition =
                    PropulsionCondition.NextStageFeedRisk;

                status.Summary =
                    "Next stage retains propulsion engine(s) without complete retained propellant feed.";

                return;
            }

            if (status.NextStageEndsPropulsion)
            {
                status.Severity =
                    PropulsionSeverity.Advisory;

                status.Condition =
                    PropulsionCondition.NextStagePropulsionTerminated;

                status.Summary =
                    "Next stage removes all currently installed propulsion engines.";

                return;
            }

            if (status.NextStageEngineLossCount > 0)
            {
                status.Severity =
                    PropulsionSeverity.Normal;

                status.Condition =
                    PropulsionCondition.NextStageEngineSeparation;

                status.Summary =
                    "Propulsion system is nominal; next stage separates " +
                    status.NextStageEngineLossCount +
                    " engine(s).";

                return;
            }

            if (status.ProducingEngineCount > 0 ||
                status.ReadyEngineCount > 0)
            {
                status.Severity =
                    PropulsionSeverity.Normal;

                status.Condition =
                    PropulsionCondition.Nominal;

                status.Summary =
                    "Propulsion system nominal.";

                return;
            }

            status.Severity =
                PropulsionSeverity.Normal;

            status.Condition =
                PropulsionCondition.Standby;

            status.Summary =
                "Propulsion system is in standby; no current-stage engine is producing thrust.";
        }

        private static int CountActiveEngineLosses(
            PropulsionFeedModel feed)
        {
            if (feed == null ||
                !feed.Available)
            {
                return 0;
            }

            int count = 0;

            for (int index = 0;
                 index < feed.Engines.Count;
                 index++)
            {
                PropulsionEngineFeedModel engine =
                    feed.Engines[index];

                if (!engine.SurvivesNextStage &&
                    engine.LiveStateKnown &&
                    engine.OperatingState ==
                        PropulsionEngineOperatingState.Producing)
                {
                    count++;
                }
            }

            return count;
        }

        private static void CountProducingFeedConflicts(
            PropulsionFeedModel feed,
            PropulsionStatusModel status)
        {
            if (feed == null ||
                !feed.Available)
            {
                return;
            }

            int count = 0;

            for (int index = 0;
                 index < feed.Engines.Count;
                 index++)
            {
                PropulsionEngineFeedModel engine =
                    feed.Engines[index];

                if (engine.LiveStateKnown &&
                    engine.OperatingState ==
                        PropulsionEngineOperatingState.Producing &&
                    engine.CurrentFeedStatus !=
                        PropulsionFeedStatus.Available &&
                    engine.CurrentFeedStatus !=
                        PropulsionFeedStatus.PressureLow)
                {
                    count++;
                }
            }

            status.ProducingFeedConflictCount =
                count;
        }

        private static void BuildStageSummary(
            PropulsionStatusModel status)
        {
            if (!status.Available)
            {
                return;
            }

            if (status.NextStageHasFeedRisk)
            {
                status.StageSummary =
                    "Stage " +
                    status.NextStage +
                    " retains " +
                    status.NextStageRetainedEngineCount +
                    " engine(s), with " +
                    status.NextStageRetainedFeedLimitedCount +
                    " retained engine feed(s) incomplete.";
                return;
            }

            if (status.NextStageEndsPropulsion)
            {
                status.StageSummary =
                    "Stage " +
                    status.NextStage +
                    " removes all " +
                    status.NextStageEngineLossCount +
                    " installed propulsion engine(s).";
                return;
            }

            if (status.NextStageEngineLossCount > 0)
            {
                status.StageSummary =
                    "Stage " +
                    status.NextStage +
                    " separates " +
                    status.NextStageEngineLossCount +
                    " engine(s), including " +
                    status.NextStageActiveEngineLossCount +
                    " currently producing engine(s); " +
                    status.NextStageRetainedEngineCount +
                    " engine(s) remain.";
                return;
            }

            status.StageSummary =
                "Stage " +
                status.NextStage +
                " retains all current propulsion engines and their analyzed feed paths.";
        }
    }
}
