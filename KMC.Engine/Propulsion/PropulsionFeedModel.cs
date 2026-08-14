﻿using System.Collections.Generic;

namespace KMC.Engine.Propulsion
{
    public enum PropulsionFeedStatus
    {
        Unknown = 0,
        Available,
        PressureLow,
        Depleted,
        FlowDisabled,
        SourceStateUnknown,
        NoReachableSource
    }

    /// <summary>
    /// Build 14.12.2 synthetic redundant feed-pump state.
    ///
    /// This is KMC Engine-owned spacecraft behavior. It is not a claim about
    /// stock KSP pump hardware.
    /// </summary>
    public enum PropulsionFeedPumpState
    {
        Unknown = 0,
        Nominal,
        Degraded,
        Failed
    }

    /// <summary>
    /// Engineering interpretation of one propellant requirement for one engine.
    /// Current and next-stage states are evaluated independently.
    /// </summary>
    public sealed class PropulsionRequirementFeedModel
    {
        public PropulsionRequirementFeedModel()
        {
            ResourceName =
                string.Empty;

            CurrentSourcePartIds =
                new List<uint>();

            CurrentUsableSourcePartIds =
                new List<uint>();

            NextStageSourcePartIds =
                new List<uint>();

            NextStageUsableSourcePartIds =
                new List<uint>();
        }

        public int ResourceId { get; internal set; }

        public string ResourceName { get; internal set; }

        public double Ratio { get; internal set; }

        public double DensityTonnesPerUnit { get; internal set; }

        public PropulsionFeedStatus CurrentStatus
        {
            get;
            internal set;
        }

        public int CurrentReachableSourceCount { get; internal set; }

        public int CurrentKnownSourceCount { get; internal set; }

        public int CurrentFlowEnabledSourceCount { get; internal set; }

        public int CurrentUsableSourceCount { get; internal set; }

        public double CurrentAmount { get; internal set; }

        public double CurrentCapacity { get; internal set; }

        public PropulsionFeedStatus NextStageStatus
        {
            get;
            internal set;
        }

        public int NextStageReachableSourceCount { get; internal set; }

        public int NextStageKnownSourceCount { get; internal set; }

        public int NextStageFlowEnabledSourceCount { get; internal set; }

        public int NextStageUsableSourceCount { get; internal set; }

        public double NextStageAmount { get; internal set; }

        public double NextStageCapacity { get; internal set; }

        public List<uint> CurrentSourcePartIds
        {
            get;
            private set;
        }

        public List<uint> CurrentUsableSourcePartIds
        {
            get;
            private set;
        }

        public List<uint> NextStageSourcePartIds
        {
            get;
            private set;
        }

        public List<uint> NextStageUsableSourcePartIds
        {
            get;
            private set;
        }
    }

    public sealed class PropulsionEngineFeedModel
    {
        public PropulsionEngineFeedModel()
        {
            PartTitle =
                string.Empty;

            Requirements =
                new List<PropulsionRequirementFeedModel>();
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

        public List<PropulsionRequirementFeedModel> Requirements
        {
            get;
            private set;
        }
    }

    /// <summary>
    /// Engine-owned feed graph interpretation. It does not calculate burn
    /// duration or mass flow. It only evaluates whether required propellant
    /// has a known, reachable, flow-enabled, nonempty source.
    /// </summary>
    public sealed class PropulsionFeedModel
    {
        public PropulsionFeedModel()
        {
            Engines =
                new List<PropulsionEngineFeedModel>();

            PumpAState =
                PropulsionFeedPumpState.Unknown;

            PumpBState =
                PropulsionFeedPumpState.Unknown;
        }

        public bool Available { get; internal set; }

        public int LiveCurrentStage { get; internal set; }

        public int TopologyNextStage { get; internal set; }

        public int EngineCount { get; internal set; }

        public int CurrentFeedAvailableEngineCount
        {
            get;
            internal set;
        }

        public int CurrentFeedDegradedEngineCount
        {
            get;
            internal set;
        }

        public int CurrentFeedLimitedEngineCount
        {
            get;
            internal set;
        }

        public int ReadyEngineCount { get; internal set; }

        public int ReadyEngineFeedAvailableCount
        {
            get;
            internal set;
        }

        public int ReadyEngineFeedDegradedCount
        {
            get;
            internal set;
        }

        public int ReadyEngineFeedLimitedCount
        {
            get;
            internal set;
        }

        public int NextStageRetainedEngineCount
        {
            get;
            internal set;
        }

        public int NextStageLostEngineCount
        {
            get;
            internal set;
        }

        public int NextStageRetainedFeedAvailableCount
        {
            get;
            internal set;
        }

        public int NextStageRetainedFeedDegradedCount
        {
            get;
            internal set;
        }

        public int NextStageRetainedFeedLimitedCount
        {
            get;
            internal set;
        }

        public int RequirementCount { get; internal set; }

        public int CurrentAvailableRequirementCount
        {
            get;
            internal set;
        }

        public int NextStageAvailableRequirementCount
        {
            get;
            internal set;
        }

        public bool SyntheticPumpModelAvailable
        {
            get;
            internal set;
        }

        public PropulsionFeedPumpState PumpAState
        {
            get;
            internal set;
        }

        public PropulsionFeedPumpState PumpBState
        {
            get;
            internal set;
        }

        public bool SyntheticPumpPressureDegraded
        {
            get;
            internal set;
        }

        public bool SyntheticPumpFlowLost
        {
            get;
            internal set;
        }

        public List<PropulsionEngineFeedModel> Engines
        {
            get;
            private set;
        }
    }
}
