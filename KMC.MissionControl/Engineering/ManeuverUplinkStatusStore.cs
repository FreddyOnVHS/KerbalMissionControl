using System;
using System.Collections.Generic;
using KMC.Engine.Guidance;
using KMC.Engine.Maneuver;
using KMC.Shared;

namespace KMC.MissionControl.Engineering
{
    public sealed class ManeuverUplinkStatusSnapshot
    {
        public string State { get; set; }
        public string PlanId { get; set; }
        public string Detail { get; set; }
        public bool NodeStateTelemetryAvailable { get; set; }
        public bool NodeExists { get; set; }
        public double NodeUniversalTimeSeconds { get; set; }
        public double ProgradeDeltaVMetersPerSecond { get; set; }
        public double NormalDeltaVMetersPerSecond { get; set; }
        public double RadialDeltaVMetersPerSecond { get; set; }
        public DateTime UpdatedUtc { get; set; }

        public ManeuverUplinkStatusSnapshot()
        {
            State = "IDLE";
            PlanId = string.Empty;
            Detail = "PLAN HAS NOT BEEN UPLINKED";
            NodeUniversalTimeSeconds = double.NaN;
            ProgradeDeltaVMetersPerSecond = double.NaN;
            NormalDeltaVMetersPerSecond = double.NaN;
            RadialDeltaVMetersPerSecond = double.NaN;
            UpdatedUtc = DateTime.MinValue;
        }
    }

    public static class ManeuverUplinkStatusStore
    {
        private static readonly object SyncRoot =
            new object();

        private static ManeuverUplinkStatusSnapshot _latest =
            new ManeuverUplinkStatusSnapshot();

        /*
         * Build 13.9:
         * Multiple KSP nodes now report state concurrently. Preserve one
         * immutable status per PlanId so MNV/GUID review never depends on
         * whichever tracked-node packet happened to arrive last.
         */
        private static readonly Dictionary<string, ManeuverUplinkStatusSnapshot>
            ByPlan =
                new Dictionary<string, ManeuverUplinkStatusSnapshot>(
                    StringComparer.Ordinal);

        public static void Clear()
        {
            lock (SyncRoot)
            {
                _latest =
                    new ManeuverUplinkStatusSnapshot();

                ByPlan.Clear();
            }

            GuidanceNodeStateStore.Clear();
        }

        public static void PublishPending(
            string planId,
            double nodeUt)
        {
            ManeuverUplinkStatusSnapshot snapshot =
                new ManeuverUplinkStatusSnapshot
                {
                    State = "AWAITING ACK",
                    PlanId = planId ?? string.Empty,
                    Detail = "UPLINK SENT TO KSP PLUGIN",
                    NodeUniversalTimeSeconds = nodeUt,
                    UpdatedUtc = DateTime.UtcNow
                };

            PublishSnapshot(snapshot);
            PublishGuidanceState(snapshot, true);
        }

        public static void PublishRejected(
            string planId,
            string detail)
        {
            ManeuverUplinkStatusSnapshot snapshot =
                new ManeuverUplinkStatusSnapshot
                {
                    State = "NOT SENT",
                    PlanId = planId ?? string.Empty,
                    Detail = detail ?? string.Empty,
                    NodeUniversalTimeSeconds = double.NaN,
                    UpdatedUtc = DateTime.UtcNow
                };

            PublishSnapshot(snapshot);
            PublishGuidanceState(snapshot, true);
        }

        public static void PublishAck(
            ManeuverUplinkAck ack)
        {
            if (ack == null)
            {
                return;
            }

            ManeuverUplinkStatusSnapshot snapshot =
                new ManeuverUplinkStatusSnapshot
                {
                    State =
                        string.IsNullOrWhiteSpace(ack.Status)
                            ? "ACK RECEIVED"
                            : ack.Status,

                    PlanId =
                        ack.PlanId ?? string.Empty,

                    Detail =
                        ack.Detail ?? string.Empty,

                    NodeUniversalTimeSeconds =
                        ack.NodeUniversalTimeSeconds,

                    UpdatedUtc =
                        DateTime.UtcNow
                };

            PublishSnapshot(snapshot);
            PublishGuidanceState(snapshot, true);
        }

