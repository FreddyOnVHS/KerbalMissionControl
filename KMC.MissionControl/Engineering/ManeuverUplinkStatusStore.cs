using System;
using KMC.Shared;

namespace KMC.MissionControl.Engineering
{
    public sealed class ManeuverUplinkStatusSnapshot
    {
        public string State { get; set; }
        public string PlanId { get; set; }
        public string Detail { get; set; }
        public double NodeUniversalTimeSeconds { get; set; }
        public DateTime UpdatedUtc { get; set; }

        public ManeuverUplinkStatusSnapshot()
        {
            State = "IDLE";
            PlanId = string.Empty;
            Detail = "PLAN HAS NOT BEEN UPLINKED";
            NodeUniversalTimeSeconds = double.NaN;
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
            Publish(
                "AWAITING ACK",
                planId,
                "UPLINK SENT TO KSP PLUGIN",
                nodeUt);
        }

        public static void PublishRejected(
            string planId,
            string detail)
        {
            Publish(
                "NOT SENT",
                planId,
                detail,
                double.NaN);
        }

        public static void PublishAck(
            ManeuverUplinkAck ack)
        {
            if (ack == null)
            {
                return;
            }

            Publish(
                string.IsNullOrWhiteSpace(ack.Status)
                    ? "ACK RECEIVED"
                    : ack.Status,
                ack.PlanId,
                ack.Detail,
                ack.NodeUniversalTimeSeconds);
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
                        NodeUniversalTimeSeconds =
                            _latest.NodeUniversalTimeSeconds,
                        UpdatedUtc = _latest.UpdatedUtc
                    };
            }
        }

        private static void Publish(
            string state,
            string planId,
            string detail,
            double nodeUt)
        {
            lock (SyncRoot)
            {
                _latest =
                    new ManeuverUplinkStatusSnapshot
                    {
                        State = state ?? string.Empty,
                        PlanId = planId ?? string.Empty,
                        Detail = detail ?? string.Empty,
                        NodeUniversalTimeSeconds = nodeUt,
                        UpdatedUtc = DateTime.UtcNow
                    };
            }
        }
    }
}
