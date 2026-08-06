using System;
using System.Collections.Generic;
using KMC.Engine.Models;

namespace KMC.Engine.Analysis
{
    public sealed class AnalysisPipelineResult
    {
        public AnalysisPipelineResult(EngineeringSnapshot snapshot, IReadOnlyList<string> executedSystems)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            ExecutedSystems = executedSystems ?? throw new ArgumentNullException(nameof(executedSystems));
        }

        public EngineeringSnapshot Snapshot { get; }
        public IReadOnlyList<string> ExecutedSystems { get; }
    }
}
