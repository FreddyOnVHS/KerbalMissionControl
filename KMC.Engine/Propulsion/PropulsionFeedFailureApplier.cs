using System;
using System.Collections.Generic;
using KMC.Engine.SpacecraftSystems;

namespace KMC.Engine.Propulsion
{
    /// <summary>
    /// Build 14.12.2 synthetic propulsion-feed failure layer.
    ///
    /// Existing hidden failure truth for PUMP_A / PUMP_B is interpreted as a
    /// redundant liquid-feed pump pair. This does not mutate KSP and does not
    /// claim the stock game contains this pump topology.
    ///
    /// One degraded/failed pump:
    /// - redundant path remains usable
    /// - pump-fed requirements report PRESSURE LOW
    ///
    /// Both pumps failed:
    /// - pump-fed requirements report FLOW DISABLED
    ///
    /// Solid-fuel requirements are not pump-fed and are left untouched.
    /// </summary>
    internal static class PropulsionFeedFailureApplier
    {
        public static void Apply(
            PropulsionFeedModel feed,
            FailureSimulationSnapshot failures)
        {
            if (feed == null ||
                !feed.Available)
            {
                return;
            }

            bool pumpFedSystem =
                ContainsPumpFedRequirement(
                    feed);

            feed.SyntheticPumpModelAvailable =
                pumpFedSystem;

            if (!pumpFedSystem)
            {
                feed.PumpAState =
                    PropulsionFeedPumpState.Unknown;

                feed.PumpBState =
                    PropulsionFeedPumpState.Unknown;

                feed.SyntheticPumpPressureDegraded =
                    false;

                feed.SyntheticPumpFlowLost =
                    false;

                return;
            }

            feed.PumpAState =
                ResolvePumpState(
                    failures,
                    "PUMP_A");

            feed.PumpBState =
                ResolvePumpState(
                    failures,
                    "PUMP_B");

            bool pumpAFailed =
                feed.PumpAState ==
                    PropulsionFeedPumpState.Failed;

            bool pumpBFailed =
                feed.PumpBState ==
                    PropulsionFeedPumpState.Failed;

            feed.SyntheticPumpFlowLost =
                pumpAFailed &&
                pumpBFailed;

            feed.SyntheticPumpPressureDegraded =
                !feed.SyntheticPumpFlowLost &&
                (feed.PumpAState !=
                     PropulsionFeedPumpState.Nominal ||
                 feed.PumpBState !=
                     PropulsionFeedPumpState.Nominal);

            PropulsionFeedStatus syntheticStatus =
                feed.SyntheticPumpFlowLost
                    ? PropulsionFeedStatus.FlowDisabled
                    : feed.SyntheticPumpPressureDegraded
                        ? PropulsionFeedStatus.PressureLow
                        : PropulsionFeedStatus.Available;

            if (syntheticStatus !=
                PropulsionFeedStatus.Available)
            {
                ApplySyntheticStatus(
                    feed,
                    syntheticStatus);
            }

            /*
             * Exact feed-path failures are applied after the shared pump
             * consequence. A local FLOW OFF therefore dominates PRESS LOW for
             * only the selected engine while every other engine keeps the
             * shared-system state.
             */
            ApplyExactEngineFeedPathFailures(
                feed,
                failures);

            Recalculate(
                feed);
        }

