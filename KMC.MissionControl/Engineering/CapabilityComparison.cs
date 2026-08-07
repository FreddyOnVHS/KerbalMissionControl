using System;
using System.Collections.Generic;
using System.Text;
using KMC.Engine.Models;
using KMC.MissionControl.Capabilities;
using KMC.Shared.Topology;

namespace KMC.MissionControl.Engineering
{
    /// <summary>
    /// Temporary migration verifier.
    ///
    /// Compares the existing Mission Control capability classifier with the
    /// independent KMC.Engine vessel capability model for the same topology.
    /// This class is diagnostic only and must not influence either model.
    /// </summary>
    internal static class CapabilityComparison
    {
        public static string Compare(
            VesselTopology topology,
            CapabilityModel engineModel)
        {
            if (topology == null)
            {
                return
                    "KMC.Engine CAPABILITY COMPARE | SKIPPED | Topology unavailable";
            }

            if (engineModel == null)
            {
                return
                    "KMC.Engine CAPABILITY COMPARE | SKIPPED | Engine model unavailable";
            }

            VesselCapabilitySnapshot legacy =
                VesselCapabilityBuilder.Build(
                    topology);

            Dictionary<PartCapabilityType, int> legacyCounts =
                CountLegacyCapabilities(
                    legacy);

            List<string> mismatches =
                new List<string>();

            CompareCapability(
                PartCapabilityType.Command,
                VesselCapabilityType.Command,
                legacyCounts,
                engineModel,
                mismatches);

            CompareCapability(
                PartCapabilityType.CrewSupport,
                VesselCapabilityType.CrewSupport,
                legacyCounts,
                engineModel,
                mismatches);

            CompareCapability(
                PartCapabilityType.ElectricalStorage,
                VesselCapabilityType.ElectricalStorage,
                legacyCounts,
                engineModel,
                mismatches);

            CompareCapability(
                PartCapabilityType.ElectricalProducer,
                VesselCapabilityType.ElectricalProducer,
                legacyCounts,
                engineModel,
                mismatches);

            CompareCapability(
                PartCapabilityType.ResourceStorage,
                VesselCapabilityType.ResourceStorage,
                legacyCounts,
                engineModel,
                mismatches);

            CompareCapability(
                PartCapabilityType.ResourceConsumer,
                VesselCapabilityType.ResourceConsumer,
                legacyCounts,
                engineModel,
                mismatches);

            CompareCapability(
                PartCapabilityType.Propulsion,
                VesselCapabilityType.Propulsion,
                legacyCounts,
                engineModel,
                mismatches);

            CompareCapability(
                PartCapabilityType.ReactionControl,
                VesselCapabilityType.ReactionControl,
                legacyCounts,
                engineModel,
                mismatches);

            CompareCapability(
                PartCapabilityType.AttitudeControl,
                VesselCapabilityType.AttitudeControl,
                legacyCounts,
                engineModel,
                mismatches);

            CompareCapability(
                PartCapabilityType.Communication,
                VesselCapabilityType.Communication,
                legacyCounts,
                engineModel,
                mismatches);

            CompareCapability(
                PartCapabilityType.Science,
                VesselCapabilityType.Science,
                legacyCounts,
                engineModel,
                mismatches);

            CompareCapability(
                PartCapabilityType.Docking,
                VesselCapabilityType.Docking,
                legacyCounts,
                engineModel,
                mismatches);

            CompareCapability(
                PartCapabilityType.Separation,
                VesselCapabilityType.Separation,
                legacyCounts,
                engineModel,
                mismatches);

            CompareCapability(
                PartCapabilityType.Structural,
                VesselCapabilityType.Structural,
                legacyCounts,
                engineModel,
                mismatches);

            int legacyClassified =
                CountLegacyClassifiedParts(
                    legacy);

            int legacyUnclassified =
                legacy.Parts.Count -
                legacyClassified;

            if (legacyClassified !=
                engineModel.ClassifiedPartCount)
            {
                mismatches.Add(
                    "ClassifiedParts legacy=" +
                    legacyClassified +
                    " engine=" +
                    engineModel.ClassifiedPartCount);
            }

            if (legacyUnclassified !=
                engineModel.UnclassifiedPartCount)
            {
                mismatches.Add(
                    "UnclassifiedParts legacy=" +
                    legacyUnclassified +
                    " engine=" +
                    engineModel.UnclassifiedPartCount);
            }

            if (mismatches.Count ==
                0)
            {
                return
                    "KMC.Engine CAPABILITY COMPARE | MATCH | " +
                    "TopologyRevision=" +
                    topology.Revision +
                    " | Parts=" +
                    legacy.Parts.Count;
            }

            StringBuilder builder =
                new StringBuilder();

            builder.Append(
                "KMC.Engine CAPABILITY COMPARE | MISMATCH | ");

            builder.Append(
                "TopologyRevision=");

            builder.Append(
                topology.Revision);

            builder.Append(
                " | ");

            for (int index = 0;
                 index < mismatches.Count;
                 index++)
            {
                if (index > 0)
                {
                    builder.Append(
                        "; ");
                }

                builder.Append(
                    mismatches[index]);
            }

            return builder.ToString();
        }

