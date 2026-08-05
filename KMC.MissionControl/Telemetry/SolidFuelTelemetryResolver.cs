using System;
using System.Collections.Generic;
using KMC.MissionControl.Debugging;
using KMC.Shared.Topology;

namespace KMC.MissionControl.Telemetry
{
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

            List<BoosterResource> boosters =
                new List<BoosterResource>();

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

                BoosterResource booster =
                    new BoosterResource
                    {
                        VesselX =
                            node.VesselX
                    };

                if (node.Resources != null)
                {
                    for (int resourceIndex = 0;
                         resourceIndex <
                            node.Resources.Count;
                         resourceIndex++)
                    {
                        VesselResourceState resource =
                            node.Resources[
                                resourceIndex];

                        if (resource != null &&
                            string.Equals(
                                resource.Name,
                                "SolidFuel",
                                StringComparison
                                    .OrdinalIgnoreCase))
                        {
                            booster.Amount =
                                Math.Max(
                                    0.0,
                                    resource.Amount);

                            booster.Capacity =
                                Math.Max(
                                    0.0,
                                    resource.Capacity);
                        }
                    }
                }

                boosters.Add(
                    booster);
            }

            boosters.Sort(
                delegate(
                    BoosterResource left,
                    BoosterResource right)
                {
                    return left.VesselX
                        .CompareTo(
                            right.VesselX);
                });

            result.TimestampUtc =
                DateTime.UtcNow;

            result.BoosterCount =
                boosters.Count;

            for (int index = 0;
                 index < boosters.Count;
                 index++)
            {
                result.TotalAmount +=
                    boosters[index].Amount;

                result.TotalCapacity +=
                    boosters[index].Capacity;
            }

            if (boosters.Count > 0)
            {
                result.LeftAmount =
                    boosters[0].Amount;

                result.LeftCapacity =
                    boosters[0].Capacity;
            }

            if (boosters.Count > 1)
            {
                BoosterResource right =
                    boosters[
                        boosters.Count - 1];

                result.RightAmount =
                    right.Amount;

                result.RightCapacity =
                    right.Capacity;
            }

            return result;
        }

        private sealed class BoosterResource
        {
            public double VesselX;
            public double Amount;
            public double Capacity;
        }
    }
}
