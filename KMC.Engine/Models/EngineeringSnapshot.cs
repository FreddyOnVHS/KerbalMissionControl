using System;
using System.Collections.Generic;

namespace KMC.Engine.Models
{
    public sealed class EngineeringSnapshot
    {
        public EngineeringSnapshot(
            long sequence,
            DateTime generatedUtc,
            VesselModel vessel,
            CapabilityModel capabilities,
            PowerModel power,
            PropulsionModel propulsion,
            IReadOnlyList<string> diagnostics)
        {
            Sequence = sequence;
            GeneratedUtc = generatedUtc;
            Vessel = vessel;
            Capabilities = capabilities;
            Power = power;
            Propulsion = propulsion;
            Diagnostics = diagnostics;
        }

        public long Sequence { get; }
        public DateTime GeneratedUtc { get; }
        public VesselModel Vessel { get; }
        public CapabilityModel Capabilities { get; }
        public PowerModel Power { get; }
        public PropulsionModel Propulsion { get; }
        public IReadOnlyList<string> Diagnostics { get; }
    }
}
