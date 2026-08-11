using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace KMC.Plugin
{
    /// <summary>
    /// Publishes true three-dimensional surface/orbital velocity vectors and
    /// the true orbital-plane normal, all resolved into the active vessel
    /// reference frame.
    ///
    /// KMC-VEL1 remains unchanged on UDP 5058.
    /// Build 13.4 adds KMC-NORM1 on UDP 5098.
    /// </summary>
    [KSPAddon(
        KSPAddon.Startup.Flight,
        false)]
    public sealed class VelocityVectorTelemetrySender :
        MonoBehaviour
    {
        private const int VelocityTelemetryPort =
            5058;

        private const int OrbitNormalTelemetryPort =
            5098;

        private const string VelocityProtocolId =
            "KMC-VEL1";

        private const string OrbitNormalProtocolId =
            "KMC-NORM1";

        private const float SendIntervalSeconds =
            0.1f;

        private UdpClient _udpClient;
        private IPEndPoint _velocityEndpoint;
        private IPEndPoint _orbitNormalEndpoint;
        private float _nextSendTime;

        public void Start()
        {
            try
            {
                _udpClient =
                    new UdpClient();

                _velocityEndpoint =
                    new IPEndPoint(
                        IPAddress.Loopback,
                        VelocityTelemetryPort);

                _orbitNormalEndpoint =
                    new IPEndPoint(
                        IPAddress.Loopback,
                        OrbitNormalTelemetryPort);

                Debug.Log(
                    "[KMC] Velocity/orbit-normal telemetry sender started.");
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] Velocity/orbit-normal sender failed to start: " +
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
                DateTime nowUtc =
                    DateTime.UtcNow;

                VectorComponents surface =
                    ResolveVector(
                        vessel.srf_velocity,
                        vessel);

                VectorComponents orbital =
                    ResolveVector(
                        vessel.obt_velocity,
                        vessel);

                string velocityMessage =
                    VelocityProtocolId +
                    "|" +
                    nowUtc.Ticks.ToString(
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

                Send(
                    velocityMessage,
                    _velocityEndpoint);

                /*
                 * Orbital normal follows the standard specific angular-
                 * momentum direction h = r x v.
                 *
                 * vessel.upAxis is radial-out from the current main body.
                 * The result is normalized before resolving it in the vessel
                 * ReferenceTransform frame.
                 */
                Vector3d orbitNormal =
                    Vector3d.Cross(
                        vessel.upAxis,
                        vessel.obt_velocity);

                double normalMagnitude =
                    orbitNormal.magnitude;

                if (normalMagnitude >
                    1.0e-9)
                {
                    orbitNormal =
                        orbitNormal /
                        normalMagnitude;
                }
                else
                {
                    orbitNormal =
                        Vector3d.zero;
                }

                VectorComponents normal =
                    ResolveVector(
                        orbitNormal,
                        vessel);

                string normalMessage =
                    OrbitNormalProtocolId +
                    "|" +
                    nowUtc.Ticks.ToString(
                        CultureInfo.InvariantCulture) +
                    "|" +
                    Uri.EscapeDataString(
                        vessel.vesselName ??
                        string.Empty) +
                    "|" +
                    Format(normal.Right) +
                    "|" +
                    Format(normal.Nose) +
                    "|" +
                    Format(normal.ReferenceForward);

                Send(
                    normalMessage,
                    _orbitNormalEndpoint);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] Velocity/orbit-normal send failed: " +
                    ex);
            }
        }

        private void Send(
            string message,
            IPEndPoint endpoint)
        {
            if (_udpClient == null ||
                endpoint == null ||
                string.IsNullOrEmpty(
                    message))
            {
                return;
            }

            byte[] payload =
                Encoding.UTF8.GetBytes(
                    message);

            _udpClient.Send(
                payload,
                payload.Length,
                endpoint);
        }

        private static VectorComponents ResolveVector(
            Vector3d vector,
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
                    vector,
                    vesselRight);

            result.Nose =
                Vector3d.Dot(
                    vector,
                    vesselNose);

            result.ReferenceForward =
                Vector3d.Dot(
                    vector,
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
                "[KMC] Velocity/orbit-normal telemetry sender stopped.");
        }

        private sealed class VectorComponents
        {
            public double Right;
            public double Nose;
            public double ReferenceForward;
        }
    }
}
