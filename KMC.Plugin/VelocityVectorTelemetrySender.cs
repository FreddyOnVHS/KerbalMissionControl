using System;
using System.Globalization;
using System.Collections.Generic;
using System.Threading;
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

        private const int RadialTelemetryPort =
            5099;

        private const string VelocityProtocolId =
            "KMC-VEL1";

        private const string OrbitNormalProtocolId =
            "KMC-NORM1";

        private const string RadialProtocolId =
            "KMC-RAD1";

        private const float SendIntervalSeconds =
            0.1f;

        private UdpClient _udpClient;
        private IPEndPoint _velocityEndpoint;
        private IPEndPoint _orbitNormalEndpoint;
        private IPEndPoint _radialEndpoint;
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

                _radialEndpoint =
                    new IPEndPoint(
                        IPAddress.Loopback,
                        RadialTelemetryPort);

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
                 * Build 13.4.1 polarity correction.
                 *
                 * KSP's Normal SAS direction is opposite the original
                 * Build 13.4 radial-out x orbital-velocity result in this
                 * coordinate convention.
                 *
                 * Reversing the cross-product order makes:
                 *   +NormalDV -> KSP Normal
                 *   -NormalDV -> KSP Anti-Normal
                 */
                Vector3d orbitNormal =
                    Vector3d.Cross(
                        vessel.obt_velocity,
                        vessel.upAxis);

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

                /*
                 * Build 13.5 true radial-out reference. vessel.upAxis is
                 * KSP's local radial-out direction from the central body.
                 */
                Vector3d radialOut =
                    vessel.upAxis;

                double radialMagnitude =
                    radialOut.magnitude;

                if (radialMagnitude > 1.0e-9)
                {
                    radialOut = radialOut / radialMagnitude;
                }
                else
                {
                    radialOut = Vector3d.zero;
                }

                VectorComponents radial =
                    ResolveVector(
                        radialOut,
                        vessel);

                string radialMessage =
                    RadialProtocolId +
                    "|" +
                    nowUtc.Ticks.ToString(
                        CultureInfo.InvariantCulture) +
                    "|" +
                    Uri.EscapeDataString(
                        vessel.vesselName ??
                        string.Empty) +
                    "|" +
                    Format(radial.Right) +
                    "|" +
                    Format(radial.Nose) +
                    "|" +
                    Format(radial.ReferenceForward);

                Send(
                    radialMessage,
                    _radialEndpoint);
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

    /// <summary>
    /// Build 13.6 complete stock maneuver-node inventory and exact-node delete
    /// service. All stock KSP reads/mutations happen on Unity Update().
    /// </summary>
    [KSPAddon(
        KSPAddon.Startup.Flight,
        false)]
    public sealed class ManeuverInventoryTelemetryService :
        MonoBehaviour
    {
        private const int InventoryPort = 5100;
        private const int DeletePort = 5101;
        private const string InventoryProtocol = "KMC-MNVI1";
        private const string DeleteProtocol = "KMC-MNVD1";
        private const float SendIntervalSeconds = 0.50f;

        private readonly object _syncRoot = new object();
        private readonly Queue<DeleteRequest> _pendingDeletes = new Queue<DeleteRequest>();
        private readonly Dictionary<ManeuverNode, string> _nodeIds = new Dictionary<ManeuverNode, string>();
        private UdpClient _inventoryClient;
        private UdpClient _deleteClient;
        private IPEndPoint _inventoryEndpoint;
        private Thread _deleteThread;
        private volatile bool _running;
        private float _nextSendTime;
        private int _nodeSequence;

        public void Start()
        {
            try
            {
                _inventoryClient = new UdpClient();
                _inventoryEndpoint = new IPEndPoint(IPAddress.Loopback, InventoryPort);
                _deleteClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, DeletePort));
                _running = true;
                _deleteThread = new Thread(DeleteReceiveLoop);
                _deleteThread.IsBackground = true;
                _deleteThread.Name = "KMC Maneuver Delete";
                _deleteThread.Start();
                Debug.Log("[KMC] Maneuver inventory/delete service started. UDP 5100/5101.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[KMC] Maneuver inventory service start failed: " + ex);
            }
        }

        public void Update()
        {
            ProcessPendingDeletes();

            if (Time.realtimeSinceStartup >= _nextSendTime)
            {
                _nextSendTime = Time.realtimeSinceStartup + SendIntervalSeconds;
                PublishInventory();
            }
        }

        private void DeleteReceiveLoop()
        {
            while (_running)
            {
                try
                {
                    IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _deleteClient.Receive(ref sender);
                    string text = Encoding.UTF8.GetString(data);
                    string[] fields = text.Split('|');

                    if (fields.Length != 3 ||
                        !string.Equals(fields[0], DeleteProtocol, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    DeleteRequest request = new DeleteRequest
                    {
                        VesselId = Uri.UnescapeDataString(fields[1]),
                        NodeId = Uri.UnescapeDataString(fields[2])
                    };

                    if (string.IsNullOrWhiteSpace(request.VesselId) ||
                        string.IsNullOrWhiteSpace(request.NodeId))
                    {
                        continue;
                    }

                    lock (_syncRoot)
                    {
                        _pendingDeletes.Enqueue(request);
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                {
                    if (!_running) return;
                }
                catch (Exception ex)
                {
                    Debug.LogError("[KMC] Maneuver delete receive failed: " + ex);
                }
            }
        }

        private void ProcessPendingDeletes()
        {
            while (true)
            {
                DeleteRequest request = null;
                lock (_syncRoot)
                {
                    if (_pendingDeletes.Count > 0)
                    {
                        request = _pendingDeletes.Dequeue();
                    }
                }

                if (request == null) return;
                ApplyDelete(request);
            }
        }

        private void ApplyDelete(DeleteRequest request)
        {
            Vessel vessel = FlightGlobals.ActiveVessel;

            if (vessel == null ||
                !string.Equals(vessel.id.ToString(), request.VesselId, StringComparison.Ordinal) ||
                vessel.patchedConicSolver == null ||
                vessel.patchedConicSolver.maneuverNodes == null)
            {
                Debug.Log("[KMC] MANEUVER DELETE REJECTED | NodeId=" + request.NodeId + " | Reason=VESSEL/SOLVER UNAVAILABLE");
                return;
            }

            ManeuverNode target = null;
            foreach (KeyValuePair<ManeuverNode, string> pair in _nodeIds)
            {
                if (string.Equals(pair.Value, request.NodeId, StringComparison.Ordinal))
                {
                    target = pair.Key;
                    break;
                }
            }

            if (target == null || !vessel.patchedConicSolver.maneuverNodes.Contains(target))
            {
                Debug.Log("[KMC] MANEUVER DELETE REJECTED | NodeId=" + request.NodeId + " | Reason=NODE NOT FOUND");
                return;
            }

            try
            {
                double nodeUt = target.UT;
                target.RemoveSelf();
                vessel.patchedConicSolver.UpdateFlightPlan();
                _nodeIds.Remove(target);

                Debug.Log("[KMC] MANEUVER NODE DELETED | NodeId=" + request.NodeId + " | NodeUT=" + nodeUt.ToString("0.0"));
                ScreenMessages.PostScreenMessage("KMC maneuver node deleted", 3f, ScreenMessageStyle.UPPER_CENTER);
                PublishInventory();
            }
            catch (Exception ex)
            {
                Debug.LogError("[KMC] Maneuver node delete failed: " + ex);
            }
        }

        private void PublishInventory()
        {
            if (_inventoryClient == null || _inventoryEndpoint == null) return;

            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null) return;

            double currentUt = Planetarium.GetUniversalTime();
            List<ManeuverNode> nodes = new List<ManeuverNode>();

            if (vessel.patchedConicSolver != null &&
                vessel.patchedConicSolver.maneuverNodes != null)
            {
                nodes.AddRange(vessel.patchedConicSolver.maneuverNodes);
            }

            nodes.Sort(delegate(ManeuverNode a, ManeuverNode b) { return a.UT.CompareTo(b.UT); });
            PruneNodeIds(nodes);

            List<string> fields = new List<string>();
            fields.Add(InventoryProtocol);
            fields.Add(DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
            fields.Add(Uri.EscapeDataString(vessel.id.ToString()));
            fields.Add(Uri.EscapeDataString(vessel.vesselName ?? string.Empty));
            fields.Add(currentUt.ToString("R", CultureInfo.InvariantCulture));
            fields.Add(nodes.Count.ToString(CultureInfo.InvariantCulture));

            for (int index = 0; index < nodes.Count; index++)
            {
                ManeuverNode node = nodes[index];
                string nodeId = GetNodeId(node);
                Vector3d dv = node.DeltaV;
                string entry =
                    Uri.EscapeDataString(nodeId) + "~" +
                    node.UT.ToString("R", CultureInfo.InvariantCulture) + "~" +
                    dv.z.ToString("R", CultureInfo.InvariantCulture) + "~" +
                    dv.y.ToString("R", CultureInfo.InvariantCulture) + "~" +
                    dv.x.ToString("R", CultureInfo.InvariantCulture);
                fields.Add(entry);
            }

            try
            {
                byte[] data = Encoding.UTF8.GetBytes(string.Join("|", fields.ToArray()));
                _inventoryClient.Send(data, data.Length, _inventoryEndpoint);
            }
            catch (Exception ex)
            {
                Debug.LogError("[KMC] Maneuver inventory send failed: " + ex);
            }
        }

        private string GetNodeId(ManeuverNode node)
        {
            string id;
            if (_nodeIds.TryGetValue(node, out id)) return id;
            _nodeSequence++;
            id = "KSP-NODE-" + _nodeSequence.ToString("D4");
            _nodeIds[node] = id;
            return id;
        }

        private void PruneNodeIds(List<ManeuverNode> liveNodes)
        {
            List<ManeuverNode> remove = new List<ManeuverNode>();
            foreach (KeyValuePair<ManeuverNode, string> pair in _nodeIds)
            {
                if (pair.Key == null || !liveNodes.Contains(pair.Key)) remove.Add(pair.Key);
            }
            for (int index = 0; index < remove.Count; index++) _nodeIds.Remove(remove[index]);
        }

        public void OnDestroy()
        {
            _running = false;
            if (_deleteClient != null) { _deleteClient.Close(); _deleteClient = null; }
            if (_inventoryClient != null) { _inventoryClient.Close(); _inventoryClient = null; }
            if (_deleteThread != null && _deleteThread.IsAlive) _deleteThread.Join(250);
            _deleteThread = null;
            _nodeIds.Clear();
            lock (_syncRoot) { _pendingDeletes.Clear(); }
            Debug.Log("[KMC] Maneuver inventory/delete service stopped.");
        }

        private sealed class DeleteRequest
        {
            public string VesselId;
            public string NodeId;
        }
    }

}