        public static void PublishNodeState(
            ManeuverNodeStatePacket packet)
        {
            if (packet == null)
            {
                return;
            }

            ManeuverUplinkStatusSnapshot snapshot =
                new ManeuverUplinkStatusSnapshot
                {
                    State =
                        string.IsNullOrWhiteSpace(packet.State)
                            ? "NODE STATE RECEIVED"
                            : packet.State,

                    PlanId =
                        packet.PlanId ?? string.Empty,

                    Detail =
                        packet.Detail ?? string.Empty,

                    NodeStateTelemetryAvailable =
                        true,

                    NodeExists =
                        packet.NodeExists,

                    NodeUniversalTimeSeconds =
                        packet.NodeUniversalTimeSeconds,

                    ProgradeDeltaVMetersPerSecond =
                        packet.ProgradeDeltaVMetersPerSecond,

                    NormalDeltaVMetersPerSecond =
                        packet.NormalDeltaVMetersPerSecond,

                    RadialDeltaVMetersPerSecond =
                        packet.RadialDeltaVMetersPerSecond,

                    UpdatedUtc =
                        DateTime.UtcNow
                };

            PublishSnapshot(snapshot);
            PublishGuidanceState(snapshot, true);
        }

        public static ManeuverUplinkStatusSnapshot GetLatest()
        {
            lock (SyncRoot)
            {
                return Clone(_latest);
            }
        }

        public static ManeuverUplinkStatusSnapshot GetForPlan(
            string planId)
        {
            lock (SyncRoot)
            {
                ManeuverUplinkStatusSnapshot snapshot;

                if (!string.IsNullOrWhiteSpace(planId) &&
                    ByPlan.TryGetValue(
                        planId,
                        out snapshot))
                {
                    return
                        Clone(snapshot);
                }

                ManeuverUplinkStatusSnapshot empty =
                    new ManeuverUplinkStatusSnapshot();

                empty.PlanId =
                    planId ?? string.Empty;

                return empty;
            }
        }

        public static void RepublishGuidanceForPlan(
            string planId)
        {
            ManeuverUplinkStatusSnapshot snapshot =
                GetForPlan(
                    planId);

            if (snapshot == null ||
                string.IsNullOrWhiteSpace(
                    snapshot.PlanId) ||
                snapshot.UpdatedUtc == DateTime.MinValue)
            {
                return;
            }

            PublishGuidanceState(
                snapshot,
                true);
        }

        private static void PublishSnapshot(
            ManeuverUplinkStatusSnapshot snapshot)
        {
            lock (SyncRoot)
            {
                _latest =
                    Clone(snapshot);

                if (snapshot != null &&
                    !string.IsNullOrWhiteSpace(
                        snapshot.PlanId))
                {
                    ByPlan[snapshot.PlanId] =
                        Clone(snapshot);
                }
            }
        }

        private static void PublishGuidanceState(
            ManeuverUplinkStatusSnapshot snapshot,
            bool available)
        {
            if (snapshot == null)
            {
                return;
            }

            /*
             * Build 13.9 selection safety:
             * Once Engine has an active PlanId, unrelated tracked-node packets
             * are retained in ByPlan but may not replace Guidance's active
             * node-verification state.
             */
            string activePlanId =
                ManeuverPlanPromotionStore.GetActivePlanId();

            if (!string.IsNullOrWhiteSpace(
                    activePlanId) &&
                !string.Equals(
                    activePlanId,
                    snapshot.PlanId ?? string.Empty,
                    StringComparison.Ordinal))
            {
                return;
            }

            GuidanceNodeStateStore.Publish(
                new GuidanceNodeStateModel
                {
                    Available = available,
                    PlanId = snapshot.PlanId ?? string.Empty,
                    State = snapshot.State ?? string.Empty,
                    Detail = snapshot.Detail ?? string.Empty,
                    NodeExists = snapshot.NodeExists,
                    NodeUniversalTimeSeconds =
                        snapshot.NodeUniversalTimeSeconds,
                    ProgradeDeltaVMetersPerSecond =
                        snapshot.ProgradeDeltaVMetersPerSecond,
                    NormalDeltaVMetersPerSecond =
                        snapshot.NormalDeltaVMetersPerSecond,
                    RadialDeltaVMetersPerSecond =
                        snapshot.RadialDeltaVMetersPerSecond,
                    ReceivedUtc = DateTime.UtcNow
                });
        }

        private static ManeuverUplinkStatusSnapshot Clone(
            ManeuverUplinkStatusSnapshot source)
        {
            if (source == null)
            {
                return new ManeuverUplinkStatusSnapshot();
            }

            return
                new ManeuverUplinkStatusSnapshot
                {
                    State = source.State,
                    PlanId = source.PlanId,
                    Detail = source.Detail,
                    NodeStateTelemetryAvailable =
                        source.NodeStateTelemetryAvailable,
                    NodeExists = source.NodeExists,
                    NodeUniversalTimeSeconds =
                        source.NodeUniversalTimeSeconds,
                    ProgradeDeltaVMetersPerSecond =
                        source.ProgradeDeltaVMetersPerSecond,
                    NormalDeltaVMetersPerSecond =
                        source.NormalDeltaVMetersPerSecond,
                    RadialDeltaVMetersPerSecond =
                        source.RadialDeltaVMetersPerSecond,
                    UpdatedUtc = source.UpdatedUtc
                };
        }
    }
}
