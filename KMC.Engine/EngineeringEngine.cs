using System;
using KMC.Engine.Analysis;
using KMC.Engine.Electrical;
using KMC.Engine.Models;
using KMC.Engine.Systems;
using KMC.Shared.Topology;

namespace KMC.Engine
{
    public sealed class EngineeringEngine
    {
        private readonly AnalysisPipeline _pipeline;
        private readonly ElectricalFlowTracker _electricalFlowTracker;

        public EngineeringEngine()
            : this(
                new AnalysisPipeline(
                    new IEngineeringSystem[]
                    {
                        new CapabilitySystem(),
                        new PowerSystem(),
                        new PropulsionSystem()
                    }))
        {
        }

        public EngineeringEngine(
            AnalysisPipeline pipeline)
        {
            if (pipeline == null)
            {
                throw new ArgumentNullException(
                    nameof(pipeline));
            }

            _pipeline =
                pipeline;

            _electricalFlowTracker =
                new ElectricalFlowTracker();
        }

        public void PublishElectricalTelemetry(
            double storedEc,
            double capacityEc,
            DateTime receivedUtc)
        {
            _electricalFlowTracker.AddSample(
                storedEc,
                capacityEc,
                receivedUtc);
        }

        public void ClearElectricalTelemetry()
        {
            _electricalFlowTracker.Clear();
        }

        public AnalysisPipelineResult Analyze(
            long sequence,
            DateTime receivedUtc,
            object telemetryPacket,
            VesselTopology topology)
        {
            TelemetrySnapshot telemetry =
                new TelemetrySnapshot(
                    sequence,
                    receivedUtc,
                    telemetryPacket,
                    _electricalFlowTracker.GetLatest());

            VesselModel vessel =
                new VesselModel(
                    topology);

            return
                _pipeline.Execute(
                    telemetry,
                    vessel);
        }
    }
}
