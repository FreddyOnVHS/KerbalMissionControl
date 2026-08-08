using System;
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

        private DateTime _lastLiveDiagnosticUtc =
            DateTime.MinValue;

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

            PropulsionLiveStateModel live =
                PropulsionLiveStateAnalyzer.Analyze(
                    topology,
                    context.Telemetry.PropulsionTelemetry,
                    context.Telemetry.ReceivedUtc,
                    context.Telemetry.Packet);

            context.Propulsion.Live =
                live;

            context.Propulsion.LiveEngineStateAvailable =
                live.TelemetryFresh &&
                live.MatchedEngineCount > 0;

            context.Propulsion.OperableEngineCount =
                context.Propulsion.LiveEngineStateAvailable
                    ? live.ReadyEngineCount
                    : 0;

            context.Propulsion.AvailableThrustKnown =
                live.AvailableThrustKnown;

            context.Propulsion.AvailableThrust =
                live.AvailableThrustKnown
                    ? live.AvailableThrust
                    : 0.0;

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

            context.AddDiagnostic(
                "Propulsion live state: telemetry=" +
                (live.TelemetryAvailable
                    ? "available"
                    : "unavailable") +
                ", fresh=" +
                live.TelemetryFresh +
                ", matched=" +
                live.MatchedEngineCount +
                "/" +
                live.TopologyEngineCount +
                ", stage=" +
                (live.FlightSummaryAvailable
                    ? live.LiveCurrentStage.ToString()
                    : "UNKNOWN") +
                ", ready=" +
                live.ReadyEngineCount +
                ", future=" +
                live.FutureStageEngineCount +
                ", producing=" +
                live.ProducingEngineCount +
                ", flameout=" +
                live.FlameoutEngineCount +
                ", thrust=" +
                (live.CurrentThrustKnown
                    ? live.CurrentThrust.ToString("0.###")
                    : "UNKNOWN") +
                ".");

            WriteTopologyDiagnosticIfChanged(
                topology);

            WriteLiveDiagnosticIfDue(
                context.Telemetry.ReceivedUtc,
                live);
        }

        private void WriteLiveDiagnosticIfDue(
            DateTime receivedUtc,
            PropulsionLiveStateModel live)
        {
            DateTime utc =
                receivedUtc.Kind == DateTimeKind.Utc
                    ? receivedUtc
                    : receivedUtc.ToUniversalTime();

            if (_lastLiveDiagnosticUtc !=
                    DateTime.MinValue &&
                (utc -
                 _lastLiveDiagnosticUtc)
                    .TotalSeconds <
                1.0)
            {
                return;
            }

            _lastLiveDiagnosticUtc =
                utc;

            string age =
                live.TelemetryAvailable
                    ? live.TelemetryAgeSeconds
                        .ToString("0.000") +
                      "s"
                    : "--";

            string currentThrust =
                live.CurrentThrustKnown
                    ? live.CurrentThrust
                        .ToString("0.###")
                    : "UNKNOWN";

            string readyMax =
                live.AvailableThrustKnown
                    ? live.AvailableThrust
                        .ToString("0.###")
                    : "UNKNOWN";

            string potentialMax =
                live.PotentialMaximumThrustKnown
                    ? live.PotentialMaximumThrust
                        .ToString("0.###")
                    : "UNKNOWN";

            Debug.WriteLine(
                "KMC.Engine PROPULSION LIVE | Telemetry=" +
                (live.TelemetryAvailable
                    ? "LIVE"
                    : "NONE") +
                " | Fresh=" +
                live.TelemetryFresh +
                " | Age=" +
                age +
                " | LiveStage=" +
                (live.FlightSummaryAvailable
                    ? live.LiveCurrentStage.ToString()
                    : "--") +
                " | TopologyEngines=" +
                live.TopologyEngineCount +
                " | Matched=" +
                live.MatchedEngineCount +
                " | MissingTopology=" +
                live.UnmatchedTopologyEngineCount +
                " | ExtraTelemetry=" +
                live.UnmatchedTelemetryEngineCount +
                " | CoverageComplete=" +
                live.CoverageComplete +
                " | Armed=" +
                live.ArmedEngineCount +
                " | Ignited=" +
                live.IgnitedEngineCount +
                " | Producing=" +
                live.ProducingEngineCount +
                " | Shutdown=" +
                live.ShutdownEngineCount +
                " | Flameout=" +
                live.FlameoutEngineCount +
                " | Unknown=" +
                live.UnknownEngineCount +
                " | FutureStage=" +
                live.FutureStageEngineCount +
                " | Ready=" +
                live.ReadyEngineCount +
                " | CurrentThrust=" +
                currentThrust +
                " | ReadyMaxThrust=" +
                readyMax +
                " | PotentialMaxThrust=" +
                potentialMax +
                " | FlightEngines=" +
                (live.FlightSummaryAvailable
                    ? live.FlightEngineCount.ToString()
                    : "--") +
                " | FlightCurrentThrust=" +
                (live.FlightSummaryAvailable
                    ? live.FlightCurrentThrust
                        .ToString("0.###")
                    : "--") +
                " | ThrustDiff=" +
                (live.CurrentThrustKnown &&
                 live.FlightSummaryAvailable
                    ? live.CurrentThrustDifference
                        .ToString("+0.###;-0.###;0")
                    : "--") +
                " | ThrustAgree=" +
                (live.CurrentThrustKnown &&
                 live.FlightSummaryAvailable
                    ? live.CurrentThrustAgreesWithFlightSummary
                        .ToString()
                    : "--"));

            for (int index = 0;
                 index < live.Engines.Count;
                 index++)
            {
                PropulsionEngineLiveStateModel engine =
                    live.Engines[index];

                Debug.WriteLine(
                    "KMC.Engine PROP LIVE ENGINE | Part=" +
                    engine.PartId +
                    " | Title=" +
                    engine.PartTitle +
                    " | Matched=" +
                    engine.TelemetryMatched +
                    " | State=" +
                    (live.TelemetryFresh
                        ? engine.OperatingState.ToString()
                        : "STALE/UNKNOWN") +
                    " | ActivateStage=" +
                    engine.ActivationStage +
                    " | StageEligible=" +
                    (live.FlightSummaryAvailable &&
                     live.TelemetryFresh &&
                     engine.TelemetryMatched
                        ? engine.StageEligible.ToString()
                        : "UNKNOWN") +
                    " | FutureStage=" +
                    (live.FlightSummaryAvailable &&
                     live.TelemetryFresh &&
                     engine.TelemetryMatched
                        ? engine.IsFutureStage.ToString()
                        : "UNKNOWN") +
                    " | CurrentThrust=" +
                    (live.TelemetryFresh &&
                     engine.TelemetryMatched
                        ? engine.CurrentThrust
                            .ToString("0.###")
                        : "UNKNOWN") +
                    " | MaxThrust=" +
                    (live.TelemetryFresh &&
                     engine.TelemetryMatched
                        ? engine.MaximumThrust
                            .ToString("0.###")
                        : "UNKNOWN") +
                    " | Ready=" +
                    (live.TelemetryFresh &&
                     engine.TelemetryMatched
                        ? engine.ReadyForThrust
                            .ToString()
                        : "UNKNOWN") +
                    " | SRB=" +
                    engine.IsSolidBooster);
            }
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
                    StringComparison.Ordinal))
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
