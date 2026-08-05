using System;
using KMC.MissionControl.Debugging;
using KMC.Shared.Topology;

namespace KMC.MissionControl.Telemetry
{
    /// <summary>
    /// Supplies the PROP renderer with solid-fuel information.
    ///
    /// Live side-channel telemetry is preferred when it is current. The
    /// already-received vessel topology is used as a fallback so attached
    /// boosters appear immediately even before the live sender is confirmed.
    /// </summary>
    public static class SolidFuelTelemetryResolver
    {
        private static readonly TimeSpan LiveTimeout =
            TimeSpan.FromSeconds(
                2.0);

        public static SolidFuelTelemetrySnapshot GetSnapshot()
        {
            SolidFuelTelemetrySnapshot live =
                SolidFuelTelemetryStore.GetSnapshot();

            if (live != null &&
                live.TimestampUtc !=
                    default(DateTime) &&
                DateTime.UtcNow -
                    live.TimestampUtc <=
                    LiveTimeout &&
                live.BoosterCount > 0)
            {
                return live;
            }

            return ReadTopologyFallback();
        }

        private static SolidFuelTelemetrySnapshot
            ReadTopologyFallback()
        {
            SolidFuelTelemetrySnapshot result =
                new SolidFuelTelemetrySnapshot();

            VesselTopology topology =
                PropulsionDebugSnapshotStore
                    .GetTopology();

            if (topology == null ||
                topology.Nodes == null)
            {
                return result;
            }

            result.TimestampUtc =
                DateTime.UtcNow;

            for (int nodeIndex = 0;
                 nodeIndex < topology.Nodes.Count;
                 nodeIndex++)
            {
                VesselTopologyNode node =
                    topology.Nodes[nodeIndex];

                if (node == null ||
                    node.Category !=
                        VesselNodeCategory.SolidBooster)
                {
                    continue;
                }

                result.BoosterCount++;

                if (node.Resources == null)
                {
                    continue;
                }

                for (int resourceIndex = 0;
                     resourceIndex <
                        node.Resources.Count;
                     resourceIndex++)
                {
                    VesselResourceState resource =
                        node.Resources[
                            resourceIndex];

                    if (resource == null ||
                        !string.Equals(
                            resource.Name,
                            "SolidFuel",
                            StringComparison
                                .OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    result.TotalAmount +=
                        Math.Max(
                            0.0,
                            resource.Amount);

                    result.TotalCapacity +=
                        Math.Max(
                            0.0,
                            resource.Capacity);
                }
            }

            /*
             * Topology packets describe structure and their resource values
             * are only refreshed when topology changes. They intentionally do
             * not claim that a booster is currently burning. The live sender
             * supplies burning state and smoothly decreasing fuel quantities.
             */
            result.ActiveAmount =
                0.0;

            result.ActiveCapacity =
                0.0;

            result.BurningBoosterCount =
                0;

            return result;
        }
    }
}