        private static PropulsionFeedPumpState ResolvePumpState(
            FailureSimulationSnapshot failures,
            string targetId)
        {
            PropulsionFeedPumpState state =
                PropulsionFeedPumpState.Nominal;

            if (failures == null ||
                failures.Failures == null)
            {
                return state;
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
                        SyntheticFailureTargetKind.Component ||
                    !string.Equals(
                        failure.TargetId,
                        targetId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (failure.ComponentHealth ==
                        SpacecraftSystemHealth.Failed)
                {
                    return
                        PropulsionFeedPumpState.Failed;
                }

                if (failure.ComponentHealth ==
                        SpacecraftSystemHealth.Degraded)
                {
                    state =
                        PropulsionFeedPumpState.Degraded;
                }
            }

            return state;
        }

        private static bool ContainsPumpFedRequirement(
            PropulsionFeedModel feed)
        {
            for (int engineIndex = 0;
                 engineIndex < feed.Engines.Count;
                 engineIndex++)
            {
                PropulsionEngineFeedModel engine =
                    feed.Engines[engineIndex];

                if (engine == null)
                {
                    continue;
                }

                for (int requirementIndex = 0;
                     requirementIndex <
                        engine.Requirements.Count;
                     requirementIndex++)
                {
                    PropulsionRequirementFeedModel requirement =
                        engine.Requirements[
                            requirementIndex];

                    if (IsPumpFedResource(
                            requirement != null
                                ? requirement.ResourceName
                                : string.Empty))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void ApplySyntheticStatus(
            PropulsionFeedModel feed,
            PropulsionFeedStatus syntheticStatus)
        {
            for (int engineIndex = 0;
                 engineIndex < feed.Engines.Count;
                 engineIndex++)
            {
                PropulsionEngineFeedModel engine =
                    feed.Engines[engineIndex];

                if (engine == null)
                {
                    continue;
                }

                for (int requirementIndex = 0;
                     requirementIndex <
                        engine.Requirements.Count;
                     requirementIndex++)
                {
                    PropulsionRequirementFeedModel requirement =
                        engine.Requirements[
                            requirementIndex];

                    if (requirement == null ||
                        !IsPumpFedResource(
                            requirement.ResourceName))
                    {
                        continue;
                    }

                    requirement.CurrentStatus =
                        ApplyIfSyntheticCanDominate(
                            requirement.CurrentStatus,
                            syntheticStatus);

                    if (engine.SurvivesNextStage)
                    {
                        requirement.NextStageStatus =
                            ApplyIfSyntheticCanDominate(
                                requirement.NextStageStatus,
                                syntheticStatus);
                    }
                }
            }
        }

        private static void ApplyExactEngineFeedPathFailures(
            PropulsionFeedModel feed,
            FailureSimulationSnapshot failures)
        {
            feed.SyntheticExactFeedPathDegradedEngineCount =
                0;

            feed.SyntheticExactFeedPathLostEngineCount =
                0;

            if (failures == null ||
                failures.Failures == null)
            {
                return;
            }

            HashSet<uint> affectedPartIds =
                new HashSet<uint>();

            for (int failureIndex = 0;
                 failureIndex < failures.Failures.Count;
                 failureIndex++)
            {
                SyntheticFailureRecord failure =
                    failures.Failures[failureIndex];

                if (failure == null ||
                    !failure.EffectiveNow ||
                    failure.TargetKind !=
                        SyntheticFailureTargetKind.Component)
                {
                    continue;
                }

                uint partId;

                if (!PropulsionFeedFailureTargets
                        .TryParseExactEngineFeedPathTarget(
                            failure.TargetId,
                            out partId))
                {
                    continue;
                }

                PropulsionEngineFeedModel engine =
                    FindEngine(
                        feed,
                        partId);

                if (engine == null)
                {
                    continue;
                }

                PropulsionFeedStatus syntheticStatus =
                    failure.ComponentHealth ==
                        SpacecraftSystemHealth.Degraded
                        ? PropulsionFeedStatus.PressureLow
                        : PropulsionFeedStatus.FlowDisabled;

                bool affected =
                    false;

                for (int requirementIndex = 0;
                     requirementIndex <
                        engine.Requirements.Count;
                     requirementIndex++)
                {
                    PropulsionRequirementFeedModel requirement =
                        engine.Requirements[
                            requirementIndex];

                    if (requirement == null ||
                        !IsPumpFedResource(
                            requirement.ResourceName))
                    {
                        continue;
                    }

                    PropulsionFeedStatus currentBefore =
                        requirement.CurrentStatus;

                    requirement.CurrentStatus =
                        ApplyIfSyntheticCanDominate(
                            requirement.CurrentStatus,
                            syntheticStatus);

                    if (requirement.CurrentStatus !=
                            currentBefore)
                    {
                        affected =
                            true;
                    }

                    if (engine.SurvivesNextStage)
                    {
                        PropulsionFeedStatus nextBefore =
                            requirement.NextStageStatus;

                        requirement.NextStageStatus =
                            ApplyIfSyntheticCanDominate(
                                requirement.NextStageStatus,
                                syntheticStatus);

                        if (requirement.NextStageStatus !=
                                nextBefore)
                        {
                            affected =
                                true;
                        }
                    }
                }

                if (affected)
                {
                    affectedPartIds.Add(
                        partId);
                }
            }

            foreach (uint partId in affectedPartIds)
            {
                PropulsionEngineFeedModel engine =
                    FindEngine(
                        feed,
                        partId);

                if (engine == null)
                {
                    continue;
                }

                if (engine.CurrentFeedStatus ==
                        PropulsionFeedStatus.PressureLow)
                {
                    feed.SyntheticExactFeedPathDegradedEngineCount++;
                }
                else if (engine.CurrentFeedStatus ==
                            PropulsionFeedStatus.FlowDisabled)
                {
                    feed.SyntheticExactFeedPathLostEngineCount++;
                }
            }
        }

        private static PropulsionEngineFeedModel FindEngine(
            PropulsionFeedModel feed,
            uint partId)
        {
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

        private static PropulsionFeedStatus
            ApplyIfSyntheticCanDominate(
                PropulsionFeedStatus existing,
                PropulsionFeedStatus syntheticStatus)
        {
            /*
             * Preserve stronger direct topology/resource evidence:
             * empty/no-source/unknown/flow-disabled remain their own evidence.
             * Synthetic pressure only replaces a healthy AVAILABLE state.
             *
             * Full synthetic flow loss can replace AVAILABLE or PRESSURE LOW.
             */
            if (syntheticStatus ==
                    PropulsionFeedStatus.PressureLow)
            {
                return
                    existing ==
                        PropulsionFeedStatus.Available
                        ? PropulsionFeedStatus.PressureLow
                        : existing;
            }

            if (syntheticStatus ==
                    PropulsionFeedStatus.FlowDisabled &&
                (existing ==
                     PropulsionFeedStatus.Available ||
                 existing ==
                     PropulsionFeedStatus.PressureLow))
            {
                return
                    PropulsionFeedStatus.FlowDisabled;
            }

            return existing;
        }

        private static bool IsPumpFedResource(
            string resourceName)
        {
            return
                string.Equals(
                    resourceName,
                    "LiquidFuel",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    resourceName,
                    "Oxidizer",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static void Recalculate(
            PropulsionFeedModel feed)
        {
            feed.CurrentFeedAvailableEngineCount =
                0;

            feed.CurrentFeedDegradedEngineCount =
                0;

            feed.CurrentFeedLimitedEngineCount =
                0;

            feed.ReadyEngineCount =
                0;

            feed.ReadyEngineFeedAvailableCount =
                0;

            feed.ReadyEngineFeedDegradedCount =
                0;

            feed.ReadyEngineFeedLimitedCount =
                0;

            feed.NextStageRetainedEngineCount =
                0;

            feed.NextStageLostEngineCount =
                0;

            feed.NextStageRetainedFeedAvailableCount =
                0;

            feed.NextStageRetainedFeedDegradedCount =
                0;

            feed.NextStageRetainedFeedLimitedCount =
                0;

            feed.RequirementCount =
                0;

            feed.CurrentAvailableRequirementCount =
                0;

            feed.NextStageAvailableRequirementCount =
                0;

            for (int engineIndex = 0;
                 engineIndex < feed.Engines.Count;
                 engineIndex++)
            {
                PropulsionEngineFeedModel engine =
                    feed.Engines[engineIndex];

                if (engine == null)
                {
                    continue;
                }

                bool hasRequirements =
                    engine.Requirements.Count > 0;

                PropulsionFeedStatus currentWorst =
                    hasRequirements
                        ? PropulsionFeedStatus.Available
                        : PropulsionFeedStatus.SourceStateUnknown;

                PropulsionFeedStatus nextWorst =
                    hasRequirements
                        ? PropulsionFeedStatus.Available
                        : PropulsionFeedStatus.SourceStateUnknown;

                int currentRank =
                    SeverityRank(
                        currentWorst);

                int nextRank =
                    SeverityRank(
                        nextWorst);

                for (int requirementIndex = 0;
                     requirementIndex <
                        engine.Requirements.Count;
                     requirementIndex++)
                {
                    PropulsionRequirementFeedModel requirement =
                        engine.Requirements[
                            requirementIndex];

                    if (requirement == null)
                    {
                        continue;
                    }

                    feed.RequirementCount++;

                    if (IsUsable(
                            requirement.CurrentStatus))
                    {
                        feed.CurrentAvailableRequirementCount++;
                    }

                    if (IsUsable(
                            requirement.NextStageStatus))
                    {
                        feed.NextStageAvailableRequirementCount++;
                    }

                    int rank =
                        SeverityRank(
                            requirement.CurrentStatus);

                    if (rank >
                        currentRank)
                    {
                        currentRank =
                            rank;

                        currentWorst =
                            requirement.CurrentStatus;
                    }

                    rank =
                        SeverityRank(
                            requirement.NextStageStatus);

                    if (rank >
                        nextRank)
                    {
                        nextRank =
                            rank;

                        nextWorst =
                            requirement.NextStageStatus;
                    }
                }

                engine.CurrentFeedStatus =
                    currentWorst;

                if (engine.SurvivesNextStage)
                {
                    engine.NextStageFeedStatus =
                        nextWorst;
                }

                if (IsUsable(
                        engine.CurrentFeedStatus))
                {
                    feed.CurrentFeedAvailableEngineCount++;

                    if (engine.CurrentFeedStatus ==
                            PropulsionFeedStatus.PressureLow)
                    {
                        feed.CurrentFeedDegradedEngineCount++;
                    }
                }
                else
                {
                    feed.CurrentFeedLimitedEngineCount++;
                }

                if (engine.ReadyForThrust)
                {
                    feed.ReadyEngineCount++;

                    if (IsUsable(
                            engine.CurrentFeedStatus))
                    {
                        feed.ReadyEngineFeedAvailableCount++;

                        if (engine.CurrentFeedStatus ==
                                PropulsionFeedStatus.PressureLow)
                        {
                            feed.ReadyEngineFeedDegradedCount++;
                        }
                    }
                    else
                    {
                        feed.ReadyEngineFeedLimitedCount++;
                    }
                }

                if (engine.SurvivesNextStage)
                {
                    feed.NextStageRetainedEngineCount++;

                    if (IsUsable(
                            engine.NextStageFeedStatus))
                    {
                        feed.NextStageRetainedFeedAvailableCount++;

                        if (engine.NextStageFeedStatus ==
                                PropulsionFeedStatus.PressureLow)
                        {
                            feed.NextStageRetainedFeedDegradedCount++;
                        }
                    }
                    else
                    {
                        feed.NextStageRetainedFeedLimitedCount++;
                    }
                }
                else
                {
                    feed.NextStageLostEngineCount++;
                }
            }
        }

        private static bool IsUsable(
            PropulsionFeedStatus status)
        {
            return
                status ==
                    PropulsionFeedStatus.Available ||
                status ==
                    PropulsionFeedStatus.PressureLow;
        }

        private static int SeverityRank(
            PropulsionFeedStatus status)
        {
            switch (status)
            {
                case PropulsionFeedStatus.Available:
                    return 0;

                case PropulsionFeedStatus.PressureLow:
                    return 1;

                case PropulsionFeedStatus.SourceStateUnknown:
                case PropulsionFeedStatus.Unknown:
                    return 2;

                case PropulsionFeedStatus.FlowDisabled:
                    return 3;

                case PropulsionFeedStatus.Depleted:
                    return 4;

                case PropulsionFeedStatus.NoReachableSource:
                    return 5;

                default:
                    return 2;
            }
        }
    }
}
