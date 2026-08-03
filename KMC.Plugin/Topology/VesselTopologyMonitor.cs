using System;
using System.Net;
using System.Net.Sockets;
using KMC.Shared.Topology;
using UnityEngine;

namespace KMC.Plugin.Topology
{
    [KSPAddon(
        KSPAddon.Startup.Flight,
        false)]
    public sealed class VesselTopologyMonitor :
        MonoBehaviour
    {
        private const float ScanIntervalSeconds =
            0.50f;

        private readonly VesselTopologyService _service =
            new VesselTopologyService();

        private UdpClient _udpClient;
        private IPEndPoint _missionControlEndpoint;
        private float _nextScanTime;

        public void Start()
        {
            _service.Reset();
            _nextScanTime = 0.0f;

            try
            {
                _udpClient = new UdpClient();

                _missionControlEndpoint =
                    new IPEndPoint(
                        IPAddress.Loopback,
                        VesselTopologyPacketCodec
                            .TopologyPort);

                Debug.Log(
                    "[KMC] Vessel topology monitor started.");
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] Vessel topology transport failed to start: " +
                    ex);
            }
        }

        public void Update()
        {
            if (Time.realtimeSinceStartup <
                _nextScanTime)
            {
                return;
            }

            _nextScanTime =
                Time.realtimeSinceStartup +
                ScanIntervalSeconds;

            Vessel vessel =
                FlightGlobals.ActiveVessel;

            if (vessel == null)
            {
                return;
            }

            try
            {
                if (!_service.Update(vessel))
                {
                    return;
                }

                string report =
                    VesselTopologyDiagnostics
                        .CreateReport(
                            _service.Current);

                Debug.Log(report);

                SendTopology(
                    _service.Current);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] Vessel topology update failed: " +
                    ex);
            }
        }

        private void SendTopology(
            VesselTopology topology)
        {
            if (_udpClient == null ||
                _missionControlEndpoint == null ||
                topology == null)
            {
                return;
            }

            byte[] payload =
                VesselTopologyPacketCodec.Encode(
                    topology);

            _udpClient.Send(
                payload,
                payload.Length,
                _missionControlEndpoint);

            Debug.Log(
                "[KMC] Topology revision " +
                topology.Revision +
                " sent to Mission Control (" +
                payload.Length +
                " bytes).");
        }

        public void OnDestroy()
        {
            _service.Reset();

            if (_udpClient != null)
            {
                _udpClient.Close();
                _udpClient = null;
            }

            Debug.Log(
                "[KMC] Vessel topology monitor stopped.");
        }
    }
}
