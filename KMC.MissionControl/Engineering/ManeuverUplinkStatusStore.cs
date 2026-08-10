using System;
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

        public static void Clear()
        {
            lock (SyncRoot)
            {
                _latest =
                    new ManeuverUplinkStatusSnapshot();
            }
        }

        public static void PublishPending(
            string planId,
            double nodeUt)
        {
            lock (SyncRoot)
            {
                _latest =
                    new ManeuverUplinkStatusSnapshot
                    {
                        State = "AWAITING ACK",
                        PlanId = planId ?? string.Empty,
                        Detail = "UPLINK SENT TO KSP PLUGIN",
                        NodeUniversalTimeSeconds = nodeUt,
                        UpdatedUtc = DateTime.UtcNow
                    };
            }
        }

        public static void PublishRejected(
            string planId,
            string detail)
        {
            lock (SyncRoot)
            {
                _latest =
                    new ManeuverUplinkStatusSnapshot
                    {
                        State = "NOT SENT",
                        PlanId = planId ?? string.Empty,
                        Detail = detail ?? string.Empty,
                        NodeUniversalTimeSeconds = double.NaN,
                        UpdatedUtc = DateTime.UtcNow
                    };
            }
        }

        public static void PublishAck(
            ManeuverUplinkAck ack)
        {
            if (ack == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                _latest =
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
            }
        }

        public static void PublishNodeState(
            ManeuverNodeStatePacket packet)
        {
            if (packet == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                _latest =
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
            }
        }

        public static ManeuverUplinkStatusSnapshot GetLatest()
        {
            lock (SyncRoot)
            {
                return
                    new ManeuverUplinkStatusSnapshot
                    {
                        State = _latest.State,
                        PlanId = _latest.PlanId,
                        Detail = _latest.Detail,
                        NodeStateTelemetryAvailable =
                            _latest.NodeStateTelemetryAvailable,
                        NodeExists = _latest.NodeExists,
                        NodeUniversalTimeSeconds =
                            _latest.NodeUniversalTimeSeconds,
                        ProgradeDeltaVMetersPerSecond =
                            _latest.ProgradeDeltaVMetersPerSecond,
                        NormalDeltaVMetersPerSecond =
                            _latest.NormalDeltaVMetersPerSecond,
                        RadialDeltaVMetersPerSecond =
                            _latest.RadialDeltaVMetersPerSecond,
                        UpdatedUtc = _latest.UpdatedUtc
                    };
            }
        }
    }
}
