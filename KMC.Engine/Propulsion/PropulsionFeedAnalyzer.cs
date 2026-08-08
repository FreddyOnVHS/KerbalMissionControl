using System;
using System.Collections.Generic;

namespace KMC.Engine.Propulsion
{
    internal static class PropulsionFeedAnalyzer
    {
        private const double UsableAmountThreshold =
            0.000001;

        public static PropulsionFeedModel Analyze(
            PropulsionTopologyModel topology,
            PropulsionLiveStateModel live)
        {
            PropulsionFeedModel model =
                new PropulsionFeedModel();

            if (topology == null ||
                !topology.Available)
            {
                return model;
            }

            model.Available =
                true;

            model.EngineCount =
                topology.EngineCount;

            model.TopologyNextStage =
                topology.TopologyNextStage;

            if (live != null &&
                live.FlightSummaryAvailable)
            {
                model.LiveCurrentStage =
                    live.LiveCurrentStage;
            }
            else
            {
                model.LiveCurrentStage =
                    topology.TopologyCurrentStage;
            }

            Dictionary<uint, PropulsionEngineLiveStateModel>
                liveByPart =
                    BuildLiveMap(
                        live);

            Dictionary<string, PropulsionResourceSourceModel>
                sourceByKey =
                    BuildSourceMap(
                        topology.ResourceSources);

            for (int engineIndex = 0;
                 engineIndex < topology.Engines.Count;
                 engineIndex++)
            {
                PropulsionEngineModel engine =
                    topology.Engines[engineIndex];

                PropulsionEngineFeedModel engineFeed =
                    new PropulsionEngineFeedModel
                    {
                        PartId =
                            engine.PartId,

                        PartTitle =
                            engine.PartTitle,

                        ActivationStage =
                            engine.ActivationStage,

                        SeparationStage =
                            engine.SeparationStage,

                        SurvivesNextStage =
                            engine.SurvivesNextStage,

                        CurrentFeedStatus =
                            PropulsionFeedStatus.Unknown,

                        NextStageFeedStatus =
                            engine.SurvivesNextStage
                                ? PropulsionFeedStatus.Unknown
                                : PropulsionFeedStatus.NoReachableSource
                    };

                PropulsionEngineLiveStateModel liveEngine;

                if (liveByPart.TryGetValue(
                        engine.PartId,
                        out liveEngine) &&
                    live != null &&
                    live.TelemetryFresh &&
                    liveEngine.TelemetryMatched)
                {
                    engineFeed.LiveStateKnown =
                        true;

                    engineFeed.OperatingState =
                        liveEngine.OperatingState;

                    engineFeed.ReadyForThrust =
                        liveEngine.ReadyForThrust;
                }

                bool currentAllAvailable =
                    engine.PropellantRequirements.Count > 0;

                bool nextAllAvailable =
                    engine.PropellantRequirements.Count > 0;

                for (int requirementIndex = 0;
                     requirementIndex <
                        engine.PropellantRequirements.Count;
                     requirementIndex++)
                {
                    PropulsionPropellantRequirementModel
                        requirement =
                            engine.PropellantRequirements[
                                requirementIndex];

                    PropulsionRequirementFeedModel feed =
                        AnalyzeRequirement(
                            requirement,
                            sourceByKey);

                    engineFeed.Requirements.Add(
                        feed);

                    model.RequirementCount++;

                    if (feed.CurrentStatus ==
                        PropulsionFeedStatus.Available)
                    {
                        model.CurrentAvailableRequirementCount++;
                    }
                    else
                    {
                        currentAllAvailable =
                            false;
                    }

                    if (feed.NextStageStatus ==
                        PropulsionFeedStatus.Available)
                    {
                        model.NextStageAvailableRequirementCount++;
                    }
                    else
                    {
                        nextAllAvailable =
                            false;
                    }
                }

                if (engine.PropellantRequirements.Count == 0)
                {
                    engineFeed.CurrentFeedStatus =
                        PropulsionFeedStatus.SourceStateUnknown;

                    if (engine.SurvivesNextStage)
                    {
                        engineFeed.NextStageFeedStatus =
                            PropulsionFeedStatus.SourceStateUnknown;
                    }
                }
                else
                {
                    engineFeed.CurrentFeedStatus =
                        currentAllAvailable
                            ? PropulsionFeedStatus.Available
                            : WorstRequirementStatus(
                                engineFeed.Requirements,
                                false);

                    if (engine.SurvivesNextStage)
                    {
                        engineFeed.NextStageFeedStatus =
                            nextAllAvailable
                                ? PropulsionFeedStatus.Available
                                : WorstRequirementStatus(
                                    engineFeed.Requirements,
                                    true);
                    }
                }

                if (engineFeed.CurrentFeedStatus ==
                    PropulsionFeedStatus.Available)
                {
                    model.CurrentFeedAvailableEngineCount++;
                }
                else
                {
                    model.CurrentFeedLimitedEngineCount++;
                }

                if (engineFeed.ReadyForThrust)
                {
                    model.ReadyEngineCount++;

                    if (engineFeed.CurrentFeedStatus ==
                        PropulsionFeedStatus.Available)
                    {
                        model.ReadyEngineFeedAvailableCount++;
                    }
                    else
                    {
                        model.ReadyEngineFeedLimitedCount++;
                    }
                }

                if (engine.SurvivesNextStage)
                {
                    model.NextStageRetainedEngineCount++;

                    if (engineFeed.NextStageFeedStatus ==
                        PropulsionFeedStatus.Available)
                    {
                        model.NextStageRetainedFeedAvailableCount++;
                    }
                    else
                    {
                        model.NextStageRetainedFeedLimitedCount++;
                    }
                }
                else
                {
                    model.NextStageLostEngineCount++;
                }

                model.Engines.Add(
                    engineFeed);
            }

            return model;
        }

