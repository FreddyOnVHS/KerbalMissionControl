using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using KMC.Shared;
using UnityEngine;

namespace KMC.Plugin
{
    /// <summary>
    /// Build 11.2 side-channel for genuine KSP Universal Time and vessel id.
    /// </summary>
    [KSPAddon(
        KSPAddon.Startup.Flight,
        false)]
    public sealed class ManeuverEpochTelemetrySender :
        MonoBehaviour
    {
        private const float SendIntervalSeconds = 0.1f;

        private UdpClient _udpClient;
        private IPEndPoint _missionControlEndpoint;
        private float _nextSendTime;

        public void Start()
        {
            try
            {
                _udpClient = new UdpClient();

                _missionControlEndpoint =
                    new IPEndPoint(
                        IPAddress.Loopback,
                        ManeuverEpochPacket.TelemetryPort);

                Debug.Log(
                    "[KMC] Maneuver epoch telemetry started.");
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] Maneuver epoch telemetry start failed: " +
                    ex);
            }
        }

        public void Update()
        {
            if (_udpClient == null ||
                Time.realtimeSinceStartup < _nextSendTime)
            {
                return;
            }

            _nextSendTime =
                Time.realtimeSinceStartup +
                SendIntervalSeconds;

            Vessel vessel =
                FlightGlobals.ActiveVessel;

            if (vessel == null)
            {
                return;
            }

            try
            {
                ManeuverEpochPacket packet =
                    new ManeuverEpochPacket
                    {
                        TimestampUtc = DateTime.UtcNow,
                        VesselId = vessel.id.ToString(),
                        VesselName = vessel.vesselName ?? string.Empty,
                        UniversalTimeSeconds =
                            Planetarium.GetUniversalTime(),
                        MissionTimeSeconds =
                            vessel.missionTime
                    };

                byte[] data =
                    Encoding.UTF8.GetBytes(
                        packet.Serialize());

                _udpClient.Send(
                    data,
                    data.Length,
                    _missionControlEndpoint);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] Maneuver epoch telemetry send failed: " +
                    ex);
            }
        }

        public void OnDestroy()
        {
            if (_udpClient != null)
            {
                _udpClient.Close();
                _udpClient = null;
            }
        }
    }
}
