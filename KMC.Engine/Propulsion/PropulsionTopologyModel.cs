using System.Collections.Generic;
using KMC.Shared.Topology;

namespace KMC.Engine.Propulsion
{
    /// <summary>
    /// Topology-derived description of one propulsion engine or booster.
    ///
    /// This is structural engineering evidence only. Live operating state,
    /// current thrust, flameout state, and commanded throttle are deliberately
    /// not stored here.
    /// </summary>
    public sealed class PropulsionEngineModel
    {
        public PropulsionEngineModel()
        {
            PartTitle =
                string.Empty;

            PartName =
                string.Empty;

            PropellantRequirements =
                new List<PropulsionPropellantRequirementModel>();
        }

        public uint PartId { get; internal set; }
        public string PartTitle { get; internal set; }
        public string PartName { get; internal set; }
        public VesselNodeCategory Category { get; internal set; }

        public int ActivationStage { get; internal set; }
        public int SeparationStage { get; internal set; }

        public int StructuralDepth { get; internal set; }
        public uint BranchRootPartId { get; internal set; }
        public uint SymmetryGroupId { get; internal set; }

        public double VesselX { get; internal set; }
        public double VesselY { get; internal set; }
        public double VesselZ { get; internal set; }

        public bool SurvivesNextStage { get; internal set; }

        public bool IsSolidBooster
        {
            get
            {
                return
                    Category ==
                    VesselNodeCategory.SolidBooster;
            }
        }

        public List<PropulsionPropellantRequirementModel>
            PropellantRequirements
        {
            get;
            private set;
        }
    }

    /// <summary>
    /// One propellant requirement copied from the vessel topology together
    /// with the source parts the topology graph says are currently reachable.
    /// </summary>
    public sealed class PropulsionPropellantRequirementModel
    {
        public PropulsionPropellantRequirementModel()
        {
            ResourceName =
                string.Empty;

            RawFlowMode =
                string.Empty;

            ReachableSourcePartIds =
                new List<uint>();
        }

        public int ResourceId { get; internal set; }
        public string ResourceName { get; internal set; }
        public double Ratio { get; internal set; }
        public double DensityTonnesPerUnit { get; internal set; }
        public string RawFlowMode { get; internal set; }

        public List<uint> ReachableSourcePartIds
        {
            get;
            private set;
        }
    }

    /// <summary>
    /// One resource on a part that is referenced as a reachable propulsion
    /// source by at least one engine propellant requirement.
    /// </summary>
    public sealed class PropulsionResourceSourceModel
    {
        public PropulsionResourceSourceModel()
        {
            PartTitle =
                string.Empty;

            ResourceName =
                string.Empty;
        }

        public uint PartId { get; internal set; }
        public string PartTitle { get; internal set; }

        public int ResourceId { get; internal set; }
        public string ResourceName { get; internal set; }

        public bool ResourceStateAvailable { get; internal set; }
        public double Amount { get; internal set; }
        public double Capacity { get; internal set; }
        public double DensityTonnesPerUnit { get; internal set; }
        public bool FlowEnabled { get; internal set; }

        public bool SurvivesNextStage { get; internal set; }

        public double FillFraction
        {
            get
            {
                if (!ResourceStateAvailable ||
                    Capacity <= 0.0)
                {
                    return 0.0;
                }

                double value =
                    Amount /
                    Capacity;

                if (value < 0.0)
                {
                    return 0.0;
                }

                return
                    value > 1.0
                        ? 1.0
                        : value;
            }
        }
    }

    /// <summary>
    /// Engine-owned propulsion topology model. This is the migration target
    /// for structural propulsion meaning that previously lived only in
    /// MissionControl rendering analysis.
    /// </summary>
    public sealed class PropulsionTopologyModel
    {
        public PropulsionTopologyModel()
        {
            VesselId =
                string.Empty;

            VesselName =
                string.Empty;

            Engines =
                new List<PropulsionEngineModel>();

            ResourceSources =
                new List<PropulsionResourceSourceModel>();

            SeparationStages =
                new List<int>();
        }