        private static PropulsionRequirementFeedModel
            AnalyzeRequirement(
                PropulsionPropellantRequirementModel requirement,
                IDictionary<string, PropulsionResourceSourceModel>
                    sourceByKey)
        {
            PropulsionRequirementFeedModel feed =
                new PropulsionRequirementFeedModel
                {
                    ResourceId =
                        requirement.ResourceId,

                    ResourceName =
                        requirement.ResourceName,

                    Ratio =
                        requirement.Ratio,

                    DensityTonnesPerUnit =
                        requirement.DensityTonnesPerUnit
                };

            bool anyCurrentUnknown =
                false;

            bool anyCurrentKnown =
                false;

            bool anyCurrentFlowEnabled =
                false;

            bool anyCurrentUsable =
                false;

            bool anyNextUnknown =
                false;

            bool anyNextKnown =
                false;

            bool anyNextFlowEnabled =
                false;

            bool anyNextUsable =
                false;

            for (int sourceIndex = 0;
                 sourceIndex <
                    requirement.ReachableSourcePartIds.Count;
                 sourceIndex++)
            {
                uint partId =
                    requirement.ReachableSourcePartIds[
                        sourceIndex];

                AddUnique(
                    feed.CurrentSourcePartIds,
                    partId);

                feed.CurrentReachableSourceCount++;

                PropulsionResourceSourceModel source;

                if (!sourceByKey.TryGetValue(
                        Key(
                            partId,
                            requirement.ResourceName),
                        out source))
                {
                    anyCurrentUnknown =
                        true;

                    continue;
                }

                if (!source.ResourceStateAvailable)
                {
                    anyCurrentUnknown =
                        true;
                }
                else
                {
                    anyCurrentKnown =
                        true;

                    feed.CurrentKnownSourceCount++;

                    feed.CurrentAmount +=
                        Math.Max(
                            0.0,
                            source.Amount);

                    feed.CurrentCapacity +=
                        Math.Max(
                            0.0,
                            source.Capacity);

                    if (source.FlowEnabled)
                    {
                        anyCurrentFlowEnabled =
                            true;

                        feed.CurrentFlowEnabledSourceCount++;

                        if (source.Amount >
                            UsableAmountThreshold)
                        {
                            anyCurrentUsable =
                                true;

                            feed.CurrentUsableSourceCount++;

                            AddUnique(
                                feed.CurrentUsableSourcePartIds,
                                partId);
                        }
                    }
                }

                if (!source.SurvivesNextStage)
                {
                    continue;
                }

                AddUnique(
                    feed.NextStageSourcePartIds,
                    partId);

                feed.NextStageReachableSourceCount++;

                if (!source.ResourceStateAvailable)
                {
                    anyNextUnknown =
                        true;

                    continue;
                }

                anyNextKnown =
                    true;

                feed.NextStageKnownSourceCount++;

                feed.NextStageAmount +=
                    Math.Max(
                        0.0,
                        source.Amount);

                feed.NextStageCapacity +=
                    Math.Max(
                        0.0,
                        source.Capacity);

                if (source.FlowEnabled)
                {
                    anyNextFlowEnabled =
                        true;

                    feed.NextStageFlowEnabledSourceCount++;

                    if (source.Amount >
                        UsableAmountThreshold)
                    {
                        anyNextUsable =
                            true;

                        feed.NextStageUsableSourceCount++;

                        AddUnique(
                            feed.NextStageUsableSourcePartIds,
                            partId);
                    }
                }
            }

            feed.CurrentStatus =
                Classify(
                    feed.CurrentReachableSourceCount,
                    anyCurrentUnknown,
                    anyCurrentKnown,
                    anyCurrentFlowEnabled,
                    anyCurrentUsable);

            feed.NextStageStatus =
                Classify(
                    feed.NextStageReachableSourceCount,
                    anyNextUnknown,
                    anyNextKnown,
                    anyNextFlowEnabled,
                    anyNextUsable);

            return feed;
        }

