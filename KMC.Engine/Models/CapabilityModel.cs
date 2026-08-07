using System;
using System.Collections.Generic;
using System.Text;

namespace KMC.Engine.Models
{
    /// <summary>
    /// Vessel-level engineering capability categories.
    ///
    /// These intentionally mirror the capability concepts already used by
    /// Mission Control while keeping the Engine independent of the UI project.
    /// </summary>
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

    /// <summary>
    /// Aggregate capability model produced from the vessel topology.
    ///
    /// Part counts represent the number of distinct vessel parts that provide
    /// each capability. A single multifunction part may contribute to several
    /// capability categories, but never more than once to the same category.
    /// </summary>
    public sealed class CapabilityModel
    {
        private readonly HashSet<string> _capabilities =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<VesselCapabilityType, int> _partCounts =
            new Dictionary<VesselCapabilityType, int>();

        public IReadOnlyCollection<string> Items
        {
            get { return _capabilities; }
        }

        public IReadOnlyDictionary<VesselCapabilityType, int> PartCounts
        {
            get { return _partCounts; }
        }

        public int ClassifiedPartCount { get; internal set; }

        public int UnclassifiedPartCount { get; internal set; }

        /// <summary>
        /// Compatibility path for early Engine consumers that use string
        /// capability names.
        /// </summary>
        public void Add(
            string capability)
        {
            if (!string.IsNullOrWhiteSpace(
                    capability))
            {
                _capabilities.Add(
                    capability.Trim());
            }
        }

        public void Add(
            VesselCapabilityType capability)
        {
            Add(
                capability,
                1);
        }

        internal void Add(
            VesselCapabilityType capability,
            int partCount)
        {
            if (capability ==
                VesselCapabilityType.Unknown ||
                partCount <= 0)
            {
                return;
            }

            _capabilities.Add(
                capability.ToString());

            int current;

            if (!_partCounts.TryGetValue(
                    capability,
                    out current))
            {
                current =
                    0;
            }

            _partCounts[capability] =
                current +
                partCount;
        }

        public bool Contains(
            string capability)
        {
            return
                !string.IsNullOrWhiteSpace(
                    capability) &&
                _capabilities.Contains(
                    capability.Trim());
        }

        public bool Has(
            VesselCapabilityType capability)
        {
            return GetPartCount(
                       capability) >
                   0;
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

        /// <summary>
        /// Compact deterministic diagnostic summary used by the current
        /// Mission Control verification bridge.
        /// </summary>
        public string CreateSummary()
        {
            StringBuilder builder =
                new StringBuilder();

            Array values =
                Enum.GetValues(
                    typeof(VesselCapabilityType));

            bool first =
                true;

            foreach (VesselCapabilityType capability
                in values)
            {
                if (capability ==
                    VesselCapabilityType.Unknown)
                {
                    continue;
                }

                int count =
                    GetPartCount(
                        capability);

                if (count <= 0)
                {
                    continue;
                }

                if (!first)
                {
                    builder.Append(
                        ", ");
                }

                builder.Append(
                    capability);

                builder.Append(
                    "=");

                builder.Append(
                    count);

                first =
                    false;
            }

            if (first)
            {
                return "None";
            }

            return builder.ToString();
        }
    }
}
