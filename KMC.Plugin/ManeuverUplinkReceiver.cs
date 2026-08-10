using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using KMC.Shared;
using UnityEngine;

namespace KMC.Plugin
{
    /// <summary>
    /// Receives reviewed Mission Control maneuver plans and creates a stock KSP
    /// maneuver node on the matching active vessel. KSP flight-state changes are
    /// performed only from Update(), never from the UDP receiver thread.
    /// </summary>
    [KSPAddon(
        KSPAddon.Startup.Flight,
        false)]
    public sealed class ManeuverUplinkReceiver :
        MonoBehaviour
    {
        private readonly object _syncRoot =
            new object();

        private readonly HashSet<string> _loadedPlanIds =
            new HashSet<string>(
                StringComparer.Ordinal);

        private UdpClient _receiveClient;
        private UdpClient _ackClient;
        private IPEndPoint _ackEndpoint;
        private Thread _receiveThread;
        private volatile bool _running;
        private ManeuverUplinkPacket _pending;

        public void Start()
        {
            try
            {
                _receiveClient =
                    new UdpClient(
                        new IPEndPoint(
                            IPAddress.Loopback,
                            ManeuverUplinkPacket.CommandPort));

                _ackClient =
                    new UdpClient();

                _ackEndpoint =
                    new IPEndPoint(
                        IPAddress.Loopback,
                        ManeuverUplinkPacket.AckPort);

                _running = true;

                _receiveThread =
                    new Thread(
                        ReceiveLoop);

                _receiveThread.IsBackground = true;
                _receiveThread.Name = "KMC Maneuver Uplink";
                _receiveThread.Start();

                Debug.Log(
                    "[KMC] Maneuver uplink receiver started.");
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] Maneuver uplink receiver start failed: " +
                    ex);
            }
        }

        public void Update()
        {
            ManeuverUplinkPacket packet = null;

            lock (_syncRoot)
            {
                if (_pending != null)
                {
                    packet = _pending;
                    _pending = null;
                }
            }

            if (packet != null)
            {
                ApplyManeuver(packet);
            }
        }

        private void ReceiveLoop()
        {
            while (_running)
            {
                try
                {
                    IPEndPoint sender =
                        new IPEndPoint(
                            IPAddress.Any,
                            0);

                    byte[] data =
                        _receiveClient.Receive(
                            ref sender);

                    string text =
                        Encoding.UTF8.GetString(
                            data);

                    ManeuverUplinkPacket packet;

                    if (!ManeuverUplinkPacket.TryParse(
                            text,
                            out packet))
                    {
                        continue;
                    }

                    lock (_syncRoot)
                    {
                        _pending = packet;
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                {
                    if (!_running)
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        "[KMC] Maneuver uplink receive failed: " +
                        ex);
                }
            }
        }

        private void ApplyManeuver(
            ManeuverUplinkPacket packet)
        {
            Vessel vessel =
                FlightGlobals.ActiveVessel;

            if (vessel == null)
            {
                SendAck(
                    packet,
                    "REJECTED",
                    "NO ACTIVE VESSEL",
                    double.NaN);

                return;
            }

            string activeVesselId =
                vessel.id.ToString();

            if (!string.Equals(
                    activeVesselId,
                    packet.VesselId,
                    StringComparison.Ordinal))
            {
                SendAck(
                    packet,
                    "REJECTED",
                    "ACTIVE VESSEL ID DOES NOT MATCH PLAN",
                    double.NaN);

                return;
            }

            if (_loadedPlanIds.Contains(
                    packet.PlanId))
            {
                SendAck(
                    packet,
                    "NODE LOADED",
                    "PLAN ALREADY LOADED - DUPLICATE SUPPRESSED",
                    packet.NodeUniversalTimeSeconds);

                return;
            }

            double currentUt =
                Planetarium.GetUniversalTime();

            if (!IsFinite(packet.NodeUniversalTimeSeconds) ||
                packet.NodeUniversalTimeSeconds <=
                    currentUt + 0.25)
            {
                SendAck(
                    packet,
                    "REJECTED",
                    "NODE UT IS NOT IN THE FUTURE",
                    double.NaN);

                return;
            }

            if (!IsFinite(packet.ProgradeDeltaVMetersPerSecond) ||
                !IsFinite(packet.NormalDeltaVMetersPerSecond) ||
                !IsFinite(packet.RadialDeltaVMetersPerSecond))
            {
                SendAck(
                    packet,
                    "REJECTED",
                    "MANEUVER DELTA-V IS INVALID",
                    double.NaN);

                return;
            }

            if (vessel.patchedConicSolver == null)
            {
                SendAck(
                    packet,
                    "REJECTED",
                    "PATCHED CONIC SOLVER UNAVAILABLE",
                    double.NaN);

                return;
            }

            try
            {
                ManeuverNode node =
                    vessel.patchedConicSolver
                        .AddManeuverNode(
                            packet.NodeUniversalTimeSeconds);

                if (node == null)
                {
                    SendAck(
                        packet,
                        "REJECTED",
                        "KSP DID NOT CREATE A MANEUVER NODE",
                        double.NaN);

                    return;
                }

                /*
                 * KSP ManeuverNode coordinates:
                 * X = radial-out, Y = normal, Z = prograde.
                 */
                node.DeltaV =
                    new Vector3d(
                        packet.RadialDeltaVMetersPerSecond,
                        packet.NormalDeltaVMetersPerSecond,
                        packet.ProgradeDeltaVMetersPerSecond);

                vessel.patchedConicSolver
                    .UpdateFlightPlan();

                _loadedPlanIds.Add(
                    packet.PlanId);

                SendAck(
                    packet,
                    "NODE LOADED",
                    "PLUGIN CREATED MANEUVER NODE",
                    packet.NodeUniversalTimeSeconds);

                ScreenMessages.PostScreenMessage(
                    "KMC maneuver node loaded",
                    4f,
                    ScreenMessageStyle.UPPER_CENTER);

                Debug.Log(
                    "[KMC] MANEUVER NODE LOADED | PlanId=" +
                    packet.PlanId +
                    " | VesselId=" +
                    packet.VesselId +
                    " | NodeUT=" +
                    packet.NodeUniversalTimeSeconds.ToString("0.0") +
                    " | ProgradeDV=" +
                    packet.ProgradeDeltaVMetersPerSecond.ToString("0.00") +
                    " | NormalDV=" +
                    packet.NormalDeltaVMetersPerSecond.ToString("0.00") +
                    " | RadialDV=" +
                    packet.RadialDeltaVMetersPerSecond.ToString("0.00"));
            }
            catch (Exception ex)
            {
                SendAck(
                    packet,
                    "REJECTED",
                    "KSP NODE CREATION FAILED: " +
                    ex.GetType().Name,
                    double.NaN);

                Debug.LogError(
                    "[KMC] Maneuver node creation failed: " +
                    ex);
            }
        }

        private void SendAck(
            ManeuverUplinkPacket packet,
            string status,
            string detail,
            double nodeUt)
        {
            if (_ackClient == null ||
                _ackEndpoint == null ||
                packet == null)
            {
                return;
            }

            try
            {
                ManeuverUplinkAck ack =
                    new ManeuverUplinkAck
                    {
                        VesselId =
                            packet.VesselId ?? string.Empty,

                        PlanId =
                            packet.PlanId ?? string.Empty,

                        Status =
                            status ?? string.Empty,

                        NodeUniversalTimeSeconds =
                            nodeUt,

                        Detail =
                            detail ?? string.Empty
                    };

                byte[] data =
                    Encoding.UTF8.GetBytes(
                        ack.Serialize());

                _ackClient.Send(
                    data,
                    data.Length,
                    _ackEndpoint);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] Maneuver ACK send failed: " +
                    ex);
            }
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }

        public void OnDestroy()
        {
            _running = false;

            if (_receiveClient != null)
            {
                _receiveClient.Close();
                _receiveClient = null;
            }

            if (_ackClient != null)
            {
                _ackClient.Close();
                _ackClient = null;
            }

            if (_receiveThread != null &&
                _receiveThread.IsAlive)
            {
                _receiveThread.Join(250);
            }

            _receiveThread = null;
        }
    }
}