        private static PropulsionFeedStatus Classify(
            int reachableCount,
            bool anyUnknown,
            bool anyKnown,
            bool anyFlowEnabled,
            bool anyUsable)
        {
            if (reachableCount <= 0)
            {
                return
                    PropulsionFeedStatus.NoReachableSource;
            }

            if (anyUsable)
            {
                return
                    PropulsionFeedStatus.Available;
            }

            if (!anyKnown &&
                anyUnknown)
            {
                return
                    PropulsionFeedStatus.SourceStateUnknown;
            }

            if (anyKnown &&
                !anyFlowEnabled)
            {
                return
                    PropulsionFeedStatus.FlowDisabled;
            }

            if (anyKnown &&
                anyFlowEnabled)
            {
                return
                    PropulsionFeedStatus.Depleted;
            }

            return
                PropulsionFeedStatus.SourceStateUnknown;
        }

        private static PropulsionFeedStatus WorstRequirementStatus(
            IList<PropulsionRequirementFeedModel> requirements,
            bool nextStage)
        {
            PropulsionFeedStatus worst =
                PropulsionFeedStatus.Unknown;

            int worstRank =
                -1;

            for (int index = 0;
                 index < requirements.Count;
                 index++)
            {
                PropulsionFeedStatus status =
                    nextStage
                        ? requirements[index].NextStageStatus
                        : requirements[index].CurrentStatus;

                int rank =
                    SeverityRank(
                        status);

                if (rank > worstRank)
                {
                    worstRank =
                        rank;

                    worst =
                        status;
                }
            }

            return worst;
        }

        private static int SeverityRank(
            PropulsionFeedStatus status)
        {
            switch (status)
            {
                case PropulsionFeedStatus.Available:
                    return 0;

                case PropulsionFeedStatus.SourceStateUnknown:
                case PropulsionFeedStatus.Unknown:
                    return 1;

                case PropulsionFeedStatus.FlowDisabled:
                    return 2;

                case PropulsionFeedStatus.Depleted:
                    return 3;

                case PropulsionFeedStatus.NoReachableSource:
                    return 4;

                default:
                    return 1;
            }
        }

        private static Dictionary<uint, PropulsionEngineLiveStateModel>
            BuildLiveMap(
                PropulsionLiveStateModel live)
        {
            Dictionary<uint, PropulsionEngineLiveStateModel> result =
                new Dictionary<uint, PropulsionEngineLiveStateModel>();

            if (live == null)
            {
                return result;
            }

            for (int index = 0;
                 index < live.Engines.Count;
                 index++)
            {
                PropulsionEngineLiveStateModel engine =
                    live.Engines[index];

                if (engine != null)
                {
                    result[engine.PartId] =
                        engine;
                }
            }

            return result;
        }

        private static Dictionary<string, PropulsionResourceSourceModel>
            BuildSourceMap(
                IList<PropulsionResourceSourceModel> sources)
        {
            Dictionary<string, PropulsionResourceSourceModel> result =
                new Dictionary<string, PropulsionResourceSourceModel>(
                    StringComparer.OrdinalIgnoreCase);

            if (sources == null)
            {
                return result;
            }

            for (int index = 0;
                 index < sources.Count;
                 index++)
            {
                PropulsionResourceSourceModel source =
                    sources[index];

                if (source != null)
                {
                    result[
                        Key(
                            source.PartId,
                            source.ResourceName)] =
                        source;
                }
            }

            return result;
        }

        private static string Key(
            uint partId,
            string resourceName)
        {
            return
                partId.ToString() +
                "|" +
                (resourceName ?? string.Empty);
        }

        private static void AddUnique(
            IList<uint> values,
            uint value)
        {
            for (int index = 0;
                 index < values.Count;
                 index++)
            {
                if (values[index] ==
                    value)
                {
                    return;
                }
            }

            values.Add(
                value);
        }
    }
}
