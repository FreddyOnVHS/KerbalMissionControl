using System;
using System.Collections.Generic;
using System.Linq;
using KMC.Shared.Topology;

namespace KMC.MissionControl.Rendering.Propulsion
{
    /// <summary>
    /// Assigns stable mission-lifetime engine identifiers keyed by KSP
    /// PartId. Assignments are created from the full attached engine inventory
    /// before current-stage filtering, so surviving engines are not renumbered
    /// when other stages separate.
    /// </summary>
    public static class EngineIdentifierRegistry
    {
        private static readonly object SyncRoot =
            new object();

        private static readonly Dictionary<uint, string> Identifiers =
            new Dictionary<uint, string>();

        private static string _vesselName =
            string.Empty;

        public static void RegisterInventory(
            string vesselName,
            IEnumerable<PropulsionGraphNode> engines)
        {
            if (engines == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                string normalizedVessel =
                    vesselName ??
                    string.Empty;

                if (!string.Equals(
                        _vesselName,
                        normalizedVessel,
                        StringComparison.Ordinal))
                {
                    _vesselName =
                        normalizedVessel;

                    Identifiers.Clear();
                }

                List<PropulsionGraphNode> inventory =
                    engines
                        .Where(
                            node => node != null)
                        .ToList();

                IEnumerable<IGrouping<string, PropulsionGraphNode>> groups =
                    inventory
                        .GroupBy(
                            CreatePrefix,
                            StringComparer.Ordinal);

                foreach (
                    IGrouping<string, PropulsionGraphNode> group
                    in groups)
                {
                    List<PropulsionGraphNode> ordered =
                        group
                            .OrderByDescending(
                                node => node.ActivationStage)
                            .ThenBy(
                                node => NormalizeAngle(
                                    Math.Atan2(
                                        -node.VesselZ,
                                        node.VesselX)))
                            .ThenBy(
                                node => node.PartId)
                            .ToList();

                    int nextNumber =
                        FindNextNumber(
                            group.Key);

                    for (int index = 0;
                         index < ordered.Count;
                         index++)
                    {
                        PropulsionGraphNode node =
                            ordered[index];

                        if (Identifiers.ContainsKey(
                                node.PartId))
                        {
                            continue;
                        }

                        Identifiers[node.PartId] =
                            group.Key +
                            nextNumber.ToString("00");

                        nextNumber++;
                    }
                }
            }
        }

        public static string GetIdentifier(
            uint partId,
            string fallbackName,
            int fallbackNumber)
        {
            lock (SyncRoot)
            {
                string identifier;

                if (Identifiers.TryGetValue(
                        partId,
                        out identifier))
                {
                    return identifier;
                }
            }

            return CreatePrefix(
                    fallbackName) +
                Math.Max(
                    1,
                    fallbackNumber)
                .ToString("00");
        }

        private static int FindNextNumber(
            string prefix)
        {
            int maximum =
                0;

            foreach (string value in
                Identifiers.Values)
            {
                if (!value.StartsWith(
                        prefix,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                int number;

                if (int.TryParse(
                        value.Substring(
                            prefix.Length),
                        out number))
                {
                    maximum =
                        Math.Max(
                            maximum,
                            number);
                }
            }

            return maximum + 1;
        }

        private static string CreatePrefix(
            PropulsionGraphNode node)
        {
            return node == null
                ? "E"
                : CreatePrefix(
                    CreateEngineName(
                        node));
        }

        private static string CreatePrefix(
            string name)
        {
            if (string.IsNullOrWhiteSpace(
                    name))
            {
                return "E";
            }

            string upper =
                name.Trim()
                    .ToUpperInvariant();

            if (upper.StartsWith(
                    "THUMPER",
                    StringComparison.Ordinal))
            {
                return "T";
            }

            if (upper.StartsWith(
                    "KICKBACK",
                    StringComparison.Ordinal))
            {
                return "K";
            }

            if (upper.StartsWith(
                    "SEPARATRON",
                    StringComparison.Ordinal))
            {
                return "S";
            }

            if (upper.StartsWith(
                    "SKIPPER",
                    StringComparison.Ordinal))
            {
                return "SK";
            }

            if (upper.StartsWith(
                    "TERRIER",
                    StringComparison.Ordinal))
            {
                return "TR";
            }

            if (upper.StartsWith(
                    "SWIVEL",
                    StringComparison.Ordinal))
            {
                return "SW";
            }

            if (upper.StartsWith(
                    "RELIANT",
                    StringComparison.Ordinal))
            {
                return "R";
            }

            return upper.Substring(
                0,
                Math.Min(
                    2,
                    upper.Length));
        }

        private static string CreateEngineName(
            PropulsionGraphNode node)
        {
            string title =
                node.Title ??
                string.Empty;

            int quoteStart =
                title.IndexOf('"');

            if (quoteStart >= 0)
            {
                int quoteEnd =
                    title.IndexOf(
                        '"',
                        quoteStart + 1);

                if (quoteEnd >
                    quoteStart)
                {
                    return title.Substring(
                            quoteStart + 1,
                            quoteEnd -
                            quoteStart -
                            1)
                        .ToUpperInvariant();
                }
            }

            string[] words =
                title.Split(
                    new[] { ' ' },
                    StringSplitOptions
                        .RemoveEmptyEntries);

            if (words.Length > 0)
            {
                return words[0]
                    .ToUpperInvariant();
            }

            return node.Category ==
                VesselNodeCategory.SolidBooster
                    ? "BOOSTER"
                    : "ENGINE";
        }

        private static double NormalizeAngle(
            double angle)
        {
            while (angle <
                   0.0)
            {
                angle +=
                    Math.PI *
                    2.0;
            }

            while (angle >=
                   Math.PI *
                   2.0)
            {
                angle -=
                    Math.PI *
                    2.0;
            }

            return angle;
        }
    }
}