        private static Dictionary<PartCapabilityType, int>
            CountLegacyCapabilities(
                VesselCapabilitySnapshot legacy)
        {
            Dictionary<PartCapabilityType, int> counts =
                new Dictionary<PartCapabilityType, int>();

            for (int partIndex = 0;
                 partIndex < legacy.Parts.Count;
                 partIndex++)
            {
                PartCapabilitySnapshot part =
                    legacy.Parts[partIndex];

                if (part == null)
                {
                    continue;
                }

                HashSet<PartCapabilityType> unique =
                    new HashSet<PartCapabilityType>();

                for (int capabilityIndex = 0;
                     capabilityIndex < part.Capabilities.Count;
                     capabilityIndex++)
                {
                    PartCapability capability =
                        part.Capabilities[capabilityIndex];

                    if (capability == null ||
                        capability.Type ==
                            PartCapabilityType.Unknown)
                    {
                        continue;
                    }

                    unique.Add(
                        capability.Type);
                }

                foreach (PartCapabilityType type
                    in unique)
                {
                    int current;

                    if (!counts.TryGetValue(
                            type,
                            out current))
                    {
                        current =
                            0;
                    }

                    counts[type] =
                        current +
                        1;
                }
            }

            return counts;
        }

        private static int CountLegacyClassifiedParts(
            VesselCapabilitySnapshot legacy)
        {
            int count =
                0;

            for (int partIndex = 0;
                 partIndex < legacy.Parts.Count;
                 partIndex++)
            {
                PartCapabilitySnapshot part =
                    legacy.Parts[partIndex];

                if (part == null)
                {
                    continue;
                }

                bool classified =
                    false;

                for (int capabilityIndex = 0;
                     capabilityIndex < part.Capabilities.Count;
                     capabilityIndex++)
                {
                    PartCapability capability =
                        part.Capabilities[capabilityIndex];

                    if (capability != null &&
                        capability.Type !=
                            PartCapabilityType.Unknown)
                    {
                        classified =
                            true;

                        break;
                    }
                }

                if (classified)
                {
                    count++;
                }
            }

            return count;
        }

        private static void CompareCapability(
            PartCapabilityType legacyType,
            VesselCapabilityType engineType,
            Dictionary<PartCapabilityType, int> legacyCounts,
            CapabilityModel engineModel,
            List<string> mismatches)
        {
            int legacyCount =
                0;

            legacyCounts.TryGetValue(
                legacyType,
                out legacyCount);

            int engineCount =
                engineModel.GetPartCount(
                    engineType);

            if (legacyCount ==
                engineCount)
            {
                return;
            }

            mismatches.Add(
                engineType +
                " legacy=" +
                legacyCount +
                " engine=" +
                engineCount);
        }
    }
}
