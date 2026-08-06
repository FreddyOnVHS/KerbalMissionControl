using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace KMC.Plugin
{
    /// <summary>
    /// Publishes the active vessel's actual SAS action-group state to the
    /// persistent Mission Control annunciator panel.
    ///
    /// This deliberately uses a small independent channel so the established
    /// KMC6 telemetry packet remains backward compatible.
    /// </summary>
    [KSPAddon(
        KSPAddon.Startup.Flight,
        false)]
    public sealed class SasStateTelemetrySender :
        MonoBehaviour
    {
        private const float SendIntervalSeconds =
            0.1f;

        private const int SasTelemetryPort =
            5060;

        private const string ProtocolId =
            "KMCSAS1";

        private UdpClient _udpClient;
        private IPEndPoint _endpoint;
        private float _nextSendTime;

        public void Start()
        {
            try
            {
                _udpClient =
                    new UdpClient();

                _endpoint =
                    new IPEndPoint(
                        IPAddress.Loopback,
                        SasTelemetryPort);

                Debug.Log(
                    "[KMC] SAS state telemetry sender started.");
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] SAS state sender failed to start: " +
                    ex);
            }
        }

        public void Update()
        {
            if (_udpClient == null ||
                Time.realtimeSinceStartup <
                    _nextSendTime)
            {
                return;
            }

            _nextSendTime =
                Time.realtimeSinceStartup +
                SendIntervalSeconds;

            Vessel vessel =
                FlightGlobals.ActiveVessel;

            if (vessel == null ||
                vessel.ActionGroups == null)
            {
                return;
            }

            try
            {
                bool enabled =
                    vessel.ActionGroups[
                        KSPActionGroup.SAS];

                string message =
                    ProtocolId +
                    "|" +
                    (enabled
                        ? "1"
                        : "0");

                byte[] payload =
                    Encoding.UTF8.GetBytes(
                        message);

                _udpClient.Send(
                    payload,
                    payload.Length,
                    _endpoint);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] SAS state send failed: " +
                    ex);
            }
        }

        public void OnDestroy()
        {
            UdpClient client =
                _udpClient;

            _udpClient =
                null;

            if (client != null)
            {
                try
                {
                    client.Close();
                }
                catch
                {
                }
            }
        }
    }
}
