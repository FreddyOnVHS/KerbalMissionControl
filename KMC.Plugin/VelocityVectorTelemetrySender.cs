using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace KMC.Plugin
{
    /// <summary>
    /// Publishes true three-dimensional surface and orbital velocity vectors
    /// resolved into the active vessel reference frame.
    ///
    /// This shares the established Mission Control UDP 5058 transport socket
    /// with KMC-ENGINE1. Packets are demultiplexed by protocol ID, so no
    /// additional Mission Control socket is required and KMC6 remains
    /// unchanged.
    /// </summary>
    [KSPAddon(
        KSPAddon.Startup.Flight,
        false)]
    public sealed class VelocityVectorTelemetrySender :
        MonoBehaviour
    {
        private const int VelocityTelemetryPort =
            5058;

        private const string ProtocolId =
            "KMC-VEL1";

        private const float SendIntervalSeconds =
            0.1f;

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
                        VelocityTelemetryPort);

                Debug.Log(
                    "[KMC] Velocity-vector telemetry sender started.");
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] Velocity-vector sender failed to start: " +
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
                vessel.ReferenceTransform == null)
            {
                return;
            }

            try
            {
                VectorComponents surface =
                    ResolveVelocity(
                        vessel.srf_velocity,
                        vessel);

                VectorComponents orbital =
                    ResolveVelocity(
                        vessel.obt_velocity,
                        vessel);

                string message =
                    ProtocolId +
                    "|" +
                    DateTime.UtcNow.Ticks.ToString(
                        CultureInfo.InvariantCulture) +
                    "|" +
                    Uri.EscapeDataString(
                        vessel.vesselName ??
                        string.Empty) +
                    "|" +
                    Format(surface.Right) +
                    "|" +
                    Format(surface.Nose) +
                    "|" +
                    Format(surface.ReferenceForward) +
                    "|" +
                    Format(orbital.Right) +
                    "|" +
                    Format(orbital.Nose) +
                    "|" +
                    Format(orbital.ReferenceForward);

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
                    "[KMC] Velocity-vector send failed: " +
                    ex);
            }
        }

        private static VectorComponents ResolveVelocity(
            Vector3d velocity,
            Vessel vessel)
        {
            VectorComponents result =
                new VectorComponents();

            if (vessel == null ||
                vessel.ReferenceTransform == null)
            {
                return result;
            }

            Vector3d vesselRight =
                vessel.ReferenceTransform.right;

            Vector3d vesselNose =
                vessel.ReferenceTransform.up;

            Vector3d vesselReferenceForward =
                vessel.ReferenceTransform.forward;

            result.Right =
                Vector3d.Dot(
                    velocity,
                    vesselRight);

            result.Nose =
                Vector3d.Dot(
                    velocity,
                    vesselNose);

            result.ReferenceForward =
                Vector3d.Dot(
                    velocity,
                    vesselReferenceForward);

            return result;
        }

        private static string Format(
            double value)
        {
            if (double.IsNaN(value) ||
                double.IsInfinity(value))
            {
                value =
                    0.0;
            }

            return
                value.ToString(
                    "R",
                    CultureInfo.InvariantCulture);
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

            Debug.Log(
                "[KMC] Velocity-vector telemetry sender stopped.");
        }

        private sealed class VectorComponents
        {
            public double Right;
            public double Nose;
            public double ReferenceForward;
        }
    }
}
