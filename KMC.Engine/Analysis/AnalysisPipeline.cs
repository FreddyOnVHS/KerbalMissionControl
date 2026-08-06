using System;
using System.Collections.Generic;
using System.Linq;
using KMC.Engine.Models;

namespace KMC.Engine.Analysis
{
    public sealed class AnalysisPipeline
    {
        private readonly List<IEngineeringSystem> _systems;

        public AnalysisPipeline(IEnumerable<IEngineeringSystem> systems)
        {
            if (systems == null) throw new ArgumentNullException(nameof(systems));

            _systems = systems
                .Where(system => system != null)
                .OrderBy(system => system.Order)
                .ThenBy(system => system.Name, StringComparer.Ordinal)
                .ToList();
        }

        public AnalysisPipelineResult Execute(TelemetrySnapshot telemetry, VesselModel vessel)
        {
            var context = new AnalysisContext(telemetry, vessel);
            var executed = new List<string>();

            foreach (var system in _systems)
            {
                system.Analyze(context);
                executed.Add(system.Name);
            }

            var snapshot = new EngineeringSnapshot(
                telemetry.Sequence,
                telemetry.ReceivedUtc,
                vessel,
                context.Capabilities,
                context.Power,
                context.Propulsion,
                context.Diagnostics);

            return new AnalysisPipelineResult(snapshot, executed.AsReadOnly());
        }
    }
}
