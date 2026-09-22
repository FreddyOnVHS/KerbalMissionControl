using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using KMC.MissionControl.Engineering;
using KMC.Shared;

namespace KMC.MissionControl.Transport
{
    /// <summary>
    /// Build 14.22.30 sender-only bridge from the MAP transfer planner into
    /// the existing maneuver-uplink protocol.
    ///
    /// It intentionally does not bind the ACK or node-state ports.
    /// MissionControlReceiver's existing ManeuverLinkTransport remains the
    /// single receiver for those channels and publishes status into
    /// ManeuverUplinkStatusStore.
    /// </summary>
    public static class TransferPlannerManeuverUplink
    {
        public static bool Send(
            ManeuverUplinkPacket packet,
            out string resultText)
        {
            resultText = string.Empty;

            if (packet == null)
            {
                resultText = "NO MANEUVER PACKET";
                return false;
            }

            if (string.IsNullOrWhiteSpace(packet.VesselId) ||
                string.IsNullOrWhiteSpace(packet.PlanId))
            {
                resultText = "VESSEL / PLAN ID UNAVAILABLE";
                ManeuverUplinkStatusStore.PublishRejected(
                    packet.PlanId,
                    resultText);
                return false;
            }

            if (!IsFinite(packet.NodeUniversalTimeSeconds) ||
                !IsFinite(packet.ProgradeDeltaVMetersPerSecond) ||
                !IsFinite(packet.NormalDeltaVMetersPerSecond) ||
                !IsFinite(packet.RadialDeltaVMetersPerSecond))
            {
                resultText = "NODE COMMAND CONTAINS INVALID VALUES";
                ManeuverUplinkStatusStore.PublishRejected(
                    packet.PlanId,
                    resultText);
                return false;
            }

            UdpClient client = null;

            try
            {
                client = new UdpClient();

                byte[] data =
                    Encoding.UTF8.GetBytes(
                        packet.Serialize());

                client.Send(
                    data,
                    data.Length,
                    new IPEndPoint(
                        IPAddress.Loopback,
                        ManeuverUplinkPacket.CommandPort));

                ManeuverUplinkStatusStore.PublishPending(
                    packet.PlanId,
                    packet.NodeUniversalTimeSeconds);

                resultText = "UPLINK SENT - AWAITING PLUGIN ACK";
                return true;
            }
            catch (Exception ex)
            {
                resultText =
                    "UPLINK FAILED: " +
                    ex.Message;

                ManeuverUplinkStatusStore.PublishRejected(
                    packet.PlanId,
                    resultText);

                return false;
            }
            finally
            {
                if (client != null)
                {
                    client.Close();
                }
            }
        }

        private static bool IsFinite(double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