        public bool Available { get; internal set; }

        public string VesselId { get; internal set; }
        public string VesselName { get; internal set; }

        public long TopologyRevision { get; internal set; }

        /// <summary>
        /// Stage values from the topology snapshot. These are intentionally
        /// labeled topology values because live flight telemetry can change
        /// stage state before vessel structure is rebuilt.
        /// </summary>
        public int TopologyCurrentStage { get; internal set; }
        public int TopologyNextStage { get; internal set; }

        public List<PropulsionEngineModel> Engines
        {
            get;
            private set;
        }

        public List<PropulsionResourceSourceModel> ResourceSources
        {
            get;
            private set;
        }

        public List<int> SeparationStages
        {
            get;
            private set;
        }

        public int EngineCount
        {
            get { return Engines.Count; }
        }

        public int SolidBoosterCount
        {
            get
            {
                int count = 0;

                for (int index = 0;
                     index < Engines.Count;
                     index++)
                {
                    if (Engines[index].IsSolidBooster)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int LiquidFuelOxidizerEngineCount
        {
            get
            {
                int count = 0;

                for (int index = 0;
                     index < Engines.Count;
                     index++)
                {
                    bool liquidFuel = false;
                    bool oxidizer = false;

                    for (int requirementIndex = 0;
                         requirementIndex <
                            Engines[index]
                                .PropellantRequirements.Count;
                         requirementIndex++)
                    {
                        string name =
                            Engines[index]
                                .PropellantRequirements[
                                    requirementIndex]
                                .ResourceName;

                        if (string.Equals(
                                name,
                                "LiquidFuel",
                                System.StringComparison
                                    .OrdinalIgnoreCase))
                        {
                            liquidFuel =
                                true;
                        }
                        else if (string.Equals(
                                     name,
                                     "Oxidizer",
                                     System.StringComparison
                                         .OrdinalIgnoreCase))
                        {
                            oxidizer =
                                true;
                        }
                    }

                    if (liquidFuel &&
                        oxidizer)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int PropellantRequirementCount
        {
            get
            {
                int count = 0;

                for (int index = 0;
                     index < Engines.Count;
                     index++)
                {
                    count +=
                        Engines[index]
                            .PropellantRequirements.Count;
                }

                return count;
            }
        }

        public int ResourceSourceEntryCount
        {
            get
            {
                return
                    ResourceSources.Count;
            }
        }

        public int ResourceSourcePartCount
        {
            get
            {
                HashSet<uint> parts =
                    new HashSet<uint>();

                for (int index = 0;
                     index < ResourceSources.Count;
                     index++)
                {
                    parts.Add(
                        ResourceSources[index]
                            .PartId);
                }

                return parts.Count;
            }
        }

        public int EnginesLostOnNextStage
        {
            get
            {
                int count = 0;

                for (int index = 0;
                     index < Engines.Count;
                     index++)
                {
                    if (!Engines[index]
                            .SurvivesNextStage)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int EnginesRetainedAfterNextStage
        {
            get
            {
                return
                    EngineCount -
                    EnginesLostOnNextStage;
            }
        }

        public int ResourceSourcePartsLostOnNextStage
        {
            get
            {
                HashSet<uint> parts =
                    new HashSet<uint>();

                for (int index = 0;
                     index < ResourceSources.Count;
                     index++)
                {
                    if (!ResourceSources[index]
                            .SurvivesNextStage)
                    {
                        parts.Add(
                            ResourceSources[index]
                                .PartId);
                    }
                }

                return parts.Count;
            }
        }

        public int ResourceSourcePartsRetainedAfterNextStage
        {
            get
            {
                return
                    ResourceSourcePartCount -
                    ResourceSourcePartsLostOnNextStage;
            }
        }
    }
}
