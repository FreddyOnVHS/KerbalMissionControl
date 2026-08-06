using System;
using KMC.Engine.Analysis;
using KMC.Engine.Models;
using KMC.Engine.Systems;
using KMC.Shared.Topology;

namespace KMC.Engine
{
    public sealed class EngineeringEngine
    {
        private readonly AnalysisPipeline _pipeline;

        public EngineeringEngine()
            : this(new AnalysisPipeline(new IEngineeringSystem[]
            {
                new CapabilitySystem(),
                new PowerSystem(),
                new PropulsionSystem()
            }))
        {
        }

        public EngineeringEngine(AnalysisPipeline pipeline)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        }

        public AnalysisPipelineResult Analyze(long sequence, DateTime receivedUtc, object telemetryPacket, VesselTopology topology)
        {
            var telemetry = new TelemetrySnapshot(sequence, receivedUtc, telemetryPacket);
            var vessel = new VesselModel(topology);
            return _pipeline.Execute(telemetry, vessel);
        }
    }
}
