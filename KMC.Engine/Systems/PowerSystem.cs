using KMC.Engine.Analysis;
using KMC.Engine.Electrical;

namespace KMC.Engine.Systems
{
    public sealed class PowerSystem :
        IEngineeringSystem
    {
        private readonly ElectricalProcedureTracker _procedureTracker;

        public PowerSystem()
        {
            _procedureTracker =
                new ElectricalProcedureTracker();
        }
        public string Name
        {
            get { return "Power"; }
        }

        public int Order
        {
            get { return 200; }
        }

        public void Analyze(
            AnalysisContext context)
        {
            ElectricalNetwork network =
                ElectricalNetworkBuilder.Build(
                    context.Vessel.Topology);

            network.Storage =
                ElectricalStorageAnalyzer.Analyze(
                    context.Vessel.Topology,
                    network);

            context.Power.ElectricalNetwork =
                network;

            context.Power.Flow =
                context.Telemetry.ElectricalFlow ??
                new ElectricalFlowModel();

            context.Power.Attribution =
                context.Telemetry.ElectricalAttribution ??
                new ElectricalAttributionModel();

            context.Power.Load =
                ElectricalLoadAnalyzer.Analyze(
                    network,
                    context.Power.Flow,
                    context.Power.Attribution);

            context.Power.Diagnostic =
                ElectricalPowerDiagnosticAnalyzer.Analyze(
                    network,
                    context.Power.Flow,
                    context.Power.Load,
                    context.Power.Attribution);

            context.Power.LoadShedding =
                ElectricalLoadSheddingAnalyzer.Analyze(
                    context.Power.Flow,
                    context.Power.Load,
                    context.Power.Attribution,
                    context.Power.Diagnostic);

            context.Power.Procedure =
                _procedureTracker.Analyze(
                    context.Telemetry.ReceivedUtc,
                    context.Power.Flow,
                    context.Power.Load,
                    context.Power.Diagnostic,
                    context.Power.LoadShedding);

            context.Power.Diagnostics.Clear();

            for (int i = 0;
                 i < network.Diagnostics.Count;
                 i++)
            {
                context.Power.Diagnostics.Add(
                    network.Diagnostics[i]);
            }

            for (int i = 0;
                 i < network.Storage.Diagnostics.Count;
                 i++)
            {
                context.Power.Diagnostics.Add(
                    network.Storage.Diagnostics[i]);
            }

            if (!context.Power.Flow.TelemetryAvailable)
            {
                context.Power.Diagnostics.Add(
                    "Live systems ElectricCharge telemetry has not been received.");
            }
            else if (!context.Power.Flow.HasMeasuredNetStorageRate)
            {
                context.Power.Diagnostics.Add(
                    "Live ElectricCharge telemetry is available; flow estimator is gathering samples.");
            }
            else
            {
                context.Power.Diagnostics.Add(
                    "Measured stored-EC rate: " +
                    context.Power.Flow.NetStorageRateEcPerSecond.ToString("0.###") +
                    " EC/s.");
            }

            if (context.Power.Attribution.TelemetryAvailable)
            {
                context.Power.Diagnostics.Add(
                    "Electrical attribution: producers=" +
                    context.Power.Attribution.ProducerCount +
                    ", consumers=" +
                    context.Power.Attribution.ConsumerCount +
                    ", known current producers=" +
                    context.Power.Attribution.KnownCurrentProducerCount +
                    ", known current consumers=" +
                    context.Power.Attribution.KnownCurrentConsumerCount +
                    ".");
            }
            context.Power.Diagnostics.Add(
                "Power status: " +
                context.Power.Diagnostic.Severity +
                " / " +
                context.Power.Diagnostic.Condition +
                " - " +
                context.Power.Diagnostic.Summary);

            context.Power.Diagnostics.Add(
                "Load shedding: " +
                context.Power.LoadShedding.State +
                " - " +
                context.Power.LoadShedding.Summary);

            context.Power.Diagnostics.Add(
                "Electrical procedure: " +
                context.Power.Procedure.State +
                " / " +
                context.Power.Procedure.RecoveryState +
                " - " +
                context.Power.Procedure.PrimaryAction);

        }
    }
}
