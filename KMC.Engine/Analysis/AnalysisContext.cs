using System;
using System.Collections.Generic;
using KMC.Engine.Models;

namespace KMC.Engine.Analysis
{
    public sealed class AnalysisContext
    {
        private readonly List<string> _diagnostics;

        public AnalysisContext(TelemetrySnapshot telemetry, VesselModel vessel)
        {
            Telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            Vessel = vessel ?? throw new ArgumentNullException(nameof(vessel));
            Capabilities = new CapabilityModel();
            Power = new PowerModel();
            Propulsion = new PropulsionModel();
            _diagnostics = new List<string>();
        }

        public TelemetrySnapshot Telemetry { get; }
        public VesselModel Vessel { get; }
        public CapabilityModel Capabilities { get; }
        public PowerModel Power { get; }
        public PropulsionModel Propulsion { get; }
        public IReadOnlyList<string> Diagnostics => _diagnostics;

        public void AddDiagnostic(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _diagnostics.Add(message.Trim());
            }
        }
    }
}
