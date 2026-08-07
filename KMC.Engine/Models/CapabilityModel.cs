using System;
using System.Collections.Generic;
using System.Text;
using KMC.Engine.Capabilities;

namespace KMC.Engine.Models
{
    public enum VesselCapabilityType
    {
        Unknown = 0,
        Command,
        CrewSupport,
        ElectricalStorage,
        ElectricalProducer,
        ResourceStorage,
        ResourceConsumer,
        Propulsion,
        ReactionControl,
        AttitudeControl,
        Communication,
        Science,
        Docking,
        Separation,
        Structural
    }

    public sealed class CapabilityModel
    {
        private readonly HashSet<string> _capabilities =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<VesselCapabilityType, int> _partCounts =
            new Dictionary<VesselCapabilityType, int>();

        public CapabilityModel()
        {
            Details =
                new VesselCapabilitySnapshot();
        }

        public IReadOnlyCollection<string> Items
        {
            get { return _capabilities; }
        }

        public IReadOnlyDictionary<VesselCapabilityType, int> PartCounts
        {
            get { return _partCounts; }
        }

        public VesselCapabilitySnapshot Details
        {
            get;
            internal set;
        }

        public int ClassifiedPartCount { get; internal set; }

        public int UnclassifiedPartCount { get; internal set; }

        public void Add(
            string capability)
        {
            if (!string.IsNullOrWhiteSpace(capability))
            {
                _capabilities.Add(capability.Trim());
            }
        }

        internal void AddPartCapability(
            VesselCapabilityType capability)
        {
            if (capability == VesselCapabilityType.Unknown)
            {
                return;
            }

            _capabilities.Add(capability.ToString());

            int current;

            if (!_partCounts.TryGetValue(capability, out current))
            {
                current = 0;
            }

            _partCounts[capability] = current + 1;
        }

        public bool Contains(
            string capability)
        {
            return
                !string.IsNullOrWhiteSpace(capability) &&
                _capabilities.Contains(capability.Trim());
        }

        public bool Has(
            VesselCapabilityType capability)
        {
            return GetPartCount(capability) > 0;
        }

        public int GetPartCount(
            VesselCapabilityType capability)
        {
            int count;

            return _partCounts.TryGetValue(
                    capability,
                    out count)
                ? count
                : 0;
        }

        public string CreateSummary()
        {
            StringBuilder builder =
                new StringBuilder();

            Array values =
                Enum.GetValues(
                    typeof(VesselCapabilityType));

            bool first = true;

            foreach (VesselCapabilityType capability in values)
            {
                if (capability == VesselCapabilityType.Unknown)
                {
                    continue;
                }

                int count = GetPartCount(capability);

                if (count <= 0)
                {
                    continue;
                }

                if (!first)
                {
                    builder.Append(", ");
                }

                builder.Append(capability);
                builder.Append("=");
                builder.Append(count);

                first = false;
            }

            return first
                ? "None"
                : builder.ToString();
        }
    }
}
