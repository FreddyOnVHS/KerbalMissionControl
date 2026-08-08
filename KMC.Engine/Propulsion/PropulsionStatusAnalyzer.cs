using System;

namespace KMC.Engine.Propulsion
{
    internal static class PropulsionStatusAnalyzer
    {
        public static PropulsionStatusModel Analyze(
            PropulsionTopologyModel topology,
            PropulsionLiveStateModel live,
            PropulsionFeedModel feed)
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

                status.NextStageEngineLossCount =
                    feed.NextStageLostEngineCount;

                status.NextStageRetainedEngineCount =
                    feed.NextStageRetainedEngineCount;

                status.NextStageRetainedFeedAvailableCount =
                    feed.NextStageRetainedFeedAvailableCount;

                status.NextStageRetainedFeedLimitedCount =
                    feed.NextStageRetainedFeedLimitedCount;

                status.NextStageHasFeedRisk =
                    feed.NextStageRetainedFeedLimitedCount > 0;

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

            SelectPrimaryCondition(
                topology,
                live,
                feed,
                status);

            BuildStageSummary(
                status);

            return status;
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
             * Feed amount / FlowEnabled state is topology-snapshot evidence in
             * Build 8.15. Do not promote a snapshot-only limitation to a live
             * engine failure without direct engine evidence.
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
                        PropulsionFeedStatus.Available)
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
