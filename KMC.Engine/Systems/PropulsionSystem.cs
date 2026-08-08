using System.Diagnostics;
using KMC.Engine.Analysis;
using KMC.Engine.Propulsion;

namespace KMC.Engine.Systems
{
    public sealed class PropulsionSystem :
        IEngineeringSystem
    {
        private long _lastLoggedTopologyRevision =
            long.MinValue;

        private string _lastLoggedVesselId =
            string.Empty;

        public string Name
        {
            get { return "Propulsion"; }
        }

        public int Order
        {
            get { return 300; }
        }

        public void Analyze(
            AnalysisContext context)
        {
            PropulsionTopologyModel topology =
                PropulsionTopologyAnalyzer.Analyze(
                    context.Vessel.Topology);

            context.Propulsion.Topology =
                topology;

            context.Propulsion.IsAvailable =
                topology.Available;

            context.Propulsion.HasPropulsion =
                topology.EngineCount > 0;

            context.Propulsion.EngineCount =
                topology.EngineCount;

            /*
             * Build 8.12 is topology-only. Do not equate "exists" with
             * "operable" and do not invent available thrust from part data.
             */
            context.Propulsion.LiveEngineStateAvailable =
                false;

            context.Propulsion.OperableEngineCount =
                0;

            context.Propulsion.AvailableThrust =
                0.0;

            context.AddDiagnostic(
                "Propulsion topology: engines=" +
                topology.EngineCount +
                ", LF/OX engines=" +
                topology.LiquidFuelOxidizerEngineCount +
                ", solid boosters=" +
                topology.SolidBoosterCount +
                ", propellant requirements=" +
                topology.PropellantRequirementCount +
                ", source parts=" +
                topology.ResourceSourcePartCount +
                ", next-stage engine loss=" +
                topology.EnginesLostOnNextStage +
                ".");

            WriteTopologyDiagnosticIfChanged(
                topology);
        }

        private void WriteTopologyDiagnosticIfChanged(
            PropulsionTopologyModel topology)
        {
            if (topology == null ||
                !topology.Available)
            {
                return;
            }

            if (topology.TopologyRevision ==
                    _lastLoggedTopologyRevision &&
                string.Equals(
                    topology.VesselId,
                    _lastLoggedVesselId,
                    System.StringComparison.Ordinal))
            {
                return;
            }

            _lastLoggedTopologyRevision =
                topology.TopologyRevision;

            _lastLoggedVesselId =
                topology.VesselId ??
                string.Empty;

            Debug.WriteLine(
                "KMC.Engine PROPULSION FOUNDATION | Vessel=" +
                topology.VesselName +
                " | Revision=" +
                topology.TopologyRevision +
                " | TopologyStage=" +
                topology.TopologyCurrentStage +
                " | NextStage=" +
                topology.TopologyNextStage +
                " | Engines=" +
                topology.EngineCount +
                " | LFOX=" +
                topology.LiquidFuelOxidizerEngineCount +
                " | SRB=" +
                topology.SolidBoosterCount +
                " | Requirements=" +
                topology.PropellantRequirementCount +
                " | SourceParts=" +
                topology.ResourceSourcePartCount +
                " | SourceEntries=" +
                topology.ResourceSourceEntryCount +
                " | NextStageEngineLoss=" +
                topology.EnginesLostOnNextStage +
                " | NextStageSourceLoss=" +
                topology.ResourceSourcePartsLostOnNextStage);

            for (int index = 0;
                 index < topology.Engines.Count;
                 index++)
            {
                PropulsionEngineModel engine =
                    topology.Engines[index];

                Debug.WriteLine(
                    "KMC.Engine PROP ENGINE | Part=" +
                    engine.PartId +
                    " | Title=" +
                    engine.PartTitle +
                    " | Category=" +
                    engine.Category +
                    " | ActivateStage=" +
                    engine.ActivationStage +
                    " | SeparateStage=" +
                    engine.SeparationStage +
                    " | SurvivesNext=" +
                    engine.SurvivesNextStage +
                    " | Symmetry=" +
                    engine.SymmetryGroupId +
                    " | Branch=" +
                    engine.BranchRootPartId +
                    " | XYZ=" +
                    engine.VesselX.ToString("0.###") +
                    "," +
                    engine.VesselY.ToString("0.###") +
                    "," +
                    engine.VesselZ.ToString("0.###") +
                    " | Propellants=" +
                    engine.PropellantRequirements.Count);
            }

            for (int index = 0;
                 index < topology.ResourceSources.Count;
                 index++)
            {
                PropulsionResourceSourceModel source =
                    topology.ResourceSources[index];

                Debug.WriteLine(
                    "KMC.Engine PROP SOURCE | Part=" +
                    source.PartId +
                    " | Title=" +
                    source.PartTitle +
                    " | Resource=" +
                    source.ResourceName +
                    " | StateKnown=" +
                    source.ResourceStateAvailable +
                    " | Amount=" +
                    source.Amount.ToString("0.###") +
                    "/" +
                    source.Capacity.ToString("0.###") +
                    " | FlowEnabled=" +
                    source.FlowEnabled +
                    " | SurvivesNext=" +
                    source.SurvivesNextStage);
            }
        }
    }
}
